// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Localized text for a planning card.</summary>
public sealed class PlanningCardText(
    string title,
    string subtitle,
    IReadOnlyList<string> rows,
    string status)
{
    /// <summary>Gets the card title.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the card subtitle.</summary>
    public string Subtitle { get; } = subtitle;

    /// <summary>Gets the card detail rows.</summary>
    public IReadOnlyList<string> Rows { get; } = rows;

    /// <summary>Gets the card status.</summary>
    public string Status { get; } = status;
}

#pragma warning restore CS1591
