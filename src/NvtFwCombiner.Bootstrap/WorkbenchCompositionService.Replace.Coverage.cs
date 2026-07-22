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
                false,
                WorkbenchMemoryCoverageRole.BaseFirmware),
        ];

        IEnumerable<TpFlashMapRegion> replacementRegions = replaceMode == WorkbenchReplaceModes.CtrlRam
            ? BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile)
            : [];

        foreach (TpFlashMapRegion region in replacementRegions.OrderBy(region => region.Range.Start))
        {
            string label = region.DisplayName;
            string detail = $"{region.DisplayName} can be replaced here. Empty input keeps the original firmware; Preview lists the CRC/header refresh command.";
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    label,
                    detail,
                    CoverageFill(label),
                    true,
                    WorkbenchMemoryCoverageRole.Standard));
        }

        return ToWorkbenchCoverageSegments(segments, capacity);
    }
}
