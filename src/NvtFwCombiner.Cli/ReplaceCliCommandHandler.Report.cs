using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task PrintCompositionRunResultAsync(
        CompositionRunResult result,
        string icId,
        string experienceId,
        TextWriter output,
        TextWriter error)
    {
        await output.WriteLineAsync($"Status: {result.OutcomeStatus}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync($"Experience: {experienceId}").ConfigureAwait(false);
        bool isDiagnosticPlanOnly = StringComparer.Ordinal.Equals(
            result.OutcomeStatus,
            "DiagnosticPlanOnly");
        if (isDiagnosticPlanOnly)
        {
            await output.WriteLineAsync("Output: not produced").ConfigureAwait(false);
        }
        else
        {
            await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
            await output.WriteLineAsync($"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
            await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        }

        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (result.Report.DiagnosticPreview is { } diagnostic)
        {
            await output.WriteLineAsync(diagnostic.Message).ConfigureAwait(false);
        }

        if (result.Report.Mutations.Count > 0)
        {
            await output.WriteLineAsync("Mutations:").ConfigureAwait(false);
            foreach (MutationRunSummary mutation in result.Report.Mutations)
            {
                await output.WriteLineAsync(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"  {mutation.OperationId}: {mutation.TargetSpaceId} {FormatRange(mutation.TargetRange)} changed={mutation.ChangedByteCount}"))
                    .ConfigureAwait(false);
            }
        }

        if (result.Report.Issues.Count > 0)
        {
            await error.WriteLineAsync("Issues:").ConfigureAwait(false);
            foreach (CompositionIssue issue in result.Report.Issues)
            {
                await error.WriteLineAsync(
                        string.IsNullOrWhiteSpace(issue.OperationId)
                            ? $"  {issue.Code}: {issue.Message}"
                            : $"  {issue.Code} [{issue.OperationId}]: {issue.Message}")
                    .ConfigureAwait(false);
            }
        }
    }

    private static string FormatRange(ByteRange range)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})");
    }
}
