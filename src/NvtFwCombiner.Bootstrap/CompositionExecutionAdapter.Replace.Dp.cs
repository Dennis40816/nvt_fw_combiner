using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
{
    private static async ValueTask<WorkbenchRunResult> RunBuiltInV2DpReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        ResolvedCapability? acceptedCapability,
        ActiveSessionSnapshot? acceptedSession,
        CancellationToken cancellationToken)
    {
        if (!CompositionPlanningAdapter.TryCreateBuiltInV2DpReplaceRunContext(
                icId,
                number,
                slotPaths,
                build,
                out BuiltInV2DpReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        WorkbenchReplaceInputSlot[] replacementSlots = [.. DpReplaceInputSlotProjection.GetInputSlots(icId)];
        string[] selectedInputSlotIds = DpReplaceInputSlotProjection.GetSelectedCompiledSlotIds(
            replacementSlots,
            context!.SlotPaths);
        if (!CanonicalCapabilityProjection.TryResolveBuiltInV2DpReplaceInputSelection(
                icId,
                context!.Capacity,
                selectedInputSlotIds,
                acceptedSession?.AuthoringRevision ?? new AuthoringRevision(1),
                out InputSelectionReadinessSnapshot? selectionReadiness,
                out IReadOnlyList<CompositionIssue> selectionIssues))
        {
            CompositionIssue issue = selectionIssues.Count > 0
                ? selectionIssues[0]
                : new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 DP Replace input-selection contract for {icId} could not be resolved.");
            return CompositionPlanningAdapter.CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                issue.Code,
                issue.Message);
        }

        if (!selectionReadiness!.CanBuild)
        {
            InputSelectionReadinessIssue issue = selectionReadiness.PrimaryIssue!;
            return CompositionPlanningAdapter.CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                issue.Code,
                issue.Message);
        }

        ResolvedCapability? resolvedCapability = acceptedCapability;
        CompiledComposition? compiledComposition = acceptedCapability?.CompiledComposition;
        IReadOnlyList<CompositionIssue> issues = [];
        if (resolvedCapability is null && !CanonicalCapabilityResolution.TryCompileDpReplace(
                icId,
                context!.Capacity,
                selectedInputSlotIds,
                out compiledComposition,
                out resolvedCapability,
                out issues))
        {
            return CompositionPlanningAdapter.CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.ReplaceDpProfilePending,
                $"No supported V2 DP Replace profile is registered for {icId}.");
        }

        if (compiledComposition is null)
        {
            CompositionIssue issue = issues.Count > 0
                ? issues[0]
                : new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 DP Replace profile for {icId} did not produce an executable composition.");
            return CompositionPlanningAdapter.CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                issue.Code,
                issue.Message);
        }

        InputArtifactBinding[] bindings =
        [
            acceptedSession is null
                ? CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    CompositionAddressSpaceIds.ReferenceBase,
                    context.BasePath)
                : AcceptedAuthoringSessionBinding.Create(
                    compiledComposition,
                    CompositionAddressSpaceIds.ReferenceBase,
                    context.BasePath,
                    acceptedSession),
            .. replacementSlots
                .Where(slot => compiledComposition.Plan.RequiredInputAddressSpaceIds.Contains(
                    slot.AddressSpaceId,
                    StringComparer.Ordinal))
                .Select(slot =>
                acceptedSession is null
                    ? CompiledCompositionInputBindingFactory.Create(
                        compiledComposition,
                        slot.AddressSpaceId,
                        Path.GetFullPath(context.SlotPaths[slot.SlotId]))
                    : AcceptedAuthoringSessionBinding.Create(
                        compiledComposition,
                        slot.AddressSpaceId,
                        context.SlotPaths[slot.SlotId],
                        acceptedSession)),
        ];

        return await RunCompiledCompositionAsync(
            DpReplaceRunIdPrefix,
            compiledComposition,
            bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: context.Selection,
            cancellationToken,
            progress: progress,
            resolvedCapability: resolvedCapability).ConfigureAwait(false);
    }

}
