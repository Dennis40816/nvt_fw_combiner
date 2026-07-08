using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private void DropZone_OnDragEnter(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
    }

    private void GeneralMappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);

        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mappingId, path);
        }
    }

    private void GeneralMergeMappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);

        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mappingId, path);
        }
    }

    private async void BrowseGeneralMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            StorageProvider,
            "Select replacement BIN");
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mappingId, path);
        }
    }

    private async void BrowseGeneralMergeMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            StorageProvider,
            "Select source BIN");
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mappingId, path);
        }
    }

    private void RemoveGeneralMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: GeneralReplaceMappingViewModel mapping } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveGeneralReplaceMappingRow(mapping);
    }

    private void RemoveGeneralMergeMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: GeneralMergeMappingViewModel mapping } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveGeneralMergeMappingRow(mapping);
    }
}
