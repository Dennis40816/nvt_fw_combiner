using System.Globalization;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static async Task PrintRunResultAsync(
        CompositionRunResult result,
        TextWriter output,
        TextWriter error)
    {
        CompositionRunReport report = result.Report;
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {report.ProfileId} ({report.IcId})").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {report.Output.FileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {report.Output.Size.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {report.Output.Sha256}").ConfigureAwait(false);
        if (result.PreviewToken is not null)
        {
            await output.WriteLineAsync($"PreviewToken: {result.PreviewToken}").ConfigureAwait(false);
        }

        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (report.Mutations.Count > 0)
        {
            await output.WriteLineAsync("Mutations:").ConfigureAwait(false);
            foreach (MutationRunSummary mutation in report.Mutations)
            {
                await output.WriteLineAsync(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"  {mutation.OperationId}: {mutation.TargetSpaceId} {CliCompositionRunSupport.FormatRange(mutation.TargetRange)} changed={mutation.ChangedByteCount}"))
                    .ConfigureAwait(false);
            }
        }

        if (report.Issues.Count > 0)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, report.Issues).ConfigureAwait(false);
        }
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter error)
    {
        await error.WriteLineAsync($"error: unknown command '{command}'").ConfigureAwait(false);
        return UsageError;
    }
}
