using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class AbMergeTestSupport
{
    internal static CompiledAuthoringSessionPreparation Prepare(
        CompositionHostServices host,
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        TopologySelection? topologySelection = null)
    {
        string? topologyToken = ResolveTopologyToken(host, icId, topologySelection);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        return host.AbMergeAuthoring.PrepareSession(
            session,
            icId,
            topologyToken,
            [
                .. slotPaths.Select(static pair => new CompiledAuthoringSelectedInput(
                    pair.Key,
                    pair.Value,
                    File.ReadAllBytes(pair.Value))),
            ]);
    }

    internal static async ValueTask<CompositionOutputPreparation> PrepareOutputAsync(
        CompositionHostServices host,
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        TopologySelection? topologySelection = null)
    {
        CompiledAuthoringSessionPreparation prepared = Prepare(
            host,
            icId,
            slotPaths,
            topologySelection);
        return prepared.Succeeded
            ? await host.CompositionOutputNaming.PrepareAutomaticOutputAsync(
                    prepared.Snapshot!,
                    cancellationToken)
                .ConfigureAwait(false)
            : throw new InvalidOperationException(CompositionExecutionTestSupport.FormatIssues(
                prepared.Issues));
    }

    internal static async ValueTask<CompositionRunResult> RunAsync(
        CompositionHostServices host,
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        TopologySelection? topologySelection = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false,
        CompositionRunProgressFeed? progress = null,
        string? previewOutputFileName = null,
        string? automaticOutputDirectory = null,
        string? reportPath = null)
    {
        CompiledAuthoringSessionPreparation prepared = Prepare(
            host,
            icId,
            slotPaths,
            topologySelection);
        if (!prepared.Succeeded)
        {
            throw new InvalidOperationException(CompositionExecutionTestSupport.FormatIssues(
                prepared.Issues.Count != 0
                    ? prepared.Issues
                    : [new CompositionIssue(
                        prepared.SessionIssue?.Code ?? AuthoringSessionIssueCodes.StaleInspection,
                        prepared.SessionIssue?.Message ??
                            "AB Merge preparation did not produce one accepted session.")]));
        }

        CapabilityActionReadinessSnapshot? readiness =
            await host.AbMergeAuthoring.GetActionReadinessAsync(
                prepared.Snapshot!,
                cancellationToken).ConfigureAwait(false);
        return await host.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    prepared.Snapshot!,
                    slotPaths,
                    build,
                    outputPath: outputPath,
                    previewOutputFileName: previewOutputFileName,
                    additionalDeliveryOutputPath: aFlashCodeOutputPath,
                    outputPathUsesAutomaticName: outputPathUsesAutomaticName,
                    additionalDeliveryOutputPathUsesAutomaticName:
                        aFlashCodeOutputPathUsesAutomaticName,
                    automaticOutputDirectory: automaticOutputDirectory,
                    reportPath: reportPath,
                    actionReadiness: readiness),
                progress ?? new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? ResolveTopologyToken(
        CompositionHostServices host,
        string icId,
        TopologySelection? selection)
    {
        return selection is null
            ? null
            : host.AbMergeAuthoring.GetTopologyChoices(icId)
                .Single(choice => Equals(choice.Selection, selection))
                .Token;
    }
}
