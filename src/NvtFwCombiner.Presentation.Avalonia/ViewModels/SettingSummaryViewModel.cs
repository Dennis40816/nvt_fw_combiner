namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact settings status row.</summary>
public sealed class SettingSummaryViewModel
{
    /// <summary>Initializes a settings status row.</summary>
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

    /// <summary>Gets the row title.</summary>
    public string Title { get; }

    /// <summary>Gets the primary value.</summary>
    public string Value { get; }

    /// <summary>Gets the short supporting description.</summary>
    public string Description { get; }

    /// <summary>Gets the row status.</summary>
    public string Status { get; }
}
