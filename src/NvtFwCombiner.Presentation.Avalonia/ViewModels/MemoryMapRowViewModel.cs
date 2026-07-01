namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One readable before/after memory-map row shown on Merge and Replace pages.</summary>
public sealed class MemoryMapRowViewModel
{
    /// <summary>Creates a memory-map display row.</summary>
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
        BeforeSource = beforeSource;
        ActionLabel = actionLabel;
        AfterSource = afterSource;
        Detail = detail;
    }

    /// <summary>Address range in inclusive hex notation.</summary>
    public string RangeLabel { get; }

    /// <summary>Source or state before the workflow operation.</summary>
    public string BeforeSource { get; }

    /// <summary>Short operation label such as Copy, Replace, or Preserve.</summary>
    public string ActionLabel { get; }

    /// <summary>Source or state after the workflow operation.</summary>
    public string AfterSource { get; }

    /// <summary>Compact before/after source summary.</summary>
    public string FlowLabel => $"{BeforeSource} -> {AfterSource}";

    /// <summary>Short evidence or policy note.</summary>
    public string Detail { get; }
}
