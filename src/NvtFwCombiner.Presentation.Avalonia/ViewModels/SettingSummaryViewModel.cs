namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class SettingSummaryViewModel
{
    public SettingSummaryViewModel(
        string title,
        string value,
        string description,
        string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Title = title;
        Value = value;
        Description = description;
        Status = status;
    }

    public string Title { get; }

    public string Value { get; }

    public string Description { get; }

    public string Status { get; }
}
