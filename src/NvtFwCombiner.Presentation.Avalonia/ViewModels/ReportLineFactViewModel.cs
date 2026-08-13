namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Label/value fact shown in report evidence rows.</summary>
internal sealed class ReportLineFactViewModel(
    string label,
    string value,
    bool isTechnical = false)
{
    public string Label { get; } = label ?? string.Empty;

    public string Value { get; } = value ?? string.Empty;

    public bool IsTechnical { get; } = isTechnical;

    public bool IsPlainText { get; } = !isTechnical;
}
