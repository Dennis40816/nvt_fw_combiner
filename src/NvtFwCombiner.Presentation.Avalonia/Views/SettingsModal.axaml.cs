using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Application-level preferences and support surface that preserves workflow page state.</summary>
public sealed partial class SettingsModal : UserControl
{
    /// <summary>Canonical application-modal open state owned by the shell ViewModel.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<SettingsModal, bool>(nameof(IsOpen));

    private IInputElement? _returnFocus;

    /// <summary>Initializes the generated view.</summary>
    public SettingsModal()
    {
        InitializeComponent();
        AttachedToVisualTree += SettingsModal_OnAttachedToVisualTree;
        PropertyChanged += SettingsModal_OnPropertyChanged;
    }

    /// <summary>Gets or sets whether the retained modal content is currently active.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private void SettingsModal_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (IsOpen)
        {
            EnterModal();
        }
    }

    private void SettingsModal_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsOpenProperty || VisualRoot is null)
        {
            return;
        }

        if (IsOpen)
        {
            EnterModal();
        }
        else
        {
            Dispatcher.UIThread.Post(
                RestoreFocusAfterClose,
                DispatcherPriority.Input);
        }
    }

    private void RestoreFocusAfterClose()
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.CanRestoreSettingsFocus)
        {
            _ = _returnFocus?.Focus();
        }
    }

    private void EnterModal()
    {
        _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        Dispatcher.UIThread.Post(
            () => _ = CloseButton.Focus(NavigationMethod.Tab),
            DispatcherPriority.Input);
    }

    private void SettingsModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.CloseSettingsCommand.Execute(null);
        e.Handled = true;
    }
}
