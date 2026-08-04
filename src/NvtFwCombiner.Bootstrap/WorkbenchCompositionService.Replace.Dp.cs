using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
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
        if (!TryCreateBuiltInV2DpReplaceRunContext(
                icId,
                number,
                slotPaths,
                build,
                out BuiltInV2DpReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        WorkbenchReplaceInputSlot[] replacementSlots = [.. GetDpReplaceInputSlots(icId)];
        string[] selectedInputSlotIds =
        [
            .. replacementSlots
                .Where(slot => context!.SlotPaths.TryGetValue(slot.SlotId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path) &&
                    IsDpReplaceSelectionGroupMember(icId, slot.AddressSpaceId))
                .Select(static slot => slot.AddressSpaceId),
        ];
        if (!TryResolveBuiltInV2DpReplaceInputSelection(
                icId,
                context!.Capacity,
                selectedInputSlotIds,
                out InputSelectionReadinessSnapshot? selectionReadiness,
                out IReadOnlyList<CompositionIssue> selectionIssues))
        {
            CompositionIssue issue = selectionIssues.Count > 0
                ? selectionIssues[0]
                : new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 DP Replace input-selection contract for {icId} could not be resolved.");
            return CreatePlanningRunResult(
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
            return CreatePlanningRunResult(
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
        if (resolvedCapability is null && !TryCompileBuiltInV2DpReplace(
                icId,
                context!.Capacity,
                selectedInputSlotIds,
                out compiledComposition,
                out resolvedCapability,
                out issues))
        {
            return CreatePlanningRunResult(
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
            return CreatePlanningRunResult(
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
                : CreateAcceptedSessionBinding(
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
                    : CreateAcceptedSessionBinding(
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

    private static List<WorkbenchReplaceInputSlot> GetDpReplaceInputSlots(string icId)
    {
        string primaryReplacementAddressSpaceId =
            IsDpReplaceSelectionGroupMember(icId, CompositionAddressSpaceIds.InitialCodeReplacement)
                ? CompositionAddressSpaceIds.InitialCodeReplacement
                : CompositionAddressSpaceIds.DpReplacement;
        List<WorkbenchReplaceInputSlot> slots =
        [
            new(
                WorkbenchSlotIds.ReplaceDp,
                primaryReplacementAddressSpaceId == CompositionAddressSpaceIds.InitialCodeReplacement
                    ? "Initial Code replacement BIN"
                    : "DP replacement BIN",
                TryGetV2DpReplaceInputDescription(icId, out string v2Description)
                    ? v2Description
                    : "Replacement DP payload. Build stays gated until this IC has approved DP Replace mapping evidence.",
                IsDpReplaceSelectionGroupMember(icId, primaryReplacementAddressSpaceId),
                primaryReplacementAddressSpaceId,
                "dp",
                InputRole: WorkbenchReplaceInputRole.Dp),
        ];
        foreach (DpReplaceAdditionalPayloadRule rule in DpReplaceAuthoringCatalog.GetAdditionalPayloads(icId))
        {
            slots.Add(new WorkbenchReplaceInputSlot(
                rule.SlotId,
                rule.Title,
                rule.Description,
                IsDpReplaceSelectionGroupMember(icId, rule.AddressSpaceId),
                rule.AddressSpaceId,
                rule.RegionId,
                InputRole: WorkbenchReplaceInputRole.Dp));
        }

        return slots;
    }

}
