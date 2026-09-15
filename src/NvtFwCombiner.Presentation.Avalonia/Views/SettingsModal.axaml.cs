using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Application-level preferences and support surface that preserves workflow page state.</summary>
public sealed partial class SettingsModal : UserControl
{
    /// <summary>Canonical application-modal open state owned by the shell ViewModel.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<SettingsModal, bool>(nameof(IsOpen));

    private IInputElement? _returnFocus;
    private TopLevel? _owningTopLevel;
    private SettingsViewModel? _settings;
    private IInputElement? _confirmationReturnFocus;

    /// <summary>Initializes the generated view.</summary>
    public SettingsModal()
    {
        InitializeComponent();
        AttachedToVisualTree += SettingsModal_OnAttachedToVisualTree;
        DetachedFromVisualTree += SettingsModal_OnDetachedFromVisualTree;
        PropertyChanged += SettingsModal_OnPropertyChanged;
        DataContextChanged += (_, _) => ObserveSettings();
    }

    /// <summary>Gets or sets whether the retained modal content is currently active.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private void SettingsModal_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _owningTopLevel = TopLevel.GetTopLevel(this);
        ObserveSettings();
        // A removed chip can leave no focused descendant to bubble Escape through this modal.
        _owningTopLevel?.AddHandler(KeyDownEvent, SettingsModal_OnKeyDown, RoutingStrategies.Bubble);
        if (IsOpen)
        {
            EnterModal();
        }
    }

    private void SettingsModal_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _owningTopLevel?.RemoveHandler(KeyDownEvent, SettingsModal_OnKeyDown);
        _owningTopLevel = null;
        _settings?.PropertyChanged -= Settings_OnPropertyChanged;
        _settings = null;
        _confirmationReturnFocus = null;
    }

    private void ObserveSettings()
    {
        _settings?.PropertyChanged -= Settings_OnPropertyChanged;
        _settings = VisualRoot is not null ? (DataContext as MainWindowViewModel)?.Settings : null;
        _settings?.PropertyChanged += Settings_OnPropertyChanged;
    }

    private void Settings_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.IsEventBufferFormatCloseConfirmationOpen) || !IsOpen)
        {
            return;
        }
        bool open = _settings!.IsEventBufferFormatCloseConfirmationOpen;
        IInputElement? returnFocus = _confirmationReturnFocus;
        _confirmationReturnFocus = open ? _owningTopLevel?.FocusManager?.GetFocusedElement() : null;
        SettingsHeader.IsEnabled = !open;
        Control[] descendants = [.. this.GetVisualDescendants().OfType<Control>()];
        Control? rail = descendants.FirstOrDefault(control => control.Name == "SettingsNavigationRail");
        _ = rail?.IsEnabled = !open;
        Grid? editor = descendants.OfType<Grid>().FirstOrDefault(control => control.Name == "EventBufferFormatPageRoot");
        if (editor is not null)
        {
            foreach (Control child in editor.Children)
            {
                child.IsEnabled = !open || child.Name == "EventBufferFormatCloseConfirmation";
            }
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || _settings?.IsEventBufferFormatCloseConfirmationOpen != open) { return; }
            if (open)
            {
                _ = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(
                    button => button.Command == _settings.CancelEventBufferFormatCloseCommand)?.Focus(NavigationMethod.Tab);
            }
            else if (returnFocus is not Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control ||
                !control.Focus(NavigationMethod.Tab))
            {
                _ = CloseButton.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Input);
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
            () => _ = CloseButton.Focus(),
            DispatcherPriority.Input);
    }

    private void SettingsModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsOpen || e.Key != Key.Escape || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen)
        {
            viewModel.Settings.CancelEventBufferFormatCloseCommand.Execute(null);
        }
        else
        {
            viewModel.CloseSettingsCommand.Execute(null);
        }
        e.Handled = true;
    }
}
