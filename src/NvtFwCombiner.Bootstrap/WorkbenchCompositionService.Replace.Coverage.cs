using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

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

        IEnumerable<(TpFlashMapRegion Region, string SelectionId)> replacementRegions =
            replaceMode == WorkbenchReplaceModes.CtrlRam
            ? BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(icId, selection, postbuildProfile)
                .SelectMany(source => source.Regions.Select(region => (region, source.SourceId)))
                .DistinctBy(static item => item.region.RegionId, StringComparer.Ordinal)
            : [];

        foreach ((TpFlashMapRegion region, string selectionId) in replacementRegions.OrderBy(item => item.Region.Range.Start))
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
                    false,
                    WorkbenchMemoryCoverageRole.Standard,
                    selectionId));
        }

        return ToWorkbenchCoverageSegments(segments, capacity);
    }

    /// <summary>Projects selected CtrlRAM regions without changing the compiled coverage geometry.</summary>
    public static WorkbenchMemoryDisplay ApplyReplaceCoverageSelection(
        WorkbenchMemoryDisplay display,
        IEnumerable<string> selectedRegionIds)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(selectedRegionIds);
        var selected = selectedRegionIds.ToHashSet(StringComparer.Ordinal);
        return display with
        {
            CoverageSegments =
            [
                .. display.CoverageSegments.Select(segment => segment with
                {
                    IsChanged = segment.RegionId is null
                        ? segment.IsChanged
                        : selected.Contains(segment.RegionId),
                }),
            ],
        };
    }
}
