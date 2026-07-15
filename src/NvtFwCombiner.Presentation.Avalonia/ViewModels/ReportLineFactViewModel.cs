namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Label/value fact shown in report evidence rows.</summary>
public sealed class ReportLineFactViewModel(
    string label,
    string value,
    bool isTechnical = false)
{
    /// <summary>Short field label.</summary>
    public string Label { get; } = label ?? string.Empty;

    /// <summary>Field value.</summary>
    public string Value { get; } = value ?? string.Empty;

    /// <summary>True when the value should use fixed-width technical typography.</summary>
    public bool IsTechnical { get; } = isTechnical;

    /// <summary>True when the value should use normal typography.</summary>
    public bool IsPlainText { get; } = !isTechnical;
}
