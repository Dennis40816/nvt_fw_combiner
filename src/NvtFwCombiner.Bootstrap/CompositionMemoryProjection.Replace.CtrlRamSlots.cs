using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionMemoryProjection
{
    private static IReadOnlyList<WorkbenchReplaceInputSlot> GetCtrlRamReplaceInputSlots(
        string icId,
        string number,
        string? basePath)
    {
        _ = FirmwareInspectionAdapter.TryResolvePostbuildProfileFromBasePathForDisplay(icId, basePath, out LegacyCombinerPostbuildProfile? postbuildProfile);
        return CreateCtrlRamReplaceInputSlots(
            icId,
            number,
            postbuildProfile,
            !string.IsNullOrWhiteSpace(basePath) && File.Exists(basePath));
    }

    internal static IReadOnlyList<WorkbenchReplaceInputSlot> CreateCtrlRamReplaceInputSlots(
        string icId,
        string number,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        bool hasReadableBase)
    {
        IcNumberSelection selection = WorkbenchIcNumberSelections.FromNumberToken(number);
        LegacyCombinerPostbuildCommandPlan? commandPlan = postbuildProfile is null
            ? null
            : LegacyCombinerPostbuildPlanner.CreatePlan(
                postbuildProfile,
                selection);
        return postbuildProfile is null && hasReadableBase
            ? []
            : [
            .. BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(
                icId,
                selection,
                postbuildProfile)
                    .Select(source => CreateCtrlRamReplaceInputSlot(commandPlan, selection, source)),
            ];
    }

    private static WorkbenchReplaceInputSlot CreateCtrlRamReplaceInputSlot(
        LegacyCombinerPostbuildCommandPlan? commandPlan,
        IcNumberSelection selection,
        TpCtrlRamPostbuildSource source)
    {
        LegacyCombinerDiffDlmPolicy? diffDlmPolicy =
            commandPlan?.Branch == LegacyCombinerPostbuildBranch.Cascade
                ? commandPlan.Profile.DiffDlmPolicy
                : null;
        bool requiresDiffNfMerge = diffDlmPolicy is not null &&
            StringComparer.Ordinal.Equals(
                source.SourceFileName,
                diffDlmPolicy.IndependentNfSourceFileName);
        bool isDiffDlm = source.ArtifactRole == TpCtrlRamPostbuildArtifactRole.DiffDlm;
        string title = isDiffDlm
            ? "DiffDLM"
            : source.Regions.Count == 1
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
            requiresDiffNfMerge ? $"{title} (DiffNFMerge output)" : title,
            requiresDiffNfMerge
                ? $"{description} · Cascade requires a DiffNFMerge-prebuilt NF_Ctrlram.bin; generation is not integrated."
                : description,
            true,
            slotId,
            source.SourceId,
            slotId,
            SelectionGroupId: null,
            RegionGroup: GetCtrlRamRegionGroup(source, commandPlan?.Branch, selection),
            InputRole: WorkbenchReplaceInputRole.CtrlRam);
    }

    internal static string CtrlRamSlotId(string regionId)
    {
        return WorkbenchSlotIds.CreateReplaceCtrlRam(regionId);
    }

    private static WorkbenchReplaceRegionGroup GetCtrlRamRegionGroup(
        TpCtrlRamPostbuildSource source,
        LegacyCombinerPostbuildBranch? branch,
        IcNumberSelection selection)
    {
        TpFlashMapRegionVisibility? visibility = source.Regions.Count == 1
            ? source.Regions[0].Visibility
            : null;
        int topologyCount = selection.Mode == IcNumberInputMode.NumericSelector &&
            int.TryParse(selection.Parts.Single(), out int selectedCount)
                ? selectedCount
                : 1;
        return (source.ArtifactRole, branch, topologyCount, source.Regions.Count, visibility) switch
        {
            (TpCtrlRamPostbuildArtifactRole.DiffDlm, _, _, _, _) =>
                WorkbenchReplaceRegionGroup.Cascade,
            (_, LegacyCombinerPostbuildBranch.Cascade, _, _, _) =>
                WorkbenchReplaceRegionGroup.Common,
            (_, _, >= 2, 1,
                TpFlashMapRegionVisibility.Always) =>
                WorkbenchReplaceRegionGroup.Master,
            (_, _, >= 2, 1,
                TpFlashMapRegionVisibility.TwoChipAndAbove) =>
                WorkbenchReplaceRegionGroup.SlaveRight,
            (_, _, >= 3, 1,
                TpFlashMapRegionVisibility.ThreeChipAndAbove) =>
                WorkbenchReplaceRegionGroup.SlaveLeft,
            _ => WorkbenchReplaceRegionGroup.Common,
        };
    }

}
