// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Copies existing display facts without deriving firmware or topology decisions.</summary>
internal static class CtrlRamZoomDisplay
{
    internal static CtrlRamZoomData? FromLane(MemoryFocusLaneViewModel? lane)
    {
        return lane is null || lane.Ranges.Count == 0
            ? null
            : new CtrlRamZoomData(lane.Start, lane.EndExclusive, lane.Title,
                lane.Ranges[0].AddressSpaceId ?? string.Empty, lane.RangeLabel,
                FormattableString.Invariant($"{(lane.EndExclusive - lane.Start) / 1024d:0.###} KiB"),
                lane.Ranges.Select(Slice));
    }

    private static CtrlRamZoomSlice Slice(MemoryCoverageSegmentViewModel range)
    {
        IEnumerable<MemoryCoverageSegmentViewModel> parts = range.DisplayParts.Count > 0 ? range.DisplayParts : [range];
        string label = range.ContentRole == MemoryContentRole.CtrlRam &&
            range.DisplayParts.All(part => part.CtrlRamRegionRole == range.CtrlRamRegionRole)
            ? ShellTextResources.GetMemoryFocusLabel(range.CtrlRamRegionRole) : range.DisplayTitle;
        return new(range.RangeStart!.Value, range.RangeEndExclusive!.Value, label, range.AccessibleDetail,
            range.AddressRangeLabel, range.SizeValue, range.SourceLabel, range.ChangeLabel, range.Detail,
            range.SourceFieldLabel, range.PreservationSummary, range.DetailsLabel,
            range.PreservationDetails, range.ProcessingFacts,
            Array.AsReadOnly(parts.Select(static part => new CtrlRamZoomFill(
                part.RangeStart!.Value, part.RangeEndExclusive!.Value,
                $"NfcMemory{part.FillRole}FillBrush", part.UsesKeptPattern)).ToArray()));
    }
}
