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

        if (!TryCompileBuiltInV2DpReplace(
                icId,
                context!.Capacity,
                context.SelectedParts,
                out CompiledComposition? compiledComposition,
                out IReadOnlyList<CompositionIssue> issues))
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

        var slotsByAddressSpace =
            GetDpReplaceInputSlots(icId).ToDictionary(
                static slot => slot.AddressSpaceId,
                StringComparer.Ordinal);
        InputArtifactBinding[] bindings =
        [
            .. compiledComposition.Plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    addressSpaceId,
                    addressSpaceId == CompositionAddressSpaceIds.ReferenceBase
                        ? context.BasePath
                        : Path.GetFullPath(
                            context.SlotPaths[slotsByAddressSpace[addressSpaceId].SlotId]))),
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
            progress: progress).ConfigureAwait(false);
    }

    private static List<WorkbenchReplaceInputSlot> GetDpReplaceInputSlots(string icId)
    {
        bool nt51928OptionalParts = string.Equals(
            IcSupportCatalog.NormalizeIcId(icId),
            Nt51928DpReplaceIcId,
            StringComparison.Ordinal);
        List<WorkbenchReplaceInputSlot> slots =
        [
            new(
                WorkbenchSlotIds.ReplaceDp,
                nt51928OptionalParts ? "Initial Code replacement BIN" : "DP replacement BIN",
                TryGetV2DpReplaceInputDescription(icId, out string v2Description)
                    ? v2Description
                    : "Replacement DP payload. Build stays gated until this IC has approved DP Replace mapping evidence.",
                nt51928OptionalParts,
                CompositionAddressSpaceIds.DpReplacement,
                "dp"),
        ];
        foreach (DpReplaceAdditionalPayloadRule rule in DpReplaceAuthoringCatalog.GetAdditionalPayloads(icId))
        {
            slots.Add(new WorkbenchReplaceInputSlot(
                rule.SlotId,
                rule.Title,
                rule.Description,
                nt51928OptionalParts,
                rule.AddressSpaceId,
                rule.RegionId));
        }

        return slots;
    }

}
