namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Small badge shown on dense report evidence rows.</summary>
public sealed class ReportLineBadgeViewModel
{
    /// <summary>Creates a badge.</summary>
    public ReportLineBadgeViewModel(string text)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>Badge text.</summary>
    public string Text { get; }
}
