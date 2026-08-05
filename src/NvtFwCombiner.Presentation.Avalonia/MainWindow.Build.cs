using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private async void BuildMergeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.Merge.CanBuildMerge)
        {
            return;
        }

        await viewModel.WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync();
        if (!viewModel.Merge.CanBuildMerge)
        {
            if (viewModel.Merge.IsAbCodeMergeModeSelected)
            {
                _ = await viewModel.Merge.TryPrepareMergeBuildSaveAsync(CancellationToken.None);
            }

            return;
        }

        MergeBuildSavePreparation? preparation = await viewModel.Merge.TryPrepareMergeBuildSaveAsync(
            CancellationToken.None);
        if (preparation is null)
        {
            return;
        }

        WorkbenchAbAFlashCodeDeliveryPlan? aFlashCodePlan = preparation.AFlashCodePlan;
        bool exportAFlashCode = aFlashCodePlan is not null &&
            await viewModel.Merge.PromptForAbAFlashCodeDeliveryAsync();
        string? outputPath = await FirmwareFilePickerDialogs.PickMergedFirmwareOutputPathAsync(
            StorageProvider,
            preparation.SuggestedFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }
        bool outputPathUsesAutomaticName = viewModel.Merge.IsAbCodeMergeModeSelected &&
            string.Equals(
                Path.GetFileName(outputPath),
                preparation.SuggestedFileName,
                StringComparison.Ordinal);

        string? aFlashCodeOutputPath = null;
        bool aFlashCodeOutputPathUsesAutomaticName = false;
        if (exportAFlashCode)
        {
            aFlashCodeOutputPath = await FirmwareFilePickerDialogs.PickAbAFlashCodeOutputPathAsync(
                StorageProvider,
                aFlashCodePlan!.SuggestedFileName);
            if (string.IsNullOrWhiteSpace(aFlashCodeOutputPath))
            {
                return;
            }
            aFlashCodeOutputPathUsesAutomaticName = string.Equals(
                Path.GetFileName(aFlashCodeOutputPath),
                aFlashCodePlan!.SuggestedFileName,
                StringComparison.Ordinal);
        }

        await viewModel.Merge.BuildMergeAsync(
            outputPath,
            aFlashCodeOutputPath,
            outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName);
    }

    private async void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.Replace.CanBuildReplace)
        {
            return;
        }

        await viewModel.Replace.RefreshSelectedFirmwareInspectionsAsync();
        if (!viewModel.Replace.CanBuildReplace)
        {
            return;
        }

        if (viewModel.Replace.IsCtrlRamReplaceModeSelected)
        {
            _ = await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync();
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickReplacedFirmwareOutputPathAsync(
            StorageProvider,
            viewModel.Replace.ReplaceOutputFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.Replace.BuildReplaceAsync(outputPath);
    }
}
