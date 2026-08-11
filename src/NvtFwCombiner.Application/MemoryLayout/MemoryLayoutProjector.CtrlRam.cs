using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
    /// <summary>
    /// Projects one already-resolved CtrlRAM discovery contract into the same typed
    /// regions and input slots consumed by memory presentation and authoring clients.
    /// </summary>
    public static CtrlRamInspectionDisplay ProjectCtrlRamDiscovery(
        string numberToken,
        LegacyCombinerPostbuildCommandPlan? commandPlan,
        IEnumerable<TpFlashMapRegion> regions,
        IEnumerable<TpCtrlRamPostbuildSource> sources,
        bool hasReadableBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numberToken);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(sources);
        var selection = IcNumberSelection.FromToken(numberToken);
        LegacyCombinerPostbuildBranch? branch = commandPlan?.Branch;
        CtrlRamRegion[] projectedRegions =
        [
            .. regions.Select(region => new CtrlRamRegion(
                region.RegionId,
                region.DisplayName,
                region.Range.Start,
                region.Range.Length,
                region.Tags.Any(static tag =>
                    StringComparer.OrdinalIgnoreCase.Equals(tag, "diff") ||
                    StringComparer.OrdinalIgnoreCase.Equals(tag, "dlm") ||
                    StringComparer.OrdinalIgnoreCase.Equals(tag, "slave")),
                ResolveCtrlRamRegionGroup(region, branch, selection))),
        ];
        ReplaceInputSlot[] inputSlots = commandPlan is null && hasReadableBase
            ? []
            : [.. sources.Select(source => CreateCtrlRamInputSlot(commandPlan, selection, source))];
        return new CtrlRamInspectionDisplay(
            numberToken,
            Array.AsReadOnly(projectedRegions),
            Array.AsReadOnly(inputSlots));
    }

    private static ReplaceInputSlot CreateCtrlRamInputSlot(
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
            .OrderBy(static block => block.FirmwareRange.Start)
            .Select(block =>
            {
                TpFlashMapRegion region = source.Regions.Single(region =>
                    region.Range.Overlaps(block.FirmwareRange));
                return $"{region.DisplayName}: max {block.FirmwareRange.Length} B → 0x{block.FirmwareRange.Start:X}";
            }));
        string description = $"{source.SourceFileName} · {sections}";
        string slotId = DynamicCtrlRamReplacementIds.Create(source.SourceId);
        return new ReplaceInputSlot(
            slotId,
            requiresDiffNfMerge ? $"{title} (DiffNFMerge output)" : title,
            requiresDiffNfMerge
                ? $"{description} · Cascade requires a DiffNFMerge-prebuilt NF_Ctrlram.bin; generation is not integrated."
                : description,
            IsOptional: true,
            slotId,
            source.SourceId,
            slotId,
            SelectionGroupId: null,
            RegionGroup: ResolveCtrlRamSourceGroup(source, commandPlan?.Branch, selection),
            InputRole: ReplaceInputRole.CtrlRam);
    }

    private static ReplaceRegionGroup ResolveCtrlRamSourceGroup(
        TpCtrlRamPostbuildSource source,
        LegacyCombinerPostbuildBranch? branch,
        IcNumberSelection selection)
    {
        return source.ArtifactRole == TpCtrlRamPostbuildArtifactRole.DiffDlm
            ? ReplaceRegionGroup.Cascade
            : source.Regions.Count == 1
                ? ResolveCtrlRamRegionGroup(source.Regions[0], branch, selection)
                : ReplaceRegionGroup.Common;
    }

    private static ReplaceRegionGroup ResolveCtrlRamRegionGroup(
        TpFlashMapRegion region,
        LegacyCombinerPostbuildBranch? branch,
        IcNumberSelection selection)
    {
        bool isDiffDlm = region.Tags.Any(static tag =>
            StringComparer.OrdinalIgnoreCase.Equals(tag, "diff") ||
            StringComparer.OrdinalIgnoreCase.Equals(tag, "dlm"));
        int topologyCount = selection.Mode == IcNumberInputMode.NumericSelector &&
            int.TryParse(selection.Parts.Single(), out int selectedCount)
                ? selectedCount
                : 1;
        return (isDiffDlm, branch, topologyCount, region.Visibility) switch
        {
            (true, _, _, _) => ReplaceRegionGroup.Cascade,
            (_, LegacyCombinerPostbuildBranch.Cascade, _, _) => ReplaceRegionGroup.Common,
            (_, _, >= 2, TpFlashMapRegionVisibility.Always) => ReplaceRegionGroup.Master,
            (_, _, >= 2, TpFlashMapRegionVisibility.TwoChipAndAbove) => ReplaceRegionGroup.SlaveRight,
            (_, _, >= 3, TpFlashMapRegionVisibility.ThreeChipAndAbove) => ReplaceRegionGroup.SlaveLeft,
            _ => ReplaceRegionGroup.Common,
        };
    }
}
