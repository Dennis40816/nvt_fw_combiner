namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One readable before/after memory-map row shown on Merge and Replace pages.</summary>
internal sealed class MemoryMapRowViewModel
{
    public MemoryMapRowViewModel(
        string rangeLabel,
        string beforeSource,
        string actionLabel,
        string afterSource,
        string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        RangeLabel = rangeLabel;
        BeforeSource = NormalizeSource(beforeSource);
        ActionLabel = actionLabel;
        AfterSource = NormalizeSource(afterSource);
        PrimaryLabel = ToPrimaryLabel(AfterSource);
        Detail = detail;
    }

    /// <summary>Address range or symbolic range label for the displayed plan row.</summary>
    public string RangeLabel { get; }

    /// <summary>Source or state before the workflow operation.</summary>
    public string BeforeSource { get; }

    /// <summary>Short operation label such as Copy, Replace, or Preserve.</summary>
    public string ActionLabel { get; }

    /// <summary>Source or state after the workflow operation.</summary>
    public string AfterSource { get; }

    public string PrimaryLabel { get; }

    /// <summary>Compact before/after source summary.</summary>
    public string FlowLabel => $"{BeforeSource} -> {AfterSource}";

    /// <summary>Short evidence or policy note.</summary>
    public string Detail { get; }

    private static string NormalizeSource(string source)
    {
        return source.StartsWith("Blank", StringComparison.OrdinalIgnoreCase)
            ? "Reserved"
            : source;
    }

    private static string ToPrimaryLabel(string source)
    {
        string label = Path.GetFileNameWithoutExtension(source)
            .Replace('_', ' ')
            .Replace('-', ' ');
        return label.Replace("Ctrlram", "CtrlRAM", StringComparison.OrdinalIgnoreCase);
    }
}
