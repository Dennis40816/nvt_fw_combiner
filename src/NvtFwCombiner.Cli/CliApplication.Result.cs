using System.Globalization;

namespace NvtFwCombiner.Cli;

public static partial class CliApplication
{
    private static async Task PrintRunResultAsync(
        CompositionRunResult result,
        string icId,
        TextWriter output,
        TextWriter error)
    {
        await output.WriteLineAsync($"Status: {result.OutcomeStatus}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
        await output.WriteLineAsync(
                $"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        if (result.PreviewToken is not null)
        {
            await output.WriteLineAsync($"PreviewToken: {result.PreviewToken}").ConfigureAwait(false);
        }
        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (result.Report.Mutations.Count > 0)
        {
            await output.WriteLineAsync("Mutations:").ConfigureAwait(false);
            foreach (MutationRunSummary mutation in result.Report.Mutations)
            {
                await output.WriteLineAsync(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"  {mutation.OperationId}: {mutation.TargetSpaceId} {CliCompositionRunSupport.FormatRange(mutation.TargetRange)} changed={mutation.ChangedByteCount}"))
                    .ConfigureAwait(false);
            }
        }

        if (result.Report.Issues.Count == 0)
        {
            return;
        }

        await error.WriteLineAsync("Issues:").ConfigureAwait(false);
        foreach (Domain.Composition.CompositionIssue issue in result.Report.Issues)
        {
            await error.WriteLineAsync(
                    string.IsNullOrWhiteSpace(issue.OperationId)
                        ? $"  {issue.Code}: {issue.Message}"
                        : $"  {issue.Code} [{issue.OperationId}]: {issue.Message}")
                .ConfigureAwait(false);
        }
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter error)
    {
        await error.WriteLineAsync($"error: unknown command '{command}'").ConfigureAwait(false);
        return UsageError;
    }
}
