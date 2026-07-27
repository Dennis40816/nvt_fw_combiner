using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static IReadOnlyList<WorkbenchReplaceInputSlot> GetCtrlRamReplaceInputSlots(
        string icId,
        string number,
        string? basePath)
    {
        _ = TryResolvePostbuildProfileFromBasePathForDisplay(icId, basePath, out LegacyCombinerPostbuildProfile? postbuildProfile);
        return CreateCtrlRamReplaceInputSlots(
            icId,
            number,
            postbuildProfile,
            !string.IsNullOrWhiteSpace(basePath) && File.Exists(basePath));
    }

    private static IReadOnlyList<WorkbenchReplaceInputSlot> CreateCtrlRamReplaceInputSlots(
        string icId,
        string number,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        bool hasReadableBase)
    {
        return postbuildProfile is null && hasReadableBase
            ? []
            : [
            .. GetUserSelectableCtrlRamSources(icId, ToIcNumberSelection(number), postbuildProfile)
                    .Select(CreateCtrlRamReplaceInputSlot),
            ];
    }

    private static IReadOnlyList<TpCtrlRamPostbuildSource> GetUserSelectableCtrlRamSources(
        string icId,
        IcNumberSelection selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        LegacyCombinerPostbuildProfile? effectiveProfile = postbuildProfile;
        if (effectiveProfile is null)
        {
            _ = TryGetDefaultPostbuildProfile(icId, out effectiveProfile);
        }

        IReadOnlyList<TpCtrlRamPostbuildSource> sources =
            BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(icId, selection, postbuildProfile);
        return effectiveProfile is null ||
            !DiffDlmNfMaskPolicy.TryResolve(
                icId,
                LegacyCombinerPostbuildPlanner.CreatePlan(effectiveProfile, selection).Branch,
                out _)
            ? sources
            : [
                .. sources.Where(
                    static source => !DiffDlmNfMaskPolicy.IsIndependentNfSource(source.SourceFileName)),
            ];
    }

    private static WorkbenchReplaceInputSlot CreateCtrlRamReplaceInputSlot(TpCtrlRamPostbuildSource source)
    {
        string title = source.Regions.Count == 1
            ? source.Regions[0].DisplayName
            : $"{DynamicCtrlRamReplacementIds.FormatRegionDisplayLabel(source.SourceId)} (Shared)";
        string sections = string.Join("; ", source.Blocks
            .DistinctBy(block => (block.SourceOffset, block.FirmwareRange))
            .OrderBy(block => block.FirmwareRange.Start)
            .Select(block =>
            {
                TpFlashMapRegion region = source.Regions.Single(region => region.Range.Overlaps(block.FirmwareRange));
                return $"{region.DisplayName}: max {block.FirmwareRange.Length} B → 0x{block.FirmwareRange.Start:X}";
            }));
        string description = $"{source.SourceFileName} · {sections}";
        string slotId = CtrlRamSlotId(source.SourceId);
        return new WorkbenchReplaceInputSlot(
            slotId,
            title,
            description,
            true,
            slotId,
            source.SourceId);
    }

    private static string CtrlRamSlotId(string regionId)
    {
        return WorkbenchSlotIds.CreateReplaceCtrlRam(regionId);
    }

}
