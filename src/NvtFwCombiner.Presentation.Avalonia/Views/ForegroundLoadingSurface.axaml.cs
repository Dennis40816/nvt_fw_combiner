using Avalonia.Controls;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Input-blocking wrapper around the reusable foreground status surface.</summary>
public sealed partial class ForegroundLoadingSurface : UserControl
{
    /// <summary>Initializes the foreground operation surface.</summary>
    public ForegroundLoadingSurface()
    {
        InitializeComponent();
    }
}
