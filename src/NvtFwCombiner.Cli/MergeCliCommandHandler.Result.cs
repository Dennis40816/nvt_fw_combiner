using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class MergeCliCommandHandler
{
    private static async Task PrintResultAsync(
        CompositionRunResult result,
        string icId,
        TextWriter output,
        TextWriter error,
        bool reportWritten)
    {
        await output.WriteLineAsync($"Status: {result.OutcomeStatus}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync("Experience: general-merge").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        if (result.PreviewToken is not null)
        {
            await output.WriteLineAsync($"PreviewToken: {result.PreviewToken}").ConfigureAwait(false);
        }

        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (result.Succeeded)
        {
            return;
        }

        if (reportWritten)
        {
            await error.WriteLineAsync("General Merge failed; inspect the JSON report for issues.").ConfigureAwait(false);
            return;
        }

        await error.WriteLineAsync("General Merge failed; no JSON report was written. Issues:").ConfigureAwait(false);
        await PrintReportIssuesAsync(result.Report.Issues, error).ConfigureAwait(false);
    }

    private static async Task PrintReportIssuesAsync(
        IReadOnlyList<CompositionIssue> issues,
        TextWriter error)
    {
        if (issues.Count == 0)
        {
            await error.WriteLineAsync("  - Unknown issue: no issue rows were recorded.").ConfigureAwait(false);
            return;
        }

        foreach (CompositionIssue issue in issues)
        {
            string source = string.IsNullOrWhiteSpace(issue.OperationId)
                ? ExperienceIds.GeneralMerge
                : issue.OperationId;
            await error.WriteLineAsync($"  - {issue.Code} [{source}]: {issue.Message}").ConfigureAwait(false);
        }
    }

    private static async Task PrintPreparationIssuesAsync(
        IReadOnlyList<CompositionIssue> issues,
        TextWriter error)
    {
        await error.WriteLineAsync(
            "General Merge failed; no JSON report was written. Issues:")
            .ConfigureAwait(false);
        await PrintReportIssuesAsync(issues, error).ConfigureAwait(false);
    }

}
