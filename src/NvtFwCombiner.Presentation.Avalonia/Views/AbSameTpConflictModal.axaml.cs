using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Resolves two existing AB TP selections before explicit linked authoring begins.</summary>
public sealed partial class AbSameTpConflictModal : UserControl
{
    /// <summary>Initializes the generated Avalonia view.</summary>
    public AbSameTpConflictModal()
    {
        InitializeComponent();
        AttachedToVisualTree += AbSameTpConflictModal_OnAttachedToVisualTree;
        PropertyChanged += AbSameTpConflictModal_OnPropertyChanged;
    }

    private void AbSameTpConflictModal_OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        FocusInitialControl();
    }

    private void AbSameTpConflictModal_OnPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible && VisualRoot is not null)
        {
            FocusInitialControl();
        }
    }

    private void FocusInitialControl()
    {
        Dispatcher.UIThread.Post(
            () => _ = CancelButton.Focus(),
            DispatcherPriority.Input);
    }

    private void AbSameTpConflictModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MergePresentationViewModel viewModel)
        {
            return;
        }

        viewModel.CancelAbSameTpConflictCommand.Execute(null);
        e.Handled = true;
    }
}
