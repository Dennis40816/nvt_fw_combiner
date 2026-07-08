using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private async void BuildMergeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildMerge)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save merged firmware BIN",
            SuggestedFileName = viewModel.MergeOutputFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.BuildMergeAsync(outputPath);
    }

    private async void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildReplace)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save replaced firmware BIN",
            SuggestedFileName = viewModel.ReplaceOutputFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.BuildReplaceAsync(outputPath);
    }
}
