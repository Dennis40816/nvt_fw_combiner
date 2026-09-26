using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Regressions proving that a CLI report write replaces its destination whole or not at all: a failed or
/// cancelled write leaves the earlier report and no staging file behind.
/// </summary>
public sealed class CliReportAtomicWriteTests
{
    private const int DiskFullHResult = unchecked((int)0x80070070);
    private const string StagingFileName = ".nfc-report-test.tmp";
    private const string ReportJson = "{\"Status\":\"Succeeded\",\"Note\":\"übersetzt\"}";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] EarlierReport = Utf8WithoutBom.GetBytes(
        $"{{\"Status\":\"Earlier\",\"Padding\":\"{new string('x', 4096)}\"}}");

    /// <summary>A successful write stores the exact UTF-8 report bytes, prints the Report line and leaves no staging file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulWriteStoresTheExactReportBytes(bool replaceEarlierReport)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-success");
        string reportDirectory = workspace.PathFor("reports");
        string reportPath = Path.Combine(reportDirectory, "report.json");
        if (replaceEarlierReport)
        {
            _ = workspace.Write("reports/report.json", EarlierReport);
        }

        using var output = new StringWriter(CultureInfo.InvariantCulture);

        await CliCompositionRunSupport.WriteReportJsonAsync(
            reportPath,
            ReportJson,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            Utf8WithoutBom.GetBytes(ReportJson),
            await File.ReadAllBytesAsync(reportPath, TestContext.Current.CancellationToken));
        Assert.Equal($"Report: {reportPath}{Environment.NewLine}", output.ToString());
        Assert.Equal([reportPath], Directory.GetFiles(reportDirectory));
    }

    /// <summary>A write that fails halfway, including on a full disk, keeps the earlier report and removes the staging file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MidwayWriteFailureKeepsTheEarlierReport(bool diskFull)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-io");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        IOException injected = diskFull
            ? new IOException("There is not enough space on the disk.", DiskFullHResult)
            : new IOException("The report write failed.");
        string? writtenPath = null;
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        IOException failure = await Assert.ThrowsAsync<IOException>(() =>
            CliCompositionRunSupport.WriteReportJsonAsync(
                reportPath,
                ReportJson,
                output,
                StagingFileName,
                async (stream, content, token) =>
                {
                    writtenPath = WriteHalf(stream, content);
                    await stream.FlushAsync(token);
                    throw injected;
                },
                TestContext.Current.CancellationToken));

        Assert.Same(injected, failure);
        AssertEarlierReportKept(reportPath, output, writtenPath);
    }

    /// <summary>A cancellation while the report is written keeps the earlier report and removes the staging file.</summary>
    [Fact]
    public async Task MidwayCancellationKeepsTheEarlierReport()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-cancel");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        string? writtenPath = null;
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CliCompositionRunSupport.WriteReportJsonAsync(
                reportPath,
                ReportJson,
                output,
                StagingFileName,
                async (stream, content, token) =>
                {
                    writtenPath = WriteHalf(stream, content);
                    await stream.FlushAsync(token);
                    await cancellation.CancelAsync();
                    token.ThrowIfCancellationRequested();
                },
                cancellation.Token));

        AssertEarlierReportKept(reportPath, output, writtenPath);
    }

    /// <summary>A cancellation requested before the write starts creates no file and keeps the earlier report.</summary>
    [Fact]
    public async Task CancellationBeforeTheWriteKeepsTheEarlierReport()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-precancel");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CliCompositionRunSupport.WriteReportJsonAsync(
                reportPath,
                ReportJson,
                output,
                cancellation.Token));

        AssertEarlierReportKept(reportPath, output);
    }

    /// <summary>A destination that cannot be replaced keeps the earlier report and removes the complete staging file.</summary>
    [Fact]
    public async Task ReplaceFailureKeepsTheEarlierReport()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to rename over a file held open without delete sharing.");
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-replace");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        string? writtenPath = null;
        long writtenLength = 0;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Exception failure;
        using (new FileStream(reportPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            failure = await Assert.ThrowsAnyAsync<Exception>(() =>
                CliCompositionRunSupport.WriteReportJsonAsync(
                    reportPath,
                    ReportJson,
                    output,
                    StagingFileName,
                    async (stream, content, token) =>
                    {
                        await stream.WriteAsync(content, token);
                        writtenPath = Assert.IsType<FileStream>(stream).Name;
                        writtenLength = stream.Length;
                    },
                    TestContext.Current.CancellationToken));
        }

        Assert.True(failure is IOException or UnauthorizedAccessException, failure.ToString());
        Assert.Equal(Utf8WithoutBom.GetByteCount(ReportJson), writtenLength);
        AssertEarlierReportKept(reportPath, output, writtenPath);
    }

    /// <summary>
    /// A staging name that names the report itself, directly or after case folding, path normalization,
    /// an alternate data stream or Unicode normalization, is refused before any file is created, so a
    /// missing report is never written directly instead of being renamed into place.
    /// </summary>
    [Theory]
    [InlineData("same-name")]
    [InlineData("different-case")]
    [InlineData("trailing-dot")]
    [InlineData("alternate-data-stream")]
    [InlineData("unicode-decomposed")]
    public async Task StagingNameThatNamesTheReportIsRefusedBeforeAnyWrite(string alias)
    {
        (string reportName, string stagingName, bool windowsAliasOnly) = alias switch
        {
            "same-name" => ("report.json", "report.json", false),
            "different-case" => ("report.json", "REPORT.JSON", false),
            "trailing-dot" => ("report.json", "report.json.", true),
            "alternate-data-stream" => ("report.json", "report.json:stream", true),
            "unicode-decomposed" => (
                (char)0x00FC + "bersicht.json",
                "u" + (char)0x0308 + "bersicht.json",
                false),
            _ => throw new ArgumentOutOfRangeException(nameof(alias), alias, "Unknown staging alias."),
        };
        if (windowsAliasOnly && !OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows path normalization makes this staging name alias the report.");
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-alias");
        string reportDirectory = workspace.PathFor("reports");
        _ = Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, reportName);
        bool contentWritten = false;
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            CliCompositionRunSupport.WriteReportJsonAsync(
                reportPath,
                ReportJson,
                output,
                stagingName,
                (stream, content, token) =>
                {
                    contentWritten = true;
                    return stream.WriteAsync(content, token);
                },
                TestContext.Current.CancellationToken));

        Assert.False(contentWritten);
        Assert.Empty(Directory.GetFileSystemEntries(reportDirectory));
        Assert.Empty(output.ToString());
    }

    /// <summary>
    /// A staging name that already belongs to another file fails the write before any report byte is
    /// written, and neither that file nor the earlier report is deleted or changed.
    /// </summary>
    [Fact]
    public async Task ExistingFileWithTheStagingNameIsNeitherDeletedNorChanged()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-collision");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        byte[] foreignBytes = Utf8WithoutBom.GetBytes("not created by this report write");
        string foreignPath = workspace.Write($"reports/{StagingFileName}", foreignBytes);
        bool contentWritten = false;
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        IOException failure = await Assert.ThrowsAnyAsync<IOException>(() =>
            CliCompositionRunSupport.WriteReportJsonAsync(
                reportPath,
                ReportJson,
                output,
                StagingFileName,
                (stream, content, token) =>
                {
                    contentWritten = true;
                    return stream.WriteAsync(content, token);
                },
                TestContext.Current.CancellationToken));

        Assert.False(contentWritten, failure.ToString());
        Assert.Equal(foreignBytes, await File.ReadAllBytesAsync(foreignPath, TestContext.Current.CancellationToken));
        Assert.Equal(EarlierReport, await File.ReadAllBytesAsync(reportPath, TestContext.Current.CancellationToken));
        Assert.Equal(
            [foreignPath, reportPath],
            Directory.GetFiles(Path.GetDirectoryName(reportPath)!).Order(StringComparer.Ordinal));
        Assert.Empty(output.ToString());
    }

    /// <summary>A committed Build replaces an earlier report with its complete run report and leaves no staging file.</summary>
    [Fact]
    public async Task CommittedBuildReplacesTheEarlierReport()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-build");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);

        CliRunResult result = await CliTestHarness.RunAsync(
            CreateGeneralMergeBuildArguments(workspace, reportPath),
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        byte[] committed = await File.ReadAllBytesAsync(
            workspace.PathFor("committed.bin"),
            TestContext.Current.CancellationToken);
        byte[] reportBytes = await File.ReadAllBytesAsync(reportPath, TestContext.Current.CancellationToken);
        Assert.Equal((byte)'{', reportBytes[0]);
        using JsonDocument report = JsonDocument.Parse(reportBytes);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(committed)),
            report.RootElement.GetProperty("Output").GetProperty("Sha256").GetString());
        Assert.Contains($"Report: {reportPath}", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CliCompositionRunSupport.CommittedReportFailedIssueCode,
            result.Error,
            StringComparison.Ordinal);
        Assert.Equal([reportPath], Directory.GetFiles(Path.GetDirectoryName(reportPath)!));
    }

    /// <summary>A cancellation after the commit keeps the earlier report, leaves no staging file and keeps exit code 0.</summary>
    [Fact]
    public async Task CommittedBuildCancellationKeepsTheEarlierReport()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-build-cancel");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using var output = new CancelAfterCommittedLineWriter(cancellation);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        int exitCode = await CliApplication.RunAsync(
            CreateGeneralMergeBuildArguments(workspace, reportPath),
            output,
            error,
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(exitCode == 0, error + Environment.NewLine + output);
        AssertCommittedReportFailed(output.ToString(), error.ToString(), reportPath);
        AssertEarlierReportKept(reportPath);
    }

    /// <summary>A report write that fails after the commit keeps the earlier report, leaves no staging file and keeps exit code 0.</summary>
    [Fact]
    public async Task CommittedBuildReportFailureKeepsTheEarlierReport()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to replace a read-only file.");
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-cli-report-atomic-build-fail");
        string reportPath = workspace.Write("reports/report.json", EarlierReport);
        File.SetAttributes(reportPath, FileAttributes.ReadOnly);
        CliRunResult result;
        try
        {
            result = await CliTestHarness.RunAsync(
                CreateGeneralMergeBuildArguments(workspace, reportPath),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            File.SetAttributes(reportPath, FileAttributes.Normal);
        }

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        AssertCommittedReportFailed(result.Output, result.Error, reportPath);
        AssertEarlierReportKept(reportPath);
    }

    private static string WriteHalf(Stream stream, ReadOnlyMemory<byte> content)
    {
        stream.Write(content.Span[..(content.Length / 2)]);
        return Assert.IsType<FileStream>(stream).Name;
    }

    private static string[] CreateGeneralMergeBuildArguments(TempWorkspace workspace, string reportPath)
    {
        return
        [
            "general-merge", "build", "--profile", "NT51950", "--size", "0x4",
            "--mapping", $"0x0+0x1+0x2={workspace.Write("source.bin", [0x10, 0x11])}",
            "--output", workspace.PathFor("committed.bin"),
            "--report", reportPath,
        ];
    }

    private static void AssertCommittedReportFailed(string output, string error, string reportPath)
    {
        Assert.Contains("SHA256: ", output, StringComparison.Ordinal);
        Assert.Contains(
            $"  {CliCompositionRunSupport.CommittedReportFailedIssueCode}: Partial success: ",
            error,
            StringComparison.Ordinal);
        Assert.Contains($"'{reportPath}'", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Report:", output, StringComparison.Ordinal);
    }

    private static void AssertEarlierReportKept(
        string reportPath,
        StringWriter? output = null,
        string? writtenPath = null)
    {
        Assert.Equal(EarlierReport, File.ReadAllBytes(reportPath));
        string reportDirectory = Path.GetDirectoryName(reportPath)!;
        Assert.Equal([reportPath], Directory.GetFiles(reportDirectory));
        if (output is not null)
        {
            Assert.Empty(output.ToString());
        }

        if (writtenPath is not null)
        {
            Assert.NotEqual(reportPath, writtenPath);
            Assert.Equal(reportDirectory, Path.GetDirectoryName(writtenPath));
        }
    }

    /// <summary>Requests cancellation once the committed receipt line has been printed.</summary>
    private sealed class CancelAfterCommittedLineWriter(CancellationTokenSource cancellation)
        : StringWriter(CultureInfo.InvariantCulture)
    {
        public override async Task WriteLineAsync(string? value)
        {
            await base.WriteLineAsync(value).ConfigureAwait(false);
            if (value?.StartsWith("Committed: ", StringComparison.Ordinal) == true)
            {
                await cancellation.CancelAsync().ConfigureAwait(false);
            }
        }
    }
}
