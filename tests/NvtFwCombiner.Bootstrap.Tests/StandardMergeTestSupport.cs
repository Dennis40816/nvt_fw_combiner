using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class StandardMergeTestSupport
{
    internal static async ValueTask<CompositionRunResult> RunAsync(
        CompositionHostServices host,
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        CompositionRunProgressFeed? progress = null)
    {
        List<CompiledAuthoringSelectedInput> inputs = [];
        foreach ((string slotId, string path) in slotPaths)
        {
            try
            {
                inputs.Add(new CompiledAuthoringSelectedInput(
                    slotId,
                    path,
                    File.ReadAllBytes(path)));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                string message = StringComparer.Ordinal.Equals(
                        slotId,
                        CompositionAddressSpaceIds.DpInput) &&
                    !File.Exists(path)
                        ? $"Selected DP BIN path does not exist for {icId} Standard Merge."
                        : exception.Message;
                throw new InvalidOperationException(
                    $"{CompositionPlanningIssueCodes.InputArtifactReadFailed}: {message}",
                    exception);
            }
        }

        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        CompiledAuthoringSessionPreparation prepared =
            host.StandardMergeAuthoring.PrepareSession(
                session,
                icId,
                inputs);
        return prepared.Succeeded
            ? await host.CompositionExecution
                .ExecuteAsync(
                    new AcceptedCompositionExecutionRequest(
                        prepared.Snapshot!,
                        slotPaths,
                        build,
                        outputPath: outputPath),
                    progress ?? new CompositionRunProgressFeed(),
                    cancellationToken)
                .ConfigureAwait(false)
            : throw new InvalidOperationException(CompositionExecutionTestSupport.FormatIssues(
                prepared.Issues.Count != 0
                    ? prepared.Issues
                    : [new CompositionIssue(
                        prepared.SessionIssue?.Code ?? AuthoringSessionIssueCodes.StaleInspection,
                        prepared.SessionIssue?.Message ??
                            "Standard Merge preparation did not produce one accepted session.")]));
    }
}
