namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Read-only planning card content for the demo shell.</summary>
public sealed class PlanningCardViewModel
{
    /// <summary>Initializes a planning card.</summary>
    /// <param name="title">Card title.</param>
    /// <param name="subtitle">Card subtitle.</param>
    /// <param name="rows">Card detail rows.</param>
    /// <param name="status">Card status row.</param>
    public PlanningCardViewModel(string title, string subtitle, IReadOnlyList<string> rows, string status)
    {
        Title = title;
        Subtitle = subtitle;
        Rows = rows;
        Status = status;
    }

    /// <summary>Gets the card title.</summary>
    public string Title { get; }

    /// <summary>Gets the secondary card text.</summary>
    public string Subtitle { get; }

    /// <summary>Gets the card detail rows.</summary>
    public IReadOnlyList<string> Rows { get; }

    /// <summary>Gets the card status row.</summary>
    public string Status { get; }
}
