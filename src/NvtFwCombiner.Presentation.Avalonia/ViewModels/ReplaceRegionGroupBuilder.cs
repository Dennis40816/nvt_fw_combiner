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
            string value when value.Contains("(Shared)", StringComparison.OrdinalIgnoreCase) =>
                ReplaceRegionGroupKeys.Shared,
            string value when value.Contains("(Master)", StringComparison.OrdinalIgnoreCase) =>
                ReplaceRegionGroupKeys.Master,
            string value when value.Contains("(Slave R)", StringComparison.OrdinalIgnoreCase) =>
                ReplaceRegionGroupKeys.SlaveRight,
            string value when value.Contains("(Slave L)", StringComparison.OrdinalIgnoreCase) =>
                ReplaceRegionGroupKeys.SlaveLeft,
            string value when value.Contains("Base", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("Preserve", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("Restored", StringComparison.OrdinalIgnoreCase) =>
                ReplaceRegionGroupKeys.Base,
            _ => ReplaceRegionGroupKeys.Single,
        };
    }

    private static int RegionGroupOrder(string key)
    {
        return key switch
        {
            ReplaceRegionGroupKeys.Shared => 0,
            ReplaceRegionGroupKeys.Master => 1,
            ReplaceRegionGroupKeys.SlaveRight => 2,
            ReplaceRegionGroupKeys.SlaveLeft => 3,
            ReplaceRegionGroupKeys.Single => 4,
            ReplaceRegionGroupKeys.Base => 5,
            _ => 6,
        };
    }

    private static bool RegionGroupDefaultExpanded(string key)
    {
        return key is ReplaceRegionGroupKeys.Shared or ReplaceRegionGroupKeys.Master or ReplaceRegionGroupKeys.Single;
    }

    private static string RegionGroupTitle(string key)
    {
        return key switch
        {
            ReplaceRegionGroupKeys.Shared => "Shared inputs",
            ReplaceRegionGroupKeys.Master => "Master",
            ReplaceRegionGroupKeys.SlaveRight => "Slave R",
            ReplaceRegionGroupKeys.SlaveLeft => "Slave L",
            ReplaceRegionGroupKeys.Base => "Base firmware",
            ReplaceRegionGroupKeys.Single => "Single IC",
            _ => "Other",
        };
    }

    private static string SlotGroupSummary(string key, int count)
    {
        return key switch
        {
            ReplaceRegionGroupKeys.Base => "Original firmware used as the starting point.",
            ReplaceRegionGroupKeys.Shared => $"{count} physical input files reused by the approved Postbuild.",
            _ => $"{count} replaceable areas. Add files only for areas you want to change.",
        };
    }

    private static string CoverageGroupSummary(string key, int count)
    {
        return key switch
        {
            ReplaceRegionGroupKeys.Base => $"{count} areas retained from the base flash BIN.",
            _ => $"{count} areas that can be replaced for this IC group.",
        };
    }
}
