using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static class CliCompositionRunSupport
{
    /// <summary>Issue printed when a requested report is not written after the Build output committed.</summary>
    internal const string CommittedReportFailedIssueCode = "cli.report.failed";

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

    internal static async Task WriteReportJsonAsync(
        string reportPath,
        string reportJson,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(reportPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, reportJson, cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync($"Report: {fullPath}").ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a run's requested report and prints its receipt in an order that cannot hide a committed
    /// output. Without a committed output the report is written first and a failure propagates as before.
    /// With one, the receipt is printed first; a later report failure or cancellation becomes one
    /// partial-success issue and keeps the run's exit code, as a failed loose delivery after the primary
    /// commit does.
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
        await WriteReportJsonAsync(
                reportPath,
                CompositionRunReportJson.Serialize(result),
                output,
                cancellationToken)
            .ConfigureAwait(false);
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
