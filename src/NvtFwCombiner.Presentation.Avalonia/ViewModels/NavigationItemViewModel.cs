namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Navigation item displayed by the planning shell sidebar.</summary>
public sealed class NavigationItemViewModel
{
    /// <summary>Initializes a sidebar navigation item.</summary>
    /// <param name="label">Visible navigation label.</param>
    public NavigationItemViewModel(string label)
    {
        Label = label;
    }

    /// <summary>Gets the visible navigation label.</summary>
    public string Label { get; }
}
