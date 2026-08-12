using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Reusable input-blocking foreground operation surface with truthful determinate or indeterminate progress.</summary>
public sealed partial class ForegroundLoadingSurface : UserControl
{
    /// <summary>Initializes the foreground operation surface.</summary>
    public ForegroundLoadingSurface()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the visible failure recovery action is invoked.</summary>
    public event EventHandler? RetryRequested;

    private void RetryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }
}
