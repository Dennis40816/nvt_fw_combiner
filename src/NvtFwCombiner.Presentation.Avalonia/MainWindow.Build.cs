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

        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        CaptureOutputDeliveryReturnFocus(viewModel, sender);
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
            _ = await viewModel.Replace.RequestCtrlRamBuildSettingsAsync();
            CaptureOutputDeliveryReturnFocus(viewModel, sender);
            return;
        }

        await viewModel.Replace.RequestBuildOutputDeliveryAsync();
        CaptureOutputDeliveryReturnFocus(viewModel, sender);
    }
}
