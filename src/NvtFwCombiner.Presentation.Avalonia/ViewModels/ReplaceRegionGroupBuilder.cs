namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class ReplaceRegionGroupBuilder
{
    public static IEnumerable<FirmwareSlotGroupViewModel> CreateSlotGroups(
        IEnumerable<FirmwareSlotViewModel> slots,
        ShellTextResources text)
    {
        return slots
            .GroupBy(static slot => slot.RegionGroup)
            .OrderBy(static group => group.Key)
            .Select(group =>
            {
                FirmwareSlotViewModel[] groupSlots = [.. group.OrderBy(slot => slot.Title, StringComparer.Ordinal)];
                return new FirmwareSlotGroupViewModel(
                    groupSlots,
                    RegionGroupDefaultExpanded(group.Key),
                    text);
            });
    }

    public static IEnumerable<MemoryCoverageGroupViewModel> CreateCoverageGroups(
        IEnumerable<MemoryCoverageSegmentViewModel> segments,
        ShellTextResources text)
    {
        return segments
            .GroupBy(static segment => segment.RegionGroup)
            .OrderBy(static group => group.Key)
            .Select(group =>
            {
                MemoryCoverageSegmentViewModel[] groupSegments =
                    [.. group.OrderBy(segment => segment.RangeLabel, StringComparer.Ordinal)];
                return new MemoryCoverageGroupViewModel(
                    text.GetReplaceRegionGroupTitle(group.Key),
                    text.FormatReplaceCoverageGroupSummary(group.Key, groupSegments.Length),
                    groupSegments,
                    RegionGroupDefaultExpanded(group.Key),
                    group.Key,
                    text);
            });
    }

    private static bool RegionGroupDefaultExpanded(ReplaceRegionGroup group)
    {
        return group is ReplaceRegionGroup.Cascade or
            ReplaceRegionGroup.Common or
            ReplaceRegionGroup.Master;
    }

}
