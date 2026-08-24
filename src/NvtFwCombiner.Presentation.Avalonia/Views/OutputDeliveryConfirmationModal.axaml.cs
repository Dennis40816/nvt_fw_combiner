using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Shared accessible pre-delivery confirmation for all composition Builds.</summary>
public sealed partial class OutputDeliveryConfirmationModal : UserControl
{
    /// <summary>Retained open state used for focus entry and safe focus restoration.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<OutputDeliveryConfirmationModal, bool>(nameof(IsOpen));

    private IInputElement? _returnFocus;
    private Func<bool>? _canRestoreFocus;
    private int _confirmationInProgress;
    private bool _isFocusRestorePending;

    /// <summary>Initializes the generated accessible confirmation view.</summary>
    public OutputDeliveryConfirmationModal()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        PropertyChanged += OutputDeliveryConfirmationModal_OnPropertyChanged;
    }

    /// <summary>Gets or sets whether the retained Build Settings surface is active.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (IsOpen)
        {
            EnterModal();
        }
    }

    private void OutputDeliveryConfirmationModal_OnPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
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
            _isFocusRestorePending = true;
            Dispatcher.UIThread.Post(RestoreFocusAfterClose, DispatcherPriority.Input);
        }
    }

    internal void CaptureReturnFocus(IInputElement returnFocus, Func<bool>? canRestoreFocus = null)
    {
        _returnFocus = returnFocus ?? throw new ArgumentNullException(nameof(returnFocus));
        _canRestoreFocus = canRestoreFocus;
        _isFocusRestorePending = false;
    }

    internal bool TryBeginConfirmation()
    {
        return Interlocked.CompareExchange(ref _confirmationInProgress, 1, 0) == 0;
    }

    internal void EndConfirmation()
    {
        _ = Interlocked.Exchange(ref _confirmationInProgress, 0);
        RetryPendingFocusRestore();
    }

    internal void RetryPendingFocusRestore()
    {
        if (!IsOpen && _isFocusRestorePending)
        {
            Dispatcher.UIThread.Post(RestoreFocusAfterClose, DispatcherPriority.Input);
        }
    }

    private void EnterModal()
    {
        _returnFocus ??= TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        Dispatcher.UIThread.Post(
            () => _ = CancelButton.Focus(NavigationMethod.Tab),
            DispatcherPriority.Input);
    }

    private void RestoreFocusAfterClose()
    {
        if (!_isFocusRestorePending ||
            Volatile.Read(ref _confirmationInProgress) != 0 ||
            _canRestoreFocus?.Invoke() == false)
        {
            return;
        }

        if (_returnFocus?.Focus() == true)
        {
            _isFocusRestorePending = false;
            _returnFocus = null;
            _canRestoreFocus = null;
        }
    }

    private void BundleToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OutputDeliveryConfirmationViewModel viewModel)
        {
            viewModel.SetBundleEnabled(BundleToggle.IsChecked == true);
        }
    }

    private void AdditionalToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OutputDeliveryConfirmationViewModel viewModel)
        {
            viewModel.SetAdditionalDeliveryEnabled(AdditionalToggle.IsChecked == true);
        }
    }

    private void SourcesDisclosureToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OutputDeliveryConfirmationViewModel viewModel)
        {
            viewModel.SetSourcesExpanded(SourcesDisclosureToggle.IsChecked == true);
        }
    }

    private void FolderNameInput_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is OutputDeliveryConfirmationViewModel viewModel)
        {
            viewModel.SetBundleFolderName(FolderNameInput.Text ?? string.Empty);
        }
    }

    private void OutputFileNameInput_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is OutputDeliveryConfirmationViewModel viewModel)
        {
            viewModel.SetOutputFileName(OutputFileNameInput.Text ?? string.Empty);
        }
    }

    private void EditOutputFileNameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OutputDeliveryConfirmationViewModel viewModel)
        {
            return;
        }

        viewModel.BeginOutputFileNameEdit();
        Dispatcher.UIThread.Post(
            () =>
            {
                if (viewModel.IsOutputFileNameEditing)
                {
                    _ = OutputFileNameInput.Focus(NavigationMethod.Tab);
                    OutputFileNameInput.SelectAll();
                }
            },
            DispatcherPriority.Input);
    }

    private void EditBundleDestinationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OutputDeliveryConfirmationViewModel viewModel)
        {
            return;
        }

        viewModel.BeginBundleDestinationEdit();
        Dispatcher.UIThread.Post(
            () =>
            {
                if (viewModel.IsBundleDestinationEditing)
                {
                    _ = FolderNameInput.Focus(NavigationMethod.Tab);
                    FolderNameInput.SelectAll();
                }
            },
            DispatcherPriority.Input);
    }

    private void CompleteBundleDestinationEditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OutputDeliveryConfirmationViewModel viewModel)
        {
            return;
        }

        viewModel.CompleteBundleDestinationEdit();
        Dispatcher.UIThread.Post(
            () => _ = EditBundleDestinationButton.Focus(NavigationMethod.Tab),
            DispatcherPriority.Input);
    }

    private async void ChooseParentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OutputDeliveryConfirmationViewModel viewModel ||
            TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? directory = await FirmwareFilePickerDialogs.PickBundleParentDirectoryAsync(
            storageProvider,
            viewModel.Text.OutputDeliveryChooseParentLabel);
        if (directory is not null)
        {
            viewModel.SetParentDirectory(directory);
        }
    }

    private async void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OutputDeliveryConfirmationViewModel viewModel)
        {
            return;
        }

        if (!TryBeginConfirmation())
        {
            return;
        }

        ConfirmButton.IsEnabled = false;
        try
        {
            await ConfirmOnceAsync(viewModel);
        }
        finally
        {
            ConfirmButton.IsEnabled = viewModel.CanConfirm;
            EndConfirmation();
        }
    }

    private async Task ConfirmOnceAsync(OutputDeliveryConfirmationViewModel viewModel)
    {

        if (viewModel.BundleEnabled)
        {
            await viewModel.ConfirmBundleAsync();
            return;
        }

        if (!await viewModel.PrepareModeSpecificAsync())
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? outputPath = viewModel.IsReplaceOutput
            ? await FirmwareFilePickerDialogs.PickReplacedFirmwareOutputPathAsync(
                storageProvider,
                viewModel.OutputFileName)
            : await FirmwareFilePickerDialogs.PickMergedFirmwareOutputPathAsync(
                storageProvider,
                viewModel.OutputFileName);
        if (outputPath is null)
        {
            return;
        }

        string? additionalPath = null;
        if (viewModel.AdditionalDeliveryEnabled && !viewModel.BundleEnabled)
        {
            additionalPath = await FirmwareFilePickerDialogs.PickAbAFlashCodeOutputPathAsync(
                storageProvider,
                viewModel.AdditionalSuggestedFileName);
            if (additionalPath is null)
            {
                return;
            }
        }

        bool primaryAutomatic =
            viewModel.OutputFileNameUsesAutomaticName &&
            StringComparer.Ordinal.Equals(Path.GetFileName(outputPath), viewModel.OutputFileName);
        bool additionalAutomatic = additionalPath is not null &&
            StringComparer.Ordinal.Equals(
                Path.GetFileName(additionalPath),
                viewModel.AdditionalSuggestedFileName);
        await viewModel.ConfirmLooseAsync(
            outputPath,
            additionalPath,
            primaryAutomatic,
            additionalAutomatic,
            prepareModeSpecific: false);
    }

    private void OutputDeliveryConfirmationModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is OutputDeliveryConfirmationViewModel viewModel)
        {
            viewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
