using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets structured Replace input slots for the selected mode and device context.</summary>
    public static IReadOnlyList<WorkbenchReplaceInputSlot> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode,
        string? basePath = null)
    {
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => GetDpReplaceInputSlots(icId),
            WorkbenchReplaceModes.CtrlRam => GetCtrlRamReplaceInputSlots(icId, number, basePath),
            _ => [],
        };
    }

    /// <summary>Gets readable memory-map rows for the selected Replace mode.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetReplaceMemoryMapRows(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        if (replaceMode == WorkbenchReplaceModes.Dp &&
            TryCreateV2DpReplaceMemoryMapRows(icId, dpBaseLength, out IReadOnlyList<WorkbenchMemoryMapRow> v2Rows))
        {
            return v2Rows;
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        LegacyCombinerPostbuildProfile? postbuildProfile = replaceMode == WorkbenchReplaceModes.CtrlRam &&
            TryResolvePostbuildProfileForDisplay(icId, ctrlRamBasePath, out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile);
        return regions.Count == 0
            ?
            [
                new WorkbenchMemoryMapRow(
                    "Catalog",
                    "No flash-map row",
                    "Blocked",
                    "No target",
                    $"No TP Overview flash-map profile is available for {icId}."),
            ]
            : replaceMode switch
            {
                WorkbenchReplaceModes.Dp => CreateDpReplaceRows(icId, regions),
                WorkbenchReplaceModes.CtrlRam => CreateCtrlRamReplaceRows(
                    TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile)),
                WorkbenchReplaceModes.General =>
                [
                    new WorkbenchMemoryMapRow(
                        "Runtime range",
                        "Base flash",
                        "Replace",
                        "General BIN",
                        "The selected explicit range must be approved by the compiled General Replace profile; TP ranges require Combiner CRC/header refresh."),
                ],
                _ =>
                [
                    new WorkbenchMemoryMapRow(
                        "Mode",
                        "Unknown",
                        "Select",
                        "No target",
                        "Select DP, CtrlRAM, or General Replace."),
                ],
            };
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number)
    {
        return GetReplaceMemoryRangeLabel(icId, number, replaceMode: string.Empty);
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context and mode.</summary>
    public static string GetReplaceMemoryRangeLabel(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        if (replaceMode == WorkbenchReplaceModes.Dp &&
            TryGetV2DpReplaceMemoryRangeLabel(icId, dpBaseLength, out string v2RangeLabel))
        {
            return v2RangeLabel;
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        LegacyCombinerPostbuildProfile? postbuildProfile = replaceMode == WorkbenchReplaceModes.CtrlRam &&
            TryResolvePostbuildProfileForDisplay(icId, ctrlRamBasePath, out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection, postbuildProfile);
        return regions.Count == 0
            ? "No flash-map profile"
            : FormatFullRange(regions.Max(region => region.Range.EndExclusive));
    }

}
