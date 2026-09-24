using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.GoldenRegression.Tests;

internal static class GoldenTestHost
{
    internal static CompositionHostServices Services { get; } =
        CompositionHostServices.Create(
            NvtFwCombiner.Infrastructure.Capabilities.BuiltInCanonicalCapabilityPolicy.Load);

    internal static async ValueTask<CompositionRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        CompiledAuthoringSelectedInput[] inputs =
        [
            .. slotPaths.Select(static pair => new CompiledAuthoringSelectedInput(
                pair.Key,
                pair.Value,
                File.ReadAllBytes(pair.Value))),
        ];
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        CompiledAuthoringSessionPreparation prepared =
            Services.StandardMergeAuthoring.PrepareSession(
                session,
                icId,
                inputs);
        Assert.True(
            prepared.Succeeded,
            string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        return await Services.CompositionExecution
            .ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    prepared.Snapshot!,
                    slotPaths,
                    build,
                    outputPath: outputPath),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

}
