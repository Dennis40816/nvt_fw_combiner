using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MemoryCoverageGroupViewModel : ObservableObject
{
    private readonly ShellTextResources _text;
    public MemoryCoverageGroupViewModel(
        string title,
        IEnumerable<MemoryCoverageLogicalItemViewModel> items,
        bool isExpanded,
        ReplaceRegionGroup regionGroup,
        ShellTextResources text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(items);

        Title = title;
        Items = [.. items];
        IsExpanded = isExpanded;
        RegionGroup = regionGroup;
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title { get; }

    public ObservableCollection<MemoryCoverageLogicalItemViewModel> Items { get; }

    public int SegmentCount => Items.Count;

    public ReplaceRegionGroup RegionGroup { get; }

    public int SelectedCount => Items.Count(item => item.IsSelectedForWrite);

    /// <summary>Compact selected/total count shown in collapsed headers.</summary>
    public string CountLabel => IsBaseFirmwareGroup ? $"{SegmentCount}" : $"{SelectedCount}/{SegmentCount}";

    public string SelectionSummary => _text.FormatCoverageSelectionSummary(
        IsBaseFirmwareGroup,
        SelectedCount,
        SegmentCount);

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private bool IsBaseFirmwareGroup => RegionGroup == ReplaceRegionGroup.Base;
}

internal sealed class MemoryCoverageLogicalItemViewModel
{
    public MemoryCoverageLogicalItemViewModel(
        string displayId,
        IEnumerable<MemoryCoverageSegmentViewModel> ranges,
        ShellTextResources text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayId);
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(text);

        MemoryCoverageSegmentViewModel[] projectedSegments = [.. ranges];
        if (projectedSegments.Length == 0)
        {
            throw new ArgumentException("A logical memory item requires at least one range.", nameof(ranges));
        }

        MemoryCoverageSegmentViewModel primary =
            projectedSegments.FirstOrDefault(static range => range.IsSelectedForWrite) ?? projectedSegments[0];
        DisplayId = displayId;
        Ranges = ProjectRanges(projectedSegments, text);
        Segments = Array.AsReadOnly(projectedSegments);
        SourceLabel = primary.LogicalSourceLabel;
        long? totalLength = Ranges.All(static range => range.RangeStart.HasValue)
            ? Ranges.Sum(static range => range.RangeEndExclusive!.Value - range.RangeStart!.Value)
            : null;
        RangeSummaryLabel = text.FormatMemoryCoverageRangeSummary(Ranges.Count, totalLength);
        string rangeDetails = string.Join(" ", Ranges.Select(static range => range.AccessibleDetail));
        AccessibleDetail = HasMultipleRanges
            ? $"{SourceLabel}. {RangeSummaryLabel}. {rangeDetails}"
            : $"{SourceLabel}. {rangeDetails}";
    }

    /// <summary>Stable typed projection identity used to prevent duplicate top-level entries.</summary>
    public string DisplayId { get; }

    public string SourceLabel { get; }

    public IReadOnlyList<MemoryCoverageSegmentViewModel> Ranges { get; }

    public MemoryCoverageSegmentViewModel PrimaryRange => Ranges[0];

    public IReadOnlyList<MemoryCoverageSegmentViewModel> Segments { get; }

    public bool UsesKeptPattern => Segments.All(static range => range.UsesKeptPattern);

    public bool IsSelectedForWrite => Segments.Any(static range => range.IsSelectedForWrite);

    public bool HasAttentionDiagnostic => Segments.Any(static range => range.HasAttentionDiagnostic);

    public bool HasMultipleRanges => Ranges.Count > 1;

    public bool HasSingleRange => Ranges.Count == 1;

    public string RangeSummaryLabel { get; }

    public string AccessibleDetail { get; }

    private static IReadOnlyList<MemoryCoverageSegmentViewModel> ProjectRanges(
        IEnumerable<MemoryCoverageSegmentViewModel> segments,
        ShellTextResources text)
    {
        MemoryCoverageSegmentViewModel[] ordered =
        [
            .. segments.OrderBy(static segment => segment.RangeStart ?? long.MaxValue),
        ];
        var bundles = new List<List<MemoryCoverageSegmentViewModel>>();
        foreach (MemoryCoverageSegmentViewModel segment in ordered)
        {
            List<MemoryCoverageSegmentViewModel>? current = bundles.LastOrDefault();
            if (current is null ||
                current[^1].RangeEndExclusive is not { } currentEnd ||
                segment.RangeStart != currentEnd ||
                current[^1].RegionGroup != segment.RegionGroup ||
                current[^1].FillRole != segment.FillRole ||
                current[^1].ObservedChange != segment.ObservedChange ||
                current[^1].HasPreservationDetails ||
                segment.HasPreservationDetails)
            {
                current = [];
                bundles.Add(current);
            }
            current.Add(segment);
        }

        return
        [
            .. bundles.Select(bundle => ProjectRange(bundle, text)),
        ];
    }

    private static MemoryCoverageSegmentViewModel ProjectRange(
        List<MemoryCoverageSegmentViewModel> projected,
        ShellTextResources text)
    {
        if (projected.Count == 1)
        {
            return projected[0];
        }

        MemoryCoverageSegmentViewModel primary =
            projected.FirstOrDefault(static segment => segment.IsSelectedForWrite) ?? projected[0];
        long start = projected[0].RangeStart!.Value;
        long endExclusive = projected[^1].RangeEndExclusive!.Value;
        bool isPartial = projected.Any(static segment => segment.IsSelectedForWrite) &&
            projected.Any(static segment => segment.UsesKeptPattern);
        string detail = isPartial
            ? text.FormatMemoryCoveragePartialReplaceDetail(primary.SourceLabel)
            : string.Join(
                " ",
                projected.Select(static segment => segment.CompactDetail).Distinct(StringComparer.Ordinal));
        string addressRange = FormattableString.Invariant($"0x{start:X5}-0x{endExclusive - 1:X5}");
        string length = FormattableString.Invariant($"len 0x{endExclusive - start:X}");
        return new MemoryCoverageSegmentViewModel(
            $"{addressRange} ({length})",
            primary.SourceLabel,
            detail,
            primary.FillRole,
            projected.Sum(static segment => segment.BarWidth),
            disposition: primary.Disposition,
            observedChange: projected.Any(static segment => segment.IsChanged)
                ? MemoryObservedChange.Changed
                : MemoryObservedChange.NotObserved,
            diagnosticSeverity: projected.Max(static segment => segment.DiagnosticSeverity),
            usesBaseFirmwarePattern: projected.All(static segment => segment.UsesKeptPattern),
            regionId: primary.RegionId,
            sourceSlotId: primary.SourceSlotId,
            logicalSourceLabel: primary.LogicalSourceLabel,
            text: text,
            regionGroup: primary.RegionGroup,
            rangeStart: start,
            rangeEndExclusive: endExclusive,
            addressRangeLabel: addressRange,
            lengthLabel: length,
            compactDetail: detail,
            changeLabel: isPartial ? text.GetMemoryCoveragePartialReplaceLabel() : null);
    }
}
