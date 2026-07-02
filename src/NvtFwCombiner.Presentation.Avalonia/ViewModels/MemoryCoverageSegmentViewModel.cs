using Avalonia;
using Avalonia.Media;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One visual segment in a memory coverage strip.</summary>
public sealed class MemoryCoverageSegmentViewModel
{
    /// <summary>Creates a memory coverage segment.</summary>
    public MemoryCoverageSegmentViewModel(
        string rangeLabel,
        string sourceLabel,
        string detail,
        string fill,
        double barWidth,
        bool isChanged = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(fill);

        RangeLabel = rangeLabel;
        SourceLabel = sourceLabel;
        Detail = detail;
        TooltipText = CreateTooltipText(rangeLabel, sourceLabel, detail);
        Fill = fill;
        FillBrush = Brush.Parse(fill);
        BarWidth = barWidth;
        IsChanged = isChanged;
        ChangeLabel = isChanged ? "Changed" : "Preserved";
        OutlineBrush = Brushes.Transparent;
        OutlineThickness = new Thickness(0);
    }

    /// <summary>Address range in half-open hex notation.</summary>
    public string RangeLabel { get; }

    /// <summary>Final source occupying this range.</summary>
    public string SourceLabel { get; }

    /// <summary>Short display note for this segment.</summary>
    public string Detail { get; }

    /// <summary>Plain-language hover text for the visual strip.</summary>
    public string TooltipText { get; }

    /// <summary>Brush color used by the visual strip.</summary>
    public string Fill { get; }

    /// <summary>Parsed brush used by Avalonia visual elements.</summary>
    public IBrush FillBrush { get; }

    /// <summary>Proportional display width in device-independent pixels.</summary>
    public double BarWidth { get; }

    /// <summary>True when the segment is written by the active replace operation.</summary>
    public bool IsChanged { get; }

    /// <summary>Compact changed/preserved label for the legend.</summary>
    public string ChangeLabel { get; }

    /// <summary>Outline brush used to call out changed coverage segments.</summary>
    public IBrush OutlineBrush { get; }

    /// <summary>Outline thickness used to call out changed coverage segments.</summary>
    public Thickness OutlineThickness { get; }

    private static string CreateTooltipText(
        string rangeLabel,
        string sourceLabel,
        string detail)
    {
        return sourceLabel.StartsWith("Blank", StringComparison.OrdinalIgnoreCase)
            ? $"Output {rangeLabel} stays {sourceLabel}; no source input writes this range."
            : $"{sourceLabel}: {rangeLabel}. {detail}";
    }
}
