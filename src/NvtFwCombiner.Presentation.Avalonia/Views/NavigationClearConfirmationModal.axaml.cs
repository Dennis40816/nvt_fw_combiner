using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms clearing composition input selections before page navigation.</summary>
public sealed partial class NavigationClearConfirmationModal : UserControl
{
    /// <summary>Initializes the generated Avalonia view.</summary>
    public NavigationClearConfirmationModal()
    {
        InitializeComponent();
        AttachedToVisualTree += NavigationClearConfirmationModal_OnAttachedToVisualTree;
    }

    private void NavigationClearConfirmationModal_OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        Dispatcher.UIThread.Post(
            () => _ = CancelButton.Focus(),
            DispatcherPriority.Input);
    }

    private void NavigationClearConfirmationModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.CancelNavigationClearCommand.Execute(null);
        e.Handled = true;
    }
}
