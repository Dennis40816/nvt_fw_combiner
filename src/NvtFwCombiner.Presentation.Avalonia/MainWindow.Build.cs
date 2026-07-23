using Avalonia.Interactivity;
using NvtFwCombiner.Bootstrap;
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

        MergeBuildSavePreparation? preparation = await viewModel.TryPrepareMergeBuildSaveAsync(
            CancellationToken.None);
        if (preparation is null)
        {
            return;
        }

        WorkbenchAbAFlashCodeDeliveryPlan? aFlashCodePlan = preparation.AFlashCodePlan;
        bool exportAFlashCode = aFlashCodePlan is not null &&
            await viewModel.PromptForAbAFlashCodeDeliveryAsync();
        string? outputPath = await FirmwareFilePickerDialogs.PickMergedFirmwareOutputPathAsync(
            StorageProvider,
            preparation.SuggestedFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        string? aFlashCodeOutputPath = null;
        if (exportAFlashCode)
        {
            aFlashCodeOutputPath = await FirmwareFilePickerDialogs.PickAbAFlashCodeOutputPathAsync(
                StorageProvider,
                aFlashCodePlan!.SuggestedFileName);
            if (string.IsNullOrWhiteSpace(aFlashCodeOutputPath))
            {
                return;
            }
        }

        await viewModel.BuildMergeAsync(outputPath, aFlashCodeOutputPath);
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
