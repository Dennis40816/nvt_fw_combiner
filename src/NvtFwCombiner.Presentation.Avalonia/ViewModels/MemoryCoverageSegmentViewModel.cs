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

        string displaySourceLabel = NormalizeSourceLabel(sourceLabel);

        RangeLabel = rangeLabel;
        SourceLabel = displaySourceLabel;
        Detail = detail;
        CompactDetail = CreateCompactDetail(displaySourceLabel, isChanged);
        TooltipText = CreateTooltipText(rangeLabel, displaySourceLabel, detail);
        Fill = fill;
        FillBrush = Brush.Parse(fill);
        BarWidth = barWidth;
        IsChanged = isChanged;
        ChangeLabel = isChanged ? "Changed" : "Kept";
        ChangeBadgeBackgroundBrush = Brush.Parse(isChanged ? "#DBEAFE" : "#F8FAFC");
        ChangeBadgeBorderBrush = Brush.Parse(isChanged ? "#BFDBFE" : "#CBD5E1");
        ChangeBadgeForegroundBrush = Brush.Parse(isChanged ? "#1D4ED8" : "#475569");
        OutlineBrush = isChanged ? Brush.Parse("#1D4ED8") : Brushes.Transparent;
        OutlineThickness = new Thickness(isChanged ? 1 : 0);
    }

    /// <summary>Address range in half-open hex notation.</summary>
    public string RangeLabel { get; }

    /// <summary>Final source occupying this range.</summary>
    public string SourceLabel { get; }

    /// <summary>Short display note for this segment.</summary>
    public string Detail { get; }

    /// <summary>Plain-language hover text for the visual strip.</summary>
    public string TooltipText { get; }

    /// <summary>Compact display note for dense workbench legends.</summary>
    public string CompactDetail { get; }

    /// <summary>Brush color used by the visual strip.</summary>
    public string Fill { get; }

    /// <summary>Parsed brush used by Avalonia visual elements.</summary>
    public IBrush FillBrush { get; }

    /// <summary>Proportional display width in device-independent pixels.</summary>
    public double BarWidth { get; }

    /// <summary>True when the segment is written by the active replace operation.</summary>
    public bool IsChanged { get; }

    /// <summary>Compact changed/kept label for the legend.</summary>
    public string ChangeLabel { get; }

    /// <summary>Badge background for changed/kept state.</summary>
    public IBrush ChangeBadgeBackgroundBrush { get; }

    /// <summary>Badge border for changed/kept state.</summary>
    public IBrush ChangeBadgeBorderBrush { get; }

    /// <summary>Badge text brush for changed/kept state.</summary>
    public IBrush ChangeBadgeForegroundBrush { get; }

    /// <summary>Outline brush used to call out changed coverage segments.</summary>
    public IBrush OutlineBrush { get; }

    /// <summary>Outline thickness used to call out changed coverage segments.</summary>
    public Thickness OutlineThickness { get; }

    private static string CreateCompactDetail(string sourceLabel, bool isChanged)
    {
        return sourceLabel switch
        {
            "Reserved" => "No selected input writes this range.",
            string label when label.Equals("Preserve", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("Base flash", StringComparison.OrdinalIgnoreCase) =>
                "Final bytes stay from the base firmware.",
            string label when label.Contains("Restored", StringComparison.OrdinalIgnoreCase) =>
                "Final bytes are restored from the base firmware.",
            _ => isChanged
                ? $"Final bytes are replaced from {sourceLabel}."
                : $"Final bytes come from {sourceLabel}.",
        };
    }

    private static string CreateTooltipText(
        string rangeLabel,
        string sourceLabel,
        string detail)
    {
        return sourceLabel.StartsWith("Blank", StringComparison.OrdinalIgnoreCase)
            ? $"Output {rangeLabel} stays {sourceLabel}; no source input writes this range."
            : $"{sourceLabel}: {rangeLabel}. {detail}";
    }

    private static string NormalizeSourceLabel(string sourceLabel)
    {
        return sourceLabel.StartsWith("Blank", StringComparison.OrdinalIgnoreCase)
            ? "Reserved"
            : sourceLabel;
    }
}
