using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private const string DropZoneDragActiveClass = "dragActive";
    private void DropZone_OnDragEnter(object? sender, DragEventArgs e)
    {
        bool canDrop = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropZoneDragActive(sender, canDrop);
    }

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        bool canDrop = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropZoneDragActive(sender, canDrop);
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);
    }

    private void SlotDrop_OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);

        if (sender is not Control { Tag: string slotId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetSlotFile(slotId, path);
        }
    }

    private void GeneralMappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);

        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mappingId, path);
        }
    }

    private void GeneralMergeMappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);

        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mappingId, path);
        }
    }

    private static void SetDropZoneDragActive(object? sender, bool isActive)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (isActive)
        {
            if (!control.Classes.Contains(DropZoneDragActiveClass))
            {
                control.Classes.Add(DropZoneDragActiveClass);
            }
        }
        else
        {
            _ = control.Classes.Remove(DropZoneDragActiveClass);
        }
    }

    private async void BrowseSlotButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string slotId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select BIN file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetSlotFile(slotId, path);
        }
    }

    private async void BrowseGeneralMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select replacement BIN",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
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

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select source BIN",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
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
