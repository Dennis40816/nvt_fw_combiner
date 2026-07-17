using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateCtrlRamReplaceRows(
        IReadOnlyList<TpFlashMapRegion> postbuildMappedRegions)
    {
        return
        [
            .. postbuildMappedRegions
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base firmware",
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
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            basePath,
            out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        return
        [
            .. TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, ToIcNumberSelection(number), postbuildProfile)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchReplaceInputSlot(
                    CtrlRamSlotId(region.RegionId),
                    region.DisplayName,
                    $"Replace this area only when needed. TP position {FormatDisplayRange(region.Range)}.",
                    true,
                    CtrlRamSlotId(region.RegionId),
                    region.RegionId)),
        ];
    }

    private static string CtrlRamSlotId(string regionId)
    {
        return WorkbenchSlotIds.CreateReplaceCtrlRam(regionId);
    }

    private static InputArtifactBinding CreateBinding(
        string addressSpaceId,
        string slotId,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return slotPaths.TryGetValue(slotId, out string? path) && !string.IsNullOrWhiteSpace(path)
            ? new InputArtifactBinding(addressSpaceId, slotId, Path.GetFullPath(path))
            : throw new InvalidOperationException($"Input slot '{slotId}' is required.");
    }
}
