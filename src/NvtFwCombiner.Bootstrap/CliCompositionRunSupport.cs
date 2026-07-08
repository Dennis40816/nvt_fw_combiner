using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static class CliCompositionRunSupport
{
    internal static CompositionRunProfile ToRunProfile(CompositionProfileDefinition profile)
    {
        return new CompositionRunProfile(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind,
            profile.IcNumberInputMode);
    }

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

    internal static async Task WriteReportFileIfRequestedAsync(
        CompositionRunResult result,
        string? reportPath,
        IReadOnlyList<InputArtifactBinding> bindings,
        CliOutputTarget outputTarget,
        bool protectOutput,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        string fullPath = await CliRunReportWriter
            .WriteAsync(
                result.Report,
                reportPath,
                ProtectedPathGuard.CreateProtectedPaths(
                    bindings,
                    protectOutput ? outputTarget.FullPath : null),
                cancellationToken)
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Report: {fullPath}").ConfigureAwait(false);
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
