namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Small badge shown on dense report evidence rows.</summary>
internal sealed class ReportLineBadgeViewModel(string text)
{
    public string Text { get; } = text ?? string.Empty;
}
