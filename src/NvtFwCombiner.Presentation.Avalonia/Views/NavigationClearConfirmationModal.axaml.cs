using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms page navigation or application exit using the shared safe-focus surface.</summary>
public sealed partial class NavigationClearConfirmationModal : UserControl
{
    private IInputElement? _returnFocus;

    /// <summary>Initializes the generated Avalonia view.</summary>
    public NavigationClearConfirmationModal()
    {
        InitializeComponent();
        AttachedToVisualTree += NavigationClearConfirmationModal_OnAttachedToVisualTree;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && VisualRoot is not null)
            {
                UpdateFocus();
            }
        };
    }

    private void NavigationClearConfirmationModal_OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        UpdateFocus();
    }

    private void UpdateFocus()
    {
        if (IsVisible)
        {
            _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Dispatcher.UIThread.Post(() =>
            {
                if (IsEffectivelyVisible)
                {
                    _ = CancelButton.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
        else
        {
            IInputElement? target = _returnFocus;
            _returnFocus = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsVisible && target is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control)
                {
                    _ = control.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
    }

    private void NavigationClearConfirmationModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not ShellNavigationViewModel viewModel)
        {
            return;
        }

        viewModel.CancelNavigationClearCommand.Execute(null);
        e.Handled = true;
    }
}
