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
        MemoryCoverageSegmentViewModel[] allSegments =
        [
            .. segments.OrderBy(static segment => segment.RangeStart ?? long.MaxValue),
        ];
        IReadOnlyDictionary<string, string> selectedSlotsByRegion = allSegments
            .Where(static segment => segment is
            {
                IsSelectedForWrite: true,
                RegionId: not null,
                SourceSlotId: not null,
            })
            .GroupBy(static segment => segment.RegionId!, StringComparer.Ordinal)
            .Select(static group => (
                RegionId: group.Key,
                SourceSlots: group.Select(segment => segment.SourceSlotId!)
                    .Distinct(StringComparer.Ordinal)
                    .Take(2)
                    .ToArray()))
            .Where(static entry => entry.SourceSlots.Length == 1)
            .ToDictionary(
                static entry => entry.RegionId,
                static entry => entry.SourceSlots[0],
                StringComparer.Ordinal);
        MemoryCoverageLogicalItemViewModel[] logicalItems =
        [
            .. allSegments
                .Select((segment, index) => (
                    Key: ResolveDisplayId(segment, index, selectedSlotsByRegion),
                    Segment: segment))
                .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(group => new MemoryCoverageLogicalItemViewModel(
                    group.Key,
                    group.Select(static entry => entry.Segment),
                    text)),
        ];

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

    private static string ResolveDisplayId(
        MemoryCoverageSegmentViewModel segment,
        int index,
        IReadOnlyDictionary<string, string> selectedSlotsByRegion)
    {
        return !segment.IsSelectedForWrite &&
            segment.UsesKeptPattern &&
            segment.RegionId is { } regionId &&
            selectedSlotsByRegion.TryGetValue(regionId, out string? selectedSlot)
            ? $"slot:{selectedSlot}"
            : segment.SourceSlotId is { } sourceSlotId
            ? $"slot:{sourceSlotId}"
            : segment.RegionId is { } remainingRegionId
            ? $"region:{remainingRegionId}"
            : $"segment:{index}";
    }

    private static bool RegionGroupDefaultExpanded(ReplaceRegionGroup group)
    {
        return group is ReplaceRegionGroup.Cascade or
            ReplaceRegionGroup.Common or
            ReplaceRegionGroup.Master;
    }

}
