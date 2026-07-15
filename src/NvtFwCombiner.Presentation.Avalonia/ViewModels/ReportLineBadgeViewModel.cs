namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Small badge shown on dense report evidence rows.</summary>
public sealed class ReportLineBadgeViewModel(string text)
{
    /// <summary>Badge text.</summary>
    public string Text { get; } = text ?? string.Empty;
}
