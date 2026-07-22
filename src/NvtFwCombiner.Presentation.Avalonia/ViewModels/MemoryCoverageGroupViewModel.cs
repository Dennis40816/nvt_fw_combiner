using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Collapsible memory coverage group for repeated region families.</summary>
public sealed partial class MemoryCoverageGroupViewModel : ObservableObject
{
    /// <summary>Creates a memory coverage group.</summary>
    public MemoryCoverageGroupViewModel(
        string title,
        string summary,
        IEnumerable<MemoryCoverageSegmentViewModel> segments,
        bool isExpanded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(segments);

        Title = title;
        Summary = summary;
        Segments = [.. segments];
        IsExpanded = isExpanded;
    }

    /// <summary>Group label shown in the expander header.</summary>
    public string Title { get; }

    /// <summary>Plain-language group summary.</summary>
    public string Summary { get; }

    /// <summary>Segments inside this group.</summary>
    public ObservableCollection<MemoryCoverageSegmentViewModel> Segments { get; }

    /// <summary>Number of memory segments in this group.</summary>
    public int SegmentCount => Segments.Count;

    /// <summary>Number of coverage segments written by the active operation type.</summary>
    public int ChangedCount => Segments.Count(segment => segment.IsChanged);

    /// <summary>Compact changed/total count shown in collapsed headers.</summary>
    public string CountLabel => IsBaseFirmwareGroup ? $"{SegmentCount}" : $"{ChangedCount}/{SegmentCount}";

    /// <summary>Plain-language group summary that is quick to scan.</summary>
    public string ChangeSummary => IsBaseFirmwareGroup
        ? "Kept from base firmware."
        : $"{ChangedCount} selected / {SegmentCount} areas.";

    /// <summary>True when the group is expanded in the UI.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private bool IsBaseFirmwareGroup => Title.StartsWith("Base firmware", StringComparison.Ordinal);
}
