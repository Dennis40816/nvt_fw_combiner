using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes the main window controls.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = ShellViewModelFactory.Create();
    }

    private async void LoadReportJsonButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load run report JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Run report JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
            ],
        });

        if (files.Count == 0 || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await using Stream stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();
        viewModel.LoadReportJson(json, files[0].Name);
    }

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void SlotDrop_OnDrop(object? sender, DragEventArgs e)
    {
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
}
