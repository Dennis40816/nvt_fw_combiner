using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateBuiltInV2DpReplaceRunContext(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        out BuiltInV2DpReplaceRunContext? context,
        out WorkbenchRunResult? failure)
    {
        context = null;
        failure = null;
        if (!slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceBase, out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.InputMissing,
                $"Reference FlashCode is required before {FormatBuiltInV2DpReplaceIcIds()} DP Replace can determine the output capacity.");
            return false;
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Reference FlashCode path does not exist.");
            return false;
        }

        DpReplacePartSelection selectedParts = DpReplacePartSelection.None;
        foreach (WorkbenchReplaceInputSlot slot in GetDpReplaceInputSlots(icId))
        {
            bool supplied = slotPaths.TryGetValue(slot.SlotId, out string? inputPath);
            if (!supplied)
            {
                if (slot.IsOptional)
                {
                    continue;
                }

                failure = CreatePlanningRunResult(
                    icId,
                    WorkbenchReplaceModes.Dp,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.InputMissing,
                    $"{slot.Title} is required for {icId} DP Replace.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                failure = CreatePlanningRunResult(
                    icId,
                    WorkbenchReplaceModes.Dp,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.InputMissing,
                    $"Selected {slot.Title} path is empty.");
                return false;
            }

            selectedParts |= slot.AddressSpaceId switch
            {
                CompositionAddressSpaceIds.DpReplacement =>
                    DpReplacePartSelection.InitialCode,
                CompositionAddressSpaceIds.LdReplacement =>
                    DpReplacePartSelection.Ldc,
                _ => throw new InvalidOperationException(
                    $"DP Replace slot '{slot.SlotId}' has unsupported address space '{slot.AddressSpaceId}'."),
            };
        }

        if (string.Equals(IcSupportCatalog.NormalizeIcId(icId), Nt51928DpReplaceIcId, StringComparison.Ordinal) &&
            selectedParts == DpReplacePartSelection.None)
        {
            failure = CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.ReplaceDpSelectionRequired,
                "NT51928 DP Replace requires at least one replacement: Initial Code or LDC.");
            return false;
        }

        long baseLength = new FileInfo(fullBasePath).Length;
        context = new BuiltInV2DpReplaceRunContext(
            ToIcNumberSelection(number),
            fullBasePath,
            baseLength,
            selectedParts,
            slotPaths);
        return true;
    }
}
