using Avalonia.Interactivity;
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

        await viewModel.RefreshSelectedMergeFirmwareInspectionsAsync();
        if (!viewModel.CanBuildMerge)
        {
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickMergedFirmwareOutputPathAsync(
            StorageProvider,
            viewModel.MergeOutputFileName);
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

        await viewModel.RefreshSelectedReplaceFirmwareInspectionsAsync();
        if (!viewModel.CanBuildReplace)
        {
            return;
        }

        if (viewModel.IsCtrlRamReplaceModeSelected)
        {
            _ = await viewModel.TryOpenCtrlRamFirmwareVersionModalAsync();
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickReplacedFirmwareOutputPathAsync(
            StorageProvider,
            viewModel.ReplaceOutputFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.BuildReplaceAsync(outputPath);
    }
}
