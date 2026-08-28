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
        IReadOnlyList<MemoryCoverageLogicalItemViewModel> logicalItems =
            CreateLogicalItems(segments, text);

        return logicalItems
            .GroupBy(ResolveDisplayGroup)
            .OrderBy(static group => group.Key)
            .Select(group =>
            {
                MemoryCoverageLogicalItemViewModel[] groupItems =
                    [.. group.OrderBy(item => item.SourceLabel, StringComparer.Ordinal)];
                return new MemoryCoverageGroupViewModel(
                    text.GetReplaceRegionGroupTitle(group.Key),
                    groupItems,
                    group.Key != ReplaceRegionGroup.Base && groupItems.Any(static item =>
                        item.IsSelectedForWrite || item.HasAttentionDiagnostic),
                    group.Key,
                    text);
            });
    }

    public static IReadOnlyList<MemoryCoverageLogicalItemViewModel> CreateLogicalItems(
        IEnumerable<MemoryCoverageSegmentViewModel> segments,
        ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(text);
        return Array.AsReadOnly(
        [
            .. segments
                .OrderBy(static segment => segment.RangeStart ?? long.MaxValue)
                .Select(segment => (
                    Key: segment.LogicalCoverageGroupId ??
                        throw new InvalidOperationException(
                            "Application memory projection must publish one logical coverage group id."),
                    Segment: segment))
                .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(group => new MemoryCoverageLogicalItemViewModel(
                    group.Key,
                    group.Select(static entry => entry.Segment),
                    text)),
        ]);
    }

    private static ReplaceRegionGroup ResolveDisplayGroup(MemoryCoverageLogicalItemViewModel item)
    {
        if (!item.IsSelectedForWrite && item.UsesKeptPattern)
        {
            return ReplaceRegionGroup.Base;
        }

        ReplaceRegionGroup[] selectedGroups =
        [
            .. item.Segments
                .Where(static segment => segment.IsSelectedForWrite)
                .Select(static segment => segment.RegionGroup)
                .Distinct(),
        ];
        ReplaceRegionGroup[] groups = selectedGroups.Length > 0
            ? selectedGroups
            : [.. item.Segments.Select(static segment => segment.RegionGroup).Distinct()];
        return groups.Length == 1 ? groups[0] : ReplaceRegionGroup.Common;
    }

    private static bool RegionGroupDefaultExpanded(ReplaceRegionGroup group)
    {
        return group is ReplaceRegionGroup.Cascade or
            ReplaceRegionGroup.Common or
            ReplaceRegionGroup.Master;
    }

}
