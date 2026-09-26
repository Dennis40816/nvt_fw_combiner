using System.Globalization;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static class CliCompositionRunSupport
{
    /// <summary>Issue printed when a requested report is not written after the Build output committed.</summary>
    internal const string CommittedReportFailedIssueCode = "cli.report.failed";

    private static readonly UTF8Encoding ReportEncoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static CliOutputTarget ResolveOutputTarget(string? requestedOutput, string defaultFileName)
    {
        string outputPath = string.IsNullOrWhiteSpace(requestedOutput)
            ? Path.GetFullPath(defaultFileName)
            : Path.GetFullPath(requestedOutput);
        string? directory = Path.GetDirectoryName(outputPath);
        string fileName = Path.GetFileName(outputPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("Output must resolve to a file path.")
            : new CliOutputTarget(directory, fileName);
    }

    internal static void EnsureOutputDoesNotAliasInputs(
        CliOutputTarget outputTarget,
        IReadOnlyList<InputArtifactBinding> bindings)
    {
        ProtectedPathGuard.EnsureOutputDoesNotAliasInputs(
            outputTarget.FullPath,
            bindings,
            nameof(outputTarget));
    }

    internal static void EnsureReportDoesNotAliasProtectedPaths(
        string? reportPath,
        IReadOnlyList<InputArtifactBinding> bindings,
        CliOutputTarget outputTarget,
        bool protectOutput)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        ProtectedPathGuard.EnsureReportDoesNotAliasProtectedPaths(
            reportPath,
            bindings,
            protectOutput ? outputTarget.FullPath : null,
            "--report");
    }

    /// <summary>
    /// Writes one report so that its destination is replaced whole or not at all: the UTF-8 report is
    /// written and flushed to a new staging file in the destination directory and then renamed over the
    /// destination. A failure or cancellation before that rename leaves the destination's earlier bytes,
    /// or its absence, unchanged and deletes the staging file this write created (best effort: a failed
    /// deletion keeps the original failure as the one reported). A pre-existing file with the staging
    /// name is never deleted.
    /// </summary>
    internal static Task WriteReportJsonAsync(
        string reportPath,
        string reportJson,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        return WriteReportJsonAsync(
            reportPath,
            reportJson,
            output,
            $".nfc-report-{Guid.NewGuid():N}.tmp",
            static (staging, content, token) => staging.WriteAsync(content, token),
            cancellationToken);
    }

    /// <summary>
    /// Writes one report as described above through the staging file named
    /// <paramref name="stagingFileName"/> in the destination directory;
    /// <paramref name="writeStagingContent"/> writes the complete report bytes into the open staging file.
    /// The staging name must be a plain file name that does not name the report itself.
    /// </summary>
    internal static async Task WriteReportJsonAsync(
        string reportPath,
        string reportJson,
        TextWriter output,
        string stagingFileName,
        Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeStagingContent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingFileName);
        ArgumentNullException.ThrowIfNull(writeStagingContent);
        string fullPath = Path.GetFullPath(reportPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException("Report path must resolve to a file path.", nameof(reportPath));
        }

        string stagingPath = ResolveStagingPath(directory, fullPath, stagingFileName);
        byte[] content = ReportEncoding.GetBytes(reportJson);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Directory.CreateDirectory(directory);
        bool stagingCreated = false;
        try
        {
            await using (var staging = new FileStream(
                             stagingPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 0,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                // Only a staging file this write created is ever deleted; an existing file of that name is not ours.
                stagingCreated = true;
                await writeStagingContent(staging, content, cancellationToken).ConfigureAwait(false);
                await staging.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagingPath, fullPath, overwrite: true);
        }
        catch
        {
            if (stagingCreated)
            {
                DeleteStagingFile(stagingPath);
            }

            throw;
        }

        await output.WriteLineAsync($"Report: {fullPath}").ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the staging file as a new direct child of the report directory. A name that is not a plain
    /// file name, or that names the report itself after path normalization (for example trailing dots),
    /// case folding or Unicode normalization, would let the write skip the rename, so it is refused.
    /// </summary>
    private static string ResolveStagingPath(
        string directory,
        string reportFullPath,
        string stagingFileName)
    {
        string stagingPath = Path.GetFullPath(Path.Combine(directory, stagingFileName));
        return stagingFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            IsEquivalentPath(Path.GetDirectoryName(stagingPath), directory) &&
            !IsEquivalentPath(stagingPath, reportFullPath)
                ? stagingPath
                : throw new ArgumentException(
                    "The staging file must be a plain file name in the report directory that does not name the report.",
                    nameof(stagingFileName));
    }

    private static bool IsEquivalentPath(string? left, string right)
    {
        return left is not null &&
            string.Equals(
                NormalizeForComparison(left),
                NormalizeForComparison(right),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForComparison(string path)
    {
        try
        {
            return path.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // A path that is not valid Unicode is compared as written.
            return path;
        }
    }

    private static void DeleteStagingFile(string stagingPath)
    {
        try
        {
            File.Delete(stagingPath);
        }
        catch (IOException)
        {
            // The original report failure is the one reported; the destination is unchanged either way.
        }
        catch (UnauthorizedAccessException)
        {
            // The original report failure is the one reported; the destination is unchanged either way.
        }
    }

    /// <summary>
    /// Writes a run's requested report and prints its receipt in an order that cannot hide a committed
    /// output. Without a committed output the report is written first and a failure propagates as before.
    /// With one, the receipt is printed first and the report must not resolve to any file the run
    /// committed; a rejected, failed or cancelled report becomes one partial-success issue and keeps the
    /// run's exit code, as a failed loose delivery after the primary commit does.
    /// </summary>
    internal static async Task WriteReportJsonAsync(
        CompositionRunResult result,
        string? reportPath,
        Action<string>? ensureReportPathAllowed,
        Func<Task> printRunResultAsync,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(printRunResultAsync);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (result.CommittedOutputId is null)
        {
            if (reportPath is not null)
            {
                await WriteRunReportAsync(result, reportPath, ensureReportPathAllowed, output, cancellationToken)
                    .ConfigureAwait(false);
            }

            await PrintRunReceiptAsync(result, printRunResultAsync, output).ConfigureAwait(false);
            return;
        }

        await PrintRunReceiptAsync(result, printRunResultAsync, output).ConfigureAwait(false);
        if (reportPath is null)
        {
            return;
        }

        try
        {
            await WriteRunReportAsync(result, reportPath, ensureReportPathAllowed, output, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsReportFailureAfterCommit(exception))
        {
            await PrintIssuesAsync(
                    error,
                    [new CompositionIssue(
                        CommittedReportFailedIssueCode,
                        $"Partial success: the Build output is committed, but the requested report '{reportPath}' was not written: {exception.Message}")])
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteRunReportAsync(
        CompositionRunResult result,
        string reportPath,
        Action<string>? ensureReportPathAllowed,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ensureReportPathAllowed?.Invoke(reportPath);
        if (result.CommittedOutputId is { } committedOutputId)
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                reportPath,
                "Report path",
                CreateCommittedFilePaths(result, committedOutputId),
                "--report");
        }

        await WriteReportJsonAsync(
                reportPath,
                CompositionRunReportJson.Serialize(result),
                output,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Every file the run actually committed: the primary output, each artifact of the promoted bundle
    /// (the receipt's bundle list) and each committed additional delivery, loose or bundled.
    /// </summary>
    private static List<ProtectedPathGuard.ProtectedPath> CreateCommittedFilePaths(
        CompositionRunResult result,
        string committedOutputId)
    {
        List<ProtectedPathGuard.ProtectedPath> committedFiles =
        [
            new(committedOutputId, "committed firmware output"),
        ];
        if (result.Report.BundleDelivery is { } bundle)
        {
            committedFiles.AddRange(bundle.Artifacts.Select(artifact =>
                new ProtectedPathGuard.ProtectedPath(
                    Path.Combine(bundle.ResolvedDirectory, artifact.DeliveredFileName),
                    $"committed bundle {artifact.Role} artifact '{artifact.DeliveredFileName}'")));
        }

        committedFiles.AddRange(result.DeliveryArtifacts.Select(static artifact =>
            new ProtectedPathGuard.ProtectedPath(
                artifact.OutputPath,
                $"committed {artifact.DeliveryKind} delivery '{artifact.OutputFileName}'")));
        return committedFiles;
    }

    private static async Task PrintRunReceiptAsync(
        CompositionRunResult result,
        Func<Task> printRunResultAsync,
        TextWriter output)
    {
        await printRunResultAsync().ConfigureAwait(false);
        await CliBundleOptions.PrintReceiptAsync(result, output).ConfigureAwait(false);
    }

    private static bool IsReportFailureAfterCommit(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            JsonException or
            InvalidOperationException or
            FormatException or
            OverflowException or
            OperationCanceledException;
    }

    internal static async Task PrintIssuesAsync(
        TextWriter error,
        IReadOnlyList<CompositionIssue> issues)
    {
        await error.WriteLineAsync("Issues:").ConfigureAwait(false);
        foreach (CompositionIssue issue in issues)
        {
            string operation = issue.OperationId is null ? string.Empty : $" [{issue.OperationId}]";
            await error.WriteLineAsync($"  {issue.Code}{operation}: {issue.Message}").ConfigureAwait(false);
        }
    }

    internal static string GetIcNumber(string icId)
    {
        return icId.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? icId[2..]
            : icId;
    }

    internal static string FormatRange(ByteRange range)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})");
    }

}

internal readonly record struct CliOutputTarget(string OutputDirectory, string FileName)
{
    internal string FullPath => ProtectedPathGuard.CombineFullPath(OutputDirectory, FileName);
}
