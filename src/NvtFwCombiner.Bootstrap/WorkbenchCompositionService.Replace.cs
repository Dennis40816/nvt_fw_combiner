using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static IReadOnlyList<WorkbenchReplaceInputSlot> GetCtrlRamReplaceInputSlots(
        string icId,
        string number,
        string? basePath)
    {
        _ = TryResolvePostbuildProfileForDisplay(icId, basePath, out LegacyCombinerPostbuildProfile? postbuildProfile);
        LegacyCombinerPostbuildBranch branch = postbuildProfile is null ? LegacyCombinerPostbuildBranch.SingleChip :
            LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, ToIcNumberSelection(number)).Branch;
        return postbuildProfile is null && basePath is not null && File.Exists(basePath)
            ? []
            : [
            .. BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(icId, ToIcNumberSelection(number), postbuildProfile)
                    .Select(source => new WorkbenchReplaceInputSlot(
                        CtrlRamSlotId(source.SourceId),
                        FormatCtrlRamSourceTitle(icId, branch, source),
                        FormatCtrlRamSourceDescription(icId, branch, source),
                        true,
                        CtrlRamSlotId(source.SourceId),
                        source.SourceId)),
            ];
    }

    private static string FormatCtrlRamSourceTitle(
        string icId,
        LegacyCombinerPostbuildBranch branch,
        TpCtrlRamPostbuildSource source)
    {
        string title = source.Regions.Count == 1
            ? source.Regions[0].DisplayName
            : $"{DynamicCtrlRamReplacementIds.FormatRegionDisplayLabel(source.SourceId)} (Shared)";
        return RequiresDiffNfMergeOutput(icId, branch, source)
            ? $"{title} (DiffNFMerge output)"
            : title;
    }

    private static string FormatCtrlRamSourceDescription(
        string icId,
        LegacyCombinerPostbuildBranch branch,
        TpCtrlRamPostbuildSource source)
    {
        string sections = string.Join("; ", source.Blocks
            .DistinctBy(block => (block.SourceOffset, block.FirmwareRange))
            .OrderBy(block => block.FirmwareRange.Start)
            .Select(block =>
            {
                TpFlashMapRegion region = source.Regions.Single(region => region.Range.Overlaps(block.FirmwareRange));
                return $"{region.DisplayName}: max {block.FirmwareRange.Length} bytes, source +0x{block.SourceOffset:X} to flash 0x{block.FirmwareRange.Start:X}";
            }));
        string description = $"{source.SourceFileName}. Expected sections: {sections}. Short source files stop at EOF without padding; bytes beyond each section maximum are not used.";
        return RequiresDiffNfMergeOutput(icId, branch, source)
            ? $"{description} Required cascade input: select an NF_Ctrlram.bin prebuilt by the external DiffNFMerge.exe. Its input contract and execution are not yet integrated."
            : description;
    }

    private static bool RequiresDiffNfMergeOutput(
        string icId,
        LegacyCombinerPostbuildBranch branch,
        TpCtrlRamPostbuildSource source)
    {
        return source.SourceId == "nf" &&
            IcSupportCatalog.NormalizeIcId(icId) is
                "NT51919" or "NT51929" or "NT51932" or "NT51950" or "NT51951" &&
            branch == LegacyCombinerPostbuildBranch.Cascade;
    }

    private static string CtrlRamSlotId(string regionId)
    {
        return WorkbenchSlotIds.CreateReplaceCtrlRam(regionId);
    }

}
