using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Reusable firmware input slot card with browse and drag/drop file selection.</summary>
public sealed partial class FirmwareSlotCard : UserControl
{
    /// <summary>Defines the localized browse button label.</summary>
    public static readonly StyledProperty<string> BrowseLabelProperty =
        AvaloniaProperty.Register<FirmwareSlotCard, string>(nameof(BrowseLabel), "Browse");

    /// <summary>Gets or sets the localized browse button label.</summary>
    public string BrowseLabel
    {
        get => GetValue(BrowseLabelProperty);
        set => SetValue(BrowseLabelProperty, value);
    }

    /// <summary>Initializes the firmware slot card.</summary>
    public FirmwareSlotCard()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ShellViewModel =>
        WorkbenchShellViewModelLocator.Find(this);

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Control { DataContext: FirmwareSlotViewModel { CanSelectFile: false } })
        {
            DropZoneDragState.SetActive(sender, isActive: false);
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
    }

    private async void SlotDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);

        if (sender is not Control
            {
                Tag: string slotId,
                DataContext: FirmwareSlotViewModel { CanSelectFile: true },
            } ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.WorkflowSession.SetSlotFileAsync(slotId, path);
        }
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control
            {
                Tag: string slotId,
                DataContext: FirmwareSlotViewModel { CanSelectFile: true },
            } ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        string? path = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            topLevel.StorageProvider,
            "Select BIN file");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.WorkflowSession.SetSlotFileAsync(slotId, path);
        }
    }
}
