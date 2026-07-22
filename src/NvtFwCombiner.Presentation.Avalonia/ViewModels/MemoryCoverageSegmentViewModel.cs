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
        bool isChanged = false,
        bool usesBaseFirmwarePattern = false,
        string? regionId = null)
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
        Fill = fill;
        FillBrush = Brush.Parse(fill);
        BarWidth = barWidth;
        IsChanged = isChanged;
        UsesBaseFirmwarePattern = usesBaseFirmwarePattern;
        RegionId = string.IsNullOrWhiteSpace(regionId) ? null : regionId;
        ChangeLabel = isChanged ? "Changed" : "Kept";
    }

    /// <summary>Address range in half-open hex notation.</summary>
    public string RangeLabel { get; }

    /// <summary>Final source occupying this range.</summary>
    public string SourceLabel { get; }

    /// <summary>Short display note for this segment.</summary>
    public string Detail { get; }

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

    /// <summary>True when retained base-firmware bytes need a non-color visual pattern.</summary>
    public bool UsesBaseFirmwarePattern { get; }

    /// <summary>Profile-owned selection identity for a replaceable physical region, when present.</summary>
    public string? RegionId { get; }

    /// <summary>Compact changed/kept label for the legend.</summary>
    public string ChangeLabel { get; }

    private static string CreateCompactDetail(string sourceLabel, bool isChanged)
    {
        return sourceLabel switch
        {
            "Reserved" => "Output range remains reserved; no input writes it.",
            "DP length pending" => "Output range will follow the selected DP BIN length.",
            "Reference FlashCode required" => "Output range will follow the selected Reference FlashCode length.",
            "Unsupported reference" => "This Reference FlashCode length is blocked by profile policy.",
            string label when label.Equals("Preserve", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("Base flash", StringComparison.OrdinalIgnoreCase) =>
                "Output range keeps bytes from the base firmware.",
            string label when label.Contains("Restored", StringComparison.OrdinalIgnoreCase) =>
                "Output range restores bytes from the base firmware.",
            _ => isChanged
                ? $"Output range is written from {FormatSourcePhrase(sourceLabel)}."
                : $"Output range uses bytes from {FormatSourcePhrase(sourceLabel)}.",
        };
    }

    private static string FormatSourcePhrase(string sourceLabel)
    {
        return sourceLabel.StartsWith("Changed ", StringComparison.OrdinalIgnoreCase)
            ? sourceLabel["Changed ".Length..]
            : sourceLabel;
    }

    private static string NormalizeSourceLabel(string sourceLabel)
    {
        return sourceLabel.StartsWith("Blank", StringComparison.OrdinalIgnoreCase)
            ? "Reserved"
            : sourceLabel;
    }
}
