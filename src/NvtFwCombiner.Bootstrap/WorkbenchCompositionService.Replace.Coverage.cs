using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets final visual coverage segments for the selected Replace view.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetReplaceCoverageSegments(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        if (replaceMode == WorkbenchReplaceModes.Dp &&
            TryCreateV2DpReplaceCoverageSegments(icId, dpBaseLength, out IReadOnlyList<WorkbenchMemoryCoverageSegment> v2Segments))
        {
            return v2Segments;
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
                    "Base flash",
                    "Final bytes remain from the original base firmware.",
                    "#CBD5E1",
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
