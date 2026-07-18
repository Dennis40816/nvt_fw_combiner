using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static IReadOnlyList<WorkbenchMemoryCoverageSegment> CreateReplaceCoverageSegments(
        string icId,
        string replaceMode,
        IcNumberSelection selection,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        long capacity = regions.Max(region => region.Range.EndExclusive);
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                "Base firmware",
                "Kept from the original base firmware unless a replacement covers it.",
                "#CBD5E1",
                false),
        ];

        IEnumerable<TpFlashMapRegion> replacementRegions = replaceMode switch
        {
            WorkbenchReplaceModes.Dp => GetDpReplaceRegions(icId, regions),
            WorkbenchReplaceModes.CtrlRam => BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile),
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
