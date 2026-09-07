using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Windows.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms selection clearing before the typed AB Dummy DP transition.</summary>
public sealed partial class AbDummyDpConfirmationModal : UserControl
{
    private ICommand? _returnFocusCommand;
    /// <summary>Retained open state, independent of the lazy host's visibility.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AbDummyDpConfirmationModal, bool>(nameof(IsOpen));

    /// <summary>Gets or sets whether this confirmation currently owns keyboard focus.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Initializes the shared-style confirmation and keyboard focus behavior.</summary>
    public AbDummyDpConfirmationModal()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => FocusCancel();
        DetachedFromVisualTree += (_, _) => ClearReturnFocus();
        PropertyChanged += OnModalPropertyChanged;
    }

    private void OnModalPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DataContextProperty) { ClearReturnFocus(); }
        if (e.Property != IsOpenProperty) { return; }
        ClearReturnFocus();
        if (IsOpen) { FocusCancel(); }
        else if (DataContext is MergePresentationViewModel viewModel)
        {
            _returnFocusCommand = viewModel.ToggleAbDummyDpCommand;
            _returnFocusCommand.CanExecuteChanged += OnReturnFocusAvailabilityChanged;
            QueueReturnFocus();
        }
    }

    private void OnReturnFocusAvailabilityChanged(object? sender, EventArgs e)
    {
        QueueReturnFocus();
    }

    private void QueueReturnFocus()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_returnFocusCommand is null || IsOpen) { return; }
            if (TopLevel.GetTopLevel(this) is not Window window ||
                window.DataContext is not MainWindowViewModel { IsCompositionActionRailVisible: true })
            {
                ClearReturnFocus();
                return;
            }
            CheckBox? option = window.GetVisualDescendants().OfType<CheckBox>()
                .FirstOrDefault(control => control.Name == "AbDummyDpCheckBox" && control.IsEffectivelyVisible);
            if (option is null) { ClearReturnFocus(); }
            else if (option.IsEffectivelyEnabled && option.Focus()) { ClearReturnFocus(); }
        }, DispatcherPriority.Input);
    }

    private void ClearReturnFocus()
    {
        if (_returnFocusCommand is null) { return; }
        _returnFocusCommand.CanExecuteChanged -= OnReturnFocusAvailabilityChanged;
        _returnFocusCommand = null;
    }

    private void FocusCancel()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen) { _ = CancelButton.Focus(NavigationMethod.Tab); }
        }, DispatcherPriority.Input);
    }

    private void OnModalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MergePresentationViewModel viewModel) { return; }
        viewModel.CancelAbDummyDpCommand.Execute(null);
        e.Handled = true;
    }
}
