using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

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
                WorkbenchReplaceModes.Dp => CreateDpReplaceRows(icId, regions, dpBaseLength),
                WorkbenchReplaceModes.CtrlRam => CreateCtrlRamReplaceRows(
                    regions,
                    TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile)),
                WorkbenchReplaceModes.General =>
                [
                    .. CreatePreserveRows(regions),
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
        if (replaceMode == WorkbenchReplaceModes.Dp && IsDpPerspectiveIc(icId))
        {
            return dpBaseLength is long value
                ? IsSupportedDpPerspectiveBaseLength(value)
                    ? FormatFullRange(value)
                    : $"Unsupported base BIN length {FormatHexLength(value)}"
                : $"Base BIN length: {FormatSupportedDpPerspectiveBaseLengths()}";
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

    /// <summary>Gets final visual coverage segments for the selected Replace view.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetReplaceCoverageSegments(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        LegacyCombinerPostbuildProfile? postbuildProfile = replaceMode == WorkbenchReplaceModes.CtrlRam &&
            TryResolvePostbuildProfileForDisplay(icId, ctrlRamBasePath, out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile);
        if (regions.Count == 0)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "No range",
                    "No profile",
                    $"No TP Overview flash-map profile is available for {icId}.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        if (replaceMode == WorkbenchReplaceModes.Dp && IsDpPerspectiveIc(icId))
        {
            if (dpBaseLength is not long selectedBaseLength)
            {
                return
                [
                    new WorkbenchMemoryCoverageSegment(
                        "Base length pending",
                        "DP base required",
                        $"Select a base BIN to resolve the actual DP Replace length ({FormatSupportedDpPerspectiveBaseLengths()}).",
                        "#CBD5E1",
                        280,
                        false),
                ];
            }

            if (!IsSupportedDpPerspectiveBaseLength(selectedBaseLength))
            {
                return
                [
                    new WorkbenchMemoryCoverageSegment(
                        $"Unsupported {FormatHexLength(selectedBaseLength)}",
                        "Unsupported base",
                        $"This base BIN length is not approved for {FormatDpPerspectiveIcIds()} DP Replace; use {FormatSupportedDpPerspectiveBaseLengths()}.",
                        "#FCA5A5",
                        280,
                        false),
                ];
            }

            long selectedCapacity = selectedBaseLength;
            ByteRange tpRestoreRange = DpPerspectiveCatalog.TpOverlayRange;
            ByteRange customerInfoPreserveRange = DpPerspectiveCatalog.CustomerInfoPreserveRange;
            CoverageSegment[] dpSegments =
            [
                new CoverageSegment(
                    new ByteRange(0, selectedCapacity),
                    "Base flash",
                    "Kept from the original base firmware unless a replacement covers it.",
                    "#CBD5E1",
                    false),
            ];
            var dpRange = new ByteRange(0, selectedCapacity);
            dpSegments = ApplyCoverageWrite(
                dpSegments,
                new CoverageSegment(
                    dpRange,
                    "Changed DP BIN",
                    $"Replacement DP fills the selected base DP length {FormatDisplayRange(dpRange)}; shorter inputs are padded by profile policy.",
                    "#2563EB",
                    true));
            dpSegments = ApplyCoverageWrite(
                dpSegments,
                new CoverageSegment(
                    tpRestoreRange,
                    "Restored TP",
                    $"Original TP FW at {FormatDisplayRange(tpRestoreRange)} is copied back from the base firmware.",
                    "#64748B",
                    false));
            dpSegments = ApplyCoverageWrite(
                dpSegments,
                new CoverageSegment(
                    customerInfoPreserveRange,
                    "Preserved customer info",
                    $"Customer information at {FormatDisplayRange(customerInfoPreserveRange)} is copied back from the base firmware.",
                    "#64748B",
                    false));
            return ToWorkbenchCoverageSegments(dpSegments, selectedCapacity);
        }

        long capacity = regions.Max(region => region.Range.EndExclusive);
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                "Base flash",
                "Kept from the original base firmware unless a replacement covers it.",
                "#CBD5E1",
                false),
        ];

        foreach (TpFlashMapRegion region in regions
            .Where(IsPreservedRegion)
            .OrderBy(region => region.Range.Start))
        {
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    "Preserve",
                    $"{region.DisplayName} stays from the original base firmware.",
                    "#94A3B8",
                    false));
        }

        IEnumerable<TpFlashMapRegion> replacementRegions = replaceMode switch
        {
            WorkbenchReplaceModes.Dp => GetDpReplaceRegions(icId, regions),
            WorkbenchReplaceModes.CtrlRam => TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile),
            _ => [],
        };

        foreach (TpFlashMapRegion region in replacementRegions.OrderBy(region => region.Range.Start))
        {
            string label = replaceMode switch
            {
                WorkbenchReplaceModes.Dp => IsLdRegion(region) ? "Changed LDC BIN" : "Changed DP BIN",
                WorkbenchReplaceModes.CtrlRam => region.DisplayName,
                _ => "Replacement BIN",
            };
            string detail = replaceMode == WorkbenchReplaceModes.CtrlRam
                ? $"{region.DisplayName} can be replaced here. Empty input keeps the original firmware; Preview lists the CRC/header refresh command."
                : $"{region.DisplayName}; {ActionSummaryForReplaceMode(replaceMode)}";
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    label,
                    detail,
                    CoverageFill(label),
                    true));
        }

        return ToWorkbenchCoverageSegments(segments, capacity);
    }
}
