using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.MemoryLayout;
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
                WorkbenchMemoryCoverageRole.BaseFirmware,
                RegionGroup: WorkbenchReplaceRegionGroup.Base),
        ];

        IReadOnlyList<TpCtrlRamPostbuildSource> replacementSources =
            replaceMode == WorkbenchReplaceModes.CtrlRam
                ? BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(
                    icId,
                    selection,
                    postbuildProfile)
                : [];
        int topologyCount = postbuildProfile is null
            ? 1
            : LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection).TopologyCount;
        foreach (TpCtrlRamPostbuildSource source in replacementSources
            .OrderBy(static source => source.Blocks.Min(block => block.FirmwareRange.Start)))
        {
            IEnumerable<(ByteRange Range, string Label, bool IsDiffDlm, IReadOnlyList<MemoryLayoutPreservationDetail>? Details)> sourceSegments;
            if (source.ArtifactRole == TpCtrlRamPostbuildArtifactRole.DiffDlm)
            {
                LegacyCombinerDiffDlmPolicy? policy = postbuildProfile?.DiffDlmPolicy is { } candidate &&
                    StringComparer.Ordinal.Equals(candidate.SourceFileName, source.SourceFileName) &&
                    candidate.AppliesTo(topologyCount)
                        ? candidate
                        : null;
                sourceSegments =
                [
                    (policy?.GetActiveTargetRange(topologyCount) ??
                        ByteRange.FromStartEndExclusive(
                            source.Blocks.Min(static block => block.FirmwareRange.Start),
                            source.Blocks.Max(static block => block.FirmwareRange.EndExclusive)),
                     "DiffDLM",
                     true,
                     policy?.GetPreservationDetails(topologyCount)),
                ];
            }
            else
            {
                sourceSegments = source.Regions
                    .DistinctBy(static region => region.RegionId, StringComparer.Ordinal)
                    .Select(static region => (region.Range, region.DisplayName, false,
                        (IReadOnlyList<MemoryLayoutPreservationDetail>?)null));
            }

            foreach ((ByteRange range, string label, bool isDiffDlm, IReadOnlyList<MemoryLayoutPreservationDetail>? details) in sourceSegments)
            {
                string detail = $"{label} can be replaced here. Empty input keeps the original firmware; Preview lists the CRC/header refresh command.";
                segments = ApplyCoverageWrite(
                    segments,
                    new CoverageSegment(
                        range,
                        label,
                        detail,
                        CoverageFill(label),
                        false,
                        WorkbenchMemoryCoverageRole.Standard,
                        source.SourceId,
                        isDiffDlm,
                        details,
                        GetCtrlRamRegionGroup(source)));
            }
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
