namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Top-level navigation item displayed by the planning shell.</summary>
public sealed class NavigationItemViewModel
{
    /// <summary>Initializes a top-level navigation item.</summary>
    /// <param name="label">Visible navigation label.</param>
    public NavigationItemViewModel(string label)
    {
        Label = label;
    }

    /// <summary>Gets the visible navigation label.</summary>
    public string Label { get; }
}
