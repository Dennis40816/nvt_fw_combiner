// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class PlanningCardText(
    string title,
    string subtitle,
    IReadOnlyList<string> rows,
    string status)
{
    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public IReadOnlyList<string> Rows { get; } = rows;

    public string Status { get; } = status;
}

#pragma warning restore CS1591
