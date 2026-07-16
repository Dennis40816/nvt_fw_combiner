using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateCtrlRamReplaceRows(
        IReadOnlyList<TpFlashMapRegion> postbuildMappedRegions)
    {
        return [
            .. postbuildMappedRegions
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Replace + CRC",
                    region.PostbuildFileName ?? "CtrlRAM BIN",
                    $"{region.DisplayName} at {FormatDisplayRange(region.Range)} can use its own replacement BIN; the report shows the CRC/header refresh command.")),
        ];
    }

    private static IReadOnlyList<WorkbenchReplaceInputSlot> GetCtrlRamReplaceInputSlots(
        string icId,
        string number,
        string? basePath)
    {
        _ = TryResolvePostbuildProfileForDisplay(
            icId,
            basePath,
            out LegacyCombinerPostbuildProfile? postbuildProfile);
        return postbuildProfile is null && basePath is not null && File.Exists(basePath)
            ? []
            : [
            .. TpFlashMapCatalog.GetPostbuildCtrlRamSources(icId, ToIcNumberSelection(number), postbuildProfile)
                    .Select(source => new WorkbenchReplaceInputSlot(
                        CtrlRamSlotId(source.SourceId),
                        FormatCtrlRamSourceTitle(icId, number, source),
                        FormatCtrlRamSourceDescription(icId, number, source),
                        true,
                        CtrlRamSlotId(source.SourceId),
                        source.SourceId)),
            ];
    }

    private static string FormatCtrlRamSourceTitle(
        string icId,
        string number,
        TpCtrlRamPostbuildSource source)
    {
        string title = source.Regions.Count == 1
            ? source.Regions[0].DisplayName
            : $"{DynamicCtrlRamReplacementIds.FormatRegionDisplayLabel(source.SourceId)} (Shared)";
        return RequiresDiffNfMergeOutput(icId, number, source)
            ? $"{title} (DiffNFMerge output)"
            : title;
    }

    private static string FormatCtrlRamSourceDescription(
        string icId,
        string number,
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
        return RequiresDiffNfMergeOutput(icId, number, source)
            ? $"{description} Required cascade input: select NF_Ctrlram.bin only after DiffNFMerge.exe has compiled one NF_Diff_<index>.bin per cascaded IC (NF_Diff_0.bin, NF_Diff_1.bin, ...). DiffNFMerge execution is not yet integrated."
            : description;
    }

    private static bool RequiresDiffNfMergeOutput(
        string icId,
        string number,
        TpCtrlRamPostbuildSource source)
    {
        return source.SourceId == "nf" &&
            number == IcNumberSelectionTokens.Cascade &&
            IcSupportCatalog.NormalizeIcId(icId) is
                "NT51919" or "NT51929" or "NT51932" or "NT51950" or "NT51951";
    }

    private static string CtrlRamSlotId(string regionId)
    {
        return WorkbenchSlotIds.CreateReplaceCtrlRam(regionId);
    }

}
