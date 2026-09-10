using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>A visual container for one endpoint's continuous run, retaining existing per-item facts.</summary>
internal sealed class MemoryFocusLaneViewModel
{
    private MemoryFocusLaneViewModel(List<MemoryCoverageSegmentViewModel> ranges, ShellTextResources text, bool isSingleIc)
    {
        Ranges = [.. ranges];
        Text = text;
        // This is a heading only: shared input groups and physical range identities stay intact.
        DisplayGroup = isSingleIc && ranges[0].RegionGroup == ReplaceRegionGroup.Common
            ? ReplaceRegionGroup.Master : ranges[0].RegionGroup;
        Title = text.GetReplaceRegionGroupTitle(DisplayGroup);
        Start = ranges[0].RangeStart!.Value;
        EndExclusive = ranges[^1].RangeEndExclusive!.Value;
        RangeLabel = FormattableString.Invariant($"0x{Start:X5}-0x{EndExclusive - 1:X5}");
    }

    public ShellTextResources Text { get; }
    private ReplaceRegionGroup DisplayGroup { get; }
    public string Title { get; }
    public string RangeLabel { get; }
    public long Start { get; }
    public long EndExclusive { get; }
    public IReadOnlyList<MemoryCoverageSegmentViewModel> Ranges { get; }

    public string PositionLabel => DisplayGroup switch
    {
        ReplaceRegionGroup.Master => "M",
        ReplaceRegionGroup.SlaveRight => "R",
        ReplaceRegionGroup.SlaveLeft => "L",
        ReplaceRegionGroup.Common or ReplaceRegionGroup.Cascade or ReplaceRegionGroup.Base or ReplaceRegionGroup.Other => "•",
        _ => throw new InvalidOperationException("Unknown endpoint group."),
    };

    public static IReadOnlyList<MemoryFocusPositionViewModel> CreatePositions(
        IEnumerable<MemoryFocusLaneViewModel> lanes, long capacity)
    {
        var parts = new List<MemoryFocusPositionViewModel>();
        long cursor = 0;
        foreach (MemoryFocusLaneViewModel lane in lanes.OrderBy(static lane => lane.Start))
        {
            if (lane.Start > cursor) { parts.Add(new(lane.Start - cursor, string.Empty)); }
            parts.Add(new(lane.EndExclusive - lane.Start, lane.PositionLabel, lane));
            cursor = lane.EndExclusive;
        }
        if (capacity > cursor) { parts.Add(new(capacity - cursor, string.Empty)); }
        return parts;
    }

    public static IReadOnlyList<MemoryFocusLaneViewModel> Create(
        IEnumerable<MemoryCoverageLogicalItemViewModel> items, ShellTextResources text, bool isSingleIc = false)
    {
        var runs = new List<List<MemoryCoverageSegmentViewModel>>();
        foreach (MemoryCoverageSegmentViewModel range in items.SelectMany(static item => item.Ranges)
            .Where(static range => range.ContentRole == MemoryContentRole.CtrlRam && range.RangeStart.HasValue)
            .OrderBy(static range => range.AddressSpaceId, StringComparer.Ordinal).ThenBy(static range => range.RangeStart))
        {
            List<MemoryCoverageSegmentViewModel>? run = runs.LastOrDefault();
            if (run is null || run[^1].RegionGroup != range.RegionGroup || !range.ImmediatelyFollows(run[^1]))
            {
                run = [];
                runs.Add(run);
            }
            run.Add(range);
        }
        return [.. runs.Select(run => new MemoryFocusLaneViewModel(run, text, isSingleIc))];
    }
}

internal sealed record MemoryFocusPositionViewModel(double BarWidth, string Label, MemoryFocusLaneViewModel? Lane = null)
{
    public bool IsTarget => Label.Length > 0;
}
