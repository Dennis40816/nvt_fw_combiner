using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    private readonly HashSet<EventBufferFormatDraftRowViewModel> _observedFormatRows = [];

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

    internal int ObservedEventBufferFormatRowCount => _observedFormatRows.Count;

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
        StopObservingSettings();
        _settings = null;
        _confirmationReturnFocus = null;
    }

    private void ObserveSettings()
    {
        StopObservingSettings();
        _settings = VisualRoot is not null ? (DataContext as MainWindowViewModel)?.Settings : null;
        if (_settings is null)
        {
            return;
        }

        _settings.PropertyChanged += Settings_OnPropertyChanged;
        _settings.ToolchainBrowseRequested += Settings_ToolchainBrowseRequested;
        _settings.EventBufferFormatRows.CollectionChanged += FormatRows_OnCollectionChanged;
        foreach (EventBufferFormatDraftRowViewModel row in _settings.EventBufferFormatRows)
        {
            ObserveFormatRow(row);
        }
    }

    private void StopObservingSettings()
    {
        if (_settings is not null)
        {
            _settings.PropertyChanged -= Settings_OnPropertyChanged;
            _settings.ToolchainBrowseRequested -= Settings_ToolchainBrowseRequested;
            _settings.EventBufferFormatRows.CollectionChanged -= FormatRows_OnCollectionChanged;
        }
        foreach (EventBufferFormatDraftRowViewModel row in _observedFormatRows)
        {
            row.PropertyChanged -= FormatRow_OnPropertyChanged;
        }

        _observedFormatRows.Clear();
    }

    private void FormatRows_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (EventBufferFormatDraftRowViewModel row in _observedFormatRows)
            {
                row.PropertyChanged -= FormatRow_OnPropertyChanged;
            }

            _observedFormatRows.Clear();
            if (_settings is not null)
            {
                foreach (EventBufferFormatDraftRowViewModel row in _settings.EventBufferFormatRows)
                {
                    ObserveFormatRow(row);
                }
            }

            return;
        }

        if (e.OldItems is not null)
        {
            foreach (EventBufferFormatDraftRowViewModel row in e.OldItems)
            {
                row.PropertyChanged -= FormatRow_OnPropertyChanged;
                _ = _observedFormatRows.Remove(row);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (EventBufferFormatDraftRowViewModel row in e.NewItems)
            {
                ObserveFormatRow(row);
            }
        }
    }

    private void ObserveFormatRow(EventBufferFormatDraftRowViewModel row)
    {
        if (!_observedFormatRows.Add(row))
        {
            return;
        }

        row.PropertyChanged += FormatRow_OnPropertyChanged;
    }

    private void FormatRow_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EventBufferFormatDraftRowViewModel.IsAddingRecognitionValue) ||
            sender is not EventBufferFormatDraftRowViewModel row)
        {
            return;
        }

        bool adding = row.IsAddingRecognitionValue;
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || row.IsAddingRecognitionValue != adding)
            {
                return;
            }

            Control? target = adding
                ? this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(control =>
                    ReferenceEquals(control.DataContext, row) &&
                    control.IsEffectivelyVisible &&
                    control.GetValue(AutomationProperties.NameProperty) == row.RecognitionValueLabel)
                : this.GetVisualDescendants().OfType<Button>().FirstOrDefault(control =>
                    ReferenceEquals(control.Command, row.BeginAddRecognitionValueCommand) && control.IsEffectivelyVisible);
            _ = target?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Input);
    }

    private void Settings_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SettingsViewModel.IsEventBufferFormatCloseConfirmationOpen) or
            nameof(SettingsViewModel.IsToolchainCloseConfirmationOpen)) || !IsOpen)
        {
            return;
        }
        bool toolchain = _settings!.IsToolchainCloseConfirmationOpen ||
            e.PropertyName == nameof(SettingsViewModel.IsToolchainCloseConfirmationOpen);
        bool open = _settings.IsEventBufferFormatCloseConfirmationOpen || _settings.IsToolchainCloseConfirmationOpen;
        IInputElement? returnFocus = _confirmationReturnFocus;
        _confirmationReturnFocus = open ? _owningTopLevel?.FocusManager?.GetFocusedElement() : null;
        SettingsHeader.IsEnabled = !open;
        Control[] descendants = [.. this.GetVisualDescendants().OfType<Control>()];
        Control? rail = descendants.FirstOrDefault(control => control.Name == "SettingsNavigationRail");
        _ = rail?.IsEnabled = !open;
        string pageName = toolchain ? "ToolchainPageRoot" : "EventBufferFormatPageRoot";
        string confirmationName = toolchain ? "ToolchainCloseConfirmation" : "EventBufferFormatCloseConfirmation";
        Grid? editor = descendants.OfType<Grid>().FirstOrDefault(control => control.Name == pageName);
        if (editor is not null)
        {
            foreach (Control child in editor.Children)
            {
                child.IsEnabled = !open || child.Name == confirmationName;
            }
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || _settings is null ||
                (_settings.IsEventBufferFormatCloseConfirmationOpen || _settings.IsToolchainCloseConfirmationOpen) != open) { return; }
            if (open)
            {
                _ = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(
                    button => ReferenceEquals(button.Command, toolchain ? _settings.CancelToolchainCloseCommand : _settings.CancelEventBufferFormatCloseCommand))?.Focus(NavigationMethod.Tab);
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
            _settings?.InvalidateToolchainOperations();
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

        if (_owningTopLevel?.FocusManager?.GetFocusedElement() is Control
            {
                DataContext: EventBufferFormatDraftRowViewModel row,
            } && row.IsAddingRecognitionValue)
        {
            row.CancelAddRecognitionValueCommand.Execute(null);
        }
        else if (viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen)
        {
            viewModel.Settings.CancelEventBufferFormatCloseCommand.Execute(null);
        }
        else if (viewModel.Settings.IsToolchainCloseConfirmationOpen)
        {
            viewModel.Settings.CancelToolchainCloseCommand.Execute(null);
        }
        else
        {
            viewModel.CloseSettingsCommand.Execute(null);
        }
        e.Handled = true;
    }

    private async void Settings_ToolchainBrowseRequested(object? sender, EventArgs e)
    {
        SettingsViewModel? settings = _settings;
        if (!IsOpen || settings is null || !settings.CanEditToolchain || _owningTopLevel is null) { return; }
        long operation = settings.ToolchainOperationGeneration;
        IReadOnlyList<IStorageFile> files = [];
        try
        {
            files = await _owningTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = (DataContext as MainWindowViewModel)!.Text.ToolchainBrowseLabel,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Runtime DLL") { Patterns = ["*.dll"] }],
            });
            if (IsOpen && ReferenceEquals(settings, _settings) && operation == settings.ToolchainOperationGeneration &&
                files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            {
                await settings.InspectToolchainPathAsync(path);
            }
        }
        catch (Exception)
        {
            if (IsOpen && ReferenceEquals(settings, _settings) && operation == settings.ToolchainOperationGeneration)
            {
                settings.ReportToolchainBrowseFailure();
            }
        }
        finally { foreach (IStorageFile file in files) { file.Dispose(); } }
    }
}
