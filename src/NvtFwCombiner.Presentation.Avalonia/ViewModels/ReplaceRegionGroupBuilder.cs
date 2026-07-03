namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class ReplaceRegionGroupBuilder
{
    public static IEnumerable<FirmwareSlotGroupViewModel> CreateSlotGroups(
        IEnumerable<FirmwareSlotViewModel> slots)
    {
        return slots
            .GroupBy(slot => RegionGroupKey(slot.Title), StringComparer.Ordinal)
            .OrderBy(group => RegionGroupOrder(group.Key))
            .Select(group =>
            {
                FirmwareSlotViewModel[] groupSlots = [.. group.OrderBy(slot => slot.Title, StringComparer.Ordinal)];
                return new FirmwareSlotGroupViewModel(
                    RegionGroupTitle(group.Key),
                    SlotGroupSummary(group.Key, groupSlots.Length),
                    groupSlots,
                    RegionGroupDefaultExpanded(group.Key));
            });
    }

    public static IEnumerable<MemoryCoverageGroupViewModel> CreateCoverageGroups(
        IEnumerable<MemoryCoverageSegmentViewModel> segments)
    {
        return segments
            .GroupBy(segment => RegionGroupKey(segment.SourceLabel), StringComparer.Ordinal)
            .OrderBy(group => RegionGroupOrder(group.Key))
            .Select(group =>
            {
                MemoryCoverageSegmentViewModel[] groupSegments =
                    [.. group.OrderBy(segment => segment.RangeLabel, StringComparer.Ordinal)];
                return new MemoryCoverageGroupViewModel(
                    RegionGroupTitle(group.Key),
                    CoverageGroupSummary(group.Key, groupSegments.Length),
                    groupSegments,
                    RegionGroupDefaultExpanded(group.Key));
            });
    }

    private static string RegionGroupKey(string label)
    {
        return label switch
        {
            string value when value.Contains("(Master)", StringComparison.OrdinalIgnoreCase) => "master",
            string value when value.Contains("(Slave R)", StringComparison.OrdinalIgnoreCase) => "slave-r",
            string value when value.Contains("(Slave L)", StringComparison.OrdinalIgnoreCase) => "slave-l",
            string value when value.Contains("Base", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("Preserve", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("Restored", StringComparison.OrdinalIgnoreCase) => "base",
            _ => "single",
        };
    }

    private static int RegionGroupOrder(string key)
    {
        return key switch
        {
            "master" => 0,
            "slave-r" => 1,
            "slave-l" => 2,
            "single" => 3,
            "base" => 4,
            _ => 5,
        };
    }

    private static bool RegionGroupDefaultExpanded(string key)
    {
        return key is "master" or "single";
    }

    private static string RegionGroupTitle(string key)
    {
        return key switch
        {
            "master" => "Master",
            "slave-r" => "Slave R",
            "slave-l" => "Slave L",
            "base" => "Base firmware",
            "single" => "Single IC",
            _ => "Other",
        };
    }

    private static string SlotGroupSummary(string key, int count)
    {
        return key switch
        {
            "base" => "Original firmware used as the starting point.",
            _ => $"{count} replaceable areas. Add files only for areas you want to change.",
        };
    }

    private static string CoverageGroupSummary(string key, int count)
    {
        return key switch
        {
            "base" => $"{count} kept areas from the original firmware.",
            _ => $"{count} areas that can be replaced for this IC group.",
        };
    }
}
