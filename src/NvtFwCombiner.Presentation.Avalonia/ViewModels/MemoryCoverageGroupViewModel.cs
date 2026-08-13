using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MemoryCoverageGroupViewModel : ObservableObject
{
    private readonly ShellTextResources _text;
    public MemoryCoverageGroupViewModel(
        string title,
        string summary,
        IEnumerable<MemoryCoverageSegmentViewModel> segments,
        bool isExpanded,
        ReplaceRegionGroup regionGroup,
        ShellTextResources text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(segments);

        Title = title;
        Summary = summary;
        Segments = [.. segments];
        IsExpanded = isExpanded;
        RegionGroup = regionGroup;
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title { get; }

    public string Summary { get; }

    public ObservableCollection<MemoryCoverageSegmentViewModel> Segments { get; }

    public int SegmentCount => Segments.Count;

    public ReplaceRegionGroup RegionGroup { get; }

    public int ChangedCount => Segments.Count(segment => segment.IsChanged);

    /// <summary>Compact changed/total count shown in collapsed headers.</summary>
    public string CountLabel => IsBaseFirmwareGroup ? $"{SegmentCount}" : $"{ChangedCount}/{SegmentCount}";

    public string ChangeSummary => _text.FormatCoverageChangeSummary(
        IsBaseFirmwareGroup,
        ChangedCount,
        SegmentCount);

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private bool IsBaseFirmwareGroup => RegionGroup == ReplaceRegionGroup.Base;
}
