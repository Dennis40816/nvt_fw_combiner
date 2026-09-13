using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class ReplaceRegionGroupBuilder
{
    internal static void UpdateSlotGroups(
        ObservableCollection<FirmwareSlotGroupViewModel> target,
        IEnumerable<FirmwareSlotViewModel> slots,
        ShellTextResources text)
    {
        int index = 0;
        foreach (IGrouping<ReplaceRegionGroup, FirmwareSlotViewModel> grouping in slots
            .GroupBy(static slot => slot.RegionGroup).OrderBy(static group => group.Key))
        {
            FirmwareSlotViewModel[] current = [.. grouping.OrderBy(slot => slot.Title, StringComparer.Ordinal)];
            FirmwareSlotGroupViewModel? group = target.FirstOrDefault(candidate =>
                candidate.Slots[0].RegionGroup == grouping.Key);
            if (group is null)
            {
                group = new FirmwareSlotGroupViewModel(current, RegionGroupDefaultExpanded(grouping.Key), text);
                target.Insert(index, group);
            }
            else
            {
                group.UpdateSlots(current);
                group.ApplyText(text);
                int previousIndex = target.IndexOf(group);
                if (previousIndex != index) { target.Move(previousIndex, index); }
            }
            index++;
        }
        while (target.Count > index)
        {
            target[index].DisconnectSlots();
            target.RemoveAt(index);
        }
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
                .Where(static segment => segment.IsPrimaryContent)
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
