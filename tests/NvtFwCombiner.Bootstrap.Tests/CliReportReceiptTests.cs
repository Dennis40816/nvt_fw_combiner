using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI regressions proving that a requested report never hides a committed Build receipt.</summary>
public sealed class CliReportReceiptTests
{
    /// <summary>Every CLI route that can commit one composition output.</summary>
    public static TheoryData<string> BuildCommands =>
    [
        "standard-merge",
        "ab-merge",
        "general-merge",
        "ctrlram-replace",
        "general-replace",
    ];

    /// <summary>A successful committed Build writes the same report after its complete receipt.</summary>
    [Theory]
    [MemberData(nameof(BuildCommands))]
    public async Task CommittedBuildWritesReportAfterReceipt(string command)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-receipt");
        string outputPath = workspace.PathFor("committed.bin");
        string reportPath = workspace.PathFor("committed-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [.. CreateArguments(command, "build", workspace), "--output", outputPath, "--report", reportPath],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        string sha256 = await AssertCommittedReceiptAsync(result.Output, outputPath);
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken));
        Assert.Equal(sha256, report.RootElement.GetProperty("Output").GetProperty("Sha256").GetString());
        int committedLine = result.Output.IndexOf($"Committed: {outputPath}", StringComparison.Ordinal);
        int reportLine = result.Output.IndexOf($"Report: {reportPath}", StringComparison.Ordinal);
        Assert.True(committedLine >= 0 && reportLine > committedLine, result.Output);
        Assert.DoesNotContain(
            CliCompositionRunSupport.CommittedReportFailedIssueCode,
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>A report that cannot be written after commit leaves the committed receipt and reports a partial success.</summary>
    [Theory]
    [MemberData(nameof(BuildCommands))]
    public async Task CommittedBuildReceiptSurvivesReportWriteFailure(string command)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-receipt-io");
        string outputPath = workspace.PathFor("committed.bin");
        string blockingFile = workspace.Write("not-a-directory", [0x42]);
        string reportPath = Path.Combine(blockingFile, "report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [.. CreateArguments(command, "build", workspace), "--output", outputPath, "--report", reportPath],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        _ = await AssertCommittedReceiptAsync(result.Output, outputPath);
        AssertReportFailedAfterCommit(result.Output, result.Error, reportPath);
        Assert.False(File.Exists(reportPath));
        Assert.Equal(
            [0x42],
            await File.ReadAllBytesAsync(blockingFile, TestContext.Current.CancellationToken));
    }

    /// <summary>Cancellation after commit stops only the report; the committed receipt is still printed.</summary>
    [Theory]
    [MemberData(nameof(BuildCommands))]
    public async Task CommittedBuildReceiptSurvivesCancellationBeforeReport(string command)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-receipt-cancel");
        string outputPath = workspace.PathFor("committed.bin");
        string reportPath = workspace.PathFor("canceled-report.json");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using var output = new CancelAfterCommittedLineWriter(cancellation);
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(
            [.. CreateArguments(command, "build", workspace), "--output", outputPath, "--report", reportPath],
            output,
            error,
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(exitCode == 0, error + Environment.NewLine + output);
        _ = await AssertCommittedReceiptAsync(output.ToString(), outputPath);
        AssertReportFailedAfterCommit(output.ToString(), error.ToString(), reportPath);
        Assert.False(File.Exists(reportPath));
    }

    /// <summary>An atomic bundle Build prints its bundle receipt before a failing report.</summary>
    [Fact]
    public async Task CommittedBundleReceiptSurvivesReportWriteFailure()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-receipt-bundle");
        string bundleParent = workspace.PathFor("bundles");
        _ = Directory.CreateDirectory(bundleParent);
        string blockingFile = workspace.Write("not-a-directory", [0x42]);
        string reportPath = Path.Combine(blockingFile, "report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                .. CreateArguments("standard-merge", "build", workspace),
                "--bundle-parent",
                bundleParent,
                "--bundle-name",
                "receipt_bundle",
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        string bundleDirectory = Path.Combine(bundleParent, "receipt_bundle");
        Assert.True(Directory.Exists(bundleDirectory));
        Assert.Contains("Committed: ", result.Output, StringComparison.Ordinal);
        Assert.Contains($"Bundle: {bundleDirectory}", result.Output, StringComparison.Ordinal);
        Assert.Contains("Bundle artifacts:", result.Output, StringComparison.Ordinal);
        AssertReportFailedAfterCommit(result.Output, result.Error, reportPath);
    }

    /// <summary>Without a committed output the report still precedes the result and its failure keeps the existing software-error exit.</summary>
    [Fact]
    public async Task PreviewReportWriteFailureKeepsPreCommitBehavior()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-report-receipt-preview");
        string blockingFile = workspace.Write("not-a-directory", [0x42]);
        string reportPath = Path.Combine(blockingFile, "report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [.. CreateArguments("standard-merge", "preview", workspace), "--report", reportPath],
            TestContext.Current.CancellationToken);

        Assert.Equal(70, result.ExitCode);
        Assert.StartsWith("error: ", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Status:", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CliCompositionRunSupport.CommittedReportFailedIssueCode,
            result.Error,
            StringComparison.Ordinal);
    }

    private static string[] CreateArguments(string command, string action, TempWorkspace workspace)
    {
        return command switch
        {
            "standard-merge" =>
            [
                command, action, "--profile", "NT51923",
                "--dp", workspace.Write("dp.bin", CreateStandardMergeDp()),
                "--tp", workspace.Write("tp.bin", CreateStandardMergeTp()),
            ],
            "ab-merge" =>
            [
                command, action, "--profile", "NT51929",
                "--dp-ab", workspace.Write("dp-ab.bin", new byte[0x80000]),
                "--tp-a", workspace.Write("tp-a.bin", CreateAbTp()),
                "--tp-b", workspace.Write("tp-b.bin", CreateAbTp()),
            ],
            "general-merge" =>
            [
                command, action, "--profile", "NT51950", "--size", "0x4",
                "--mapping", $"0x0+0x1+0x2={workspace.Write("source.bin", [0x10, 0x11])}",
            ],
            "ctrlram-replace" => CreateCtrlRamArguments(action),
            "general-replace" =>
            [
                command, action, "--profile", "NT51926", "--ic-num", "single",
                "--base", workspace.Write(
                    "reference.bin",
                    File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output"))),
                "--mapping", $"0x3E020+0x2={workspace.Write("source.bin", [0xBE, 0xEF])}",
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown build command."),
        };
    }

    private static string[] CreateCtrlRamArguments(string action)
    {
        ReplaceCliCommandTests.Nt51926SelectiveVnRegression fixture =
            ReplaceCliCommandTests.LoadNt51926SelectiveVnRegression();
        return
        [
            "ctrlram-replace", action, "--profile", "NT51926", "--ic-num", "cascade",
            "--base", CanonicalGoldenTestData.ArtifactPath(fixture.BaseArtifact),
            "--ctrlram", $"replace-ctrlram-vn={CanonicalGoldenTestData.ArtifactPath(fixture.VnArtifact)}",
        ];
    }

    private static async Task<string> AssertCommittedReceiptAsync(string output, string outputPath)
    {
        byte[] committed = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(committed));
        Assert.NotEmpty(committed);
        Assert.Contains($"Output: {Path.GetFileName(outputPath)}", output, StringComparison.Ordinal);
        Assert.Contains(
            $"Size: {committed.LongLength.ToString(CultureInfo.InvariantCulture)} bytes",
            output,
            StringComparison.Ordinal);
        Assert.Contains($"SHA256: {sha256}", output, StringComparison.Ordinal);
        Assert.Contains($"Committed: {outputPath}", output, StringComparison.Ordinal);
        return sha256;
    }

    private static void AssertReportFailedAfterCommit(string output, string error, string reportPath)
    {
        Assert.Contains(
            $"  {CliCompositionRunSupport.CommittedReportFailedIssueCode}: Partial success: ",
            error,
            StringComparison.Ordinal);
        Assert.Contains($"'{reportPath}'", error, StringComparison.Ordinal);
        Assert.DoesNotContain("error:", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Report:", output, StringComparison.Ordinal);
    }

    private static byte[] CreateStandardMergeDp()
    {
        byte[] dp = new byte[0x40000];
        const int dpcmiStart = 0x3E000 + 20;
        dp[dpcmiStart + 1] = 0xB6;
        dp[dpcmiStart + 2] = 0xD4;
        return dp;
    }

    private static byte[] CreateStandardMergeTp()
    {
        byte[] tp = CreateAbTp(0x3C000);
        tp[17] = 0xC9;
        return tp;
    }

    private static byte[] CreateAbTp(int length = 0x40000)
    {
        byte[] tp = new byte[length];
        tp[0] = 0xA7;
        tp[1] = 0x58;
        tp[0x17] = 1;
        "\0NVT"u8.CopyTo(tp.AsSpan(0xFFC));
        return tp;
    }

    /// <summary>Requests cancellation once the committed receipt line has been printed.</summary>
    private sealed class CancelAfterCommittedLineWriter(CancellationTokenSource cancellation) : StringWriter(CultureInfo.InvariantCulture)
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
