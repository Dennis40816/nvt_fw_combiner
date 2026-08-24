using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private async void BuildMergeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !await OpenMergeBuildSettingsAsync(viewModel))
        {
            return;
        }

        CaptureOutputDeliveryReturnFocus(viewModel, sender);
    }

    internal static async Task<bool> OpenMergeBuildSettingsAsync(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.Merge.CanBuildMerge)
        {
            return false;
        }

        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        return true;
    }

    private async void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !await OpenReplaceBuildSettingsAsync(viewModel.Replace))
        {
            return;
        }

        CaptureOutputDeliveryReturnFocus(viewModel, sender);
    }

    internal static async Task<bool> OpenReplaceBuildSettingsAsync(ReplacePresentationViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.CanBuildReplace)
        {
            return false;
        }

        if (viewModel.IsCtrlRamReplaceModeSelected)
        {
            return await viewModel.RequestCtrlRamBuildSettingsAsync();
        }

        await viewModel.RequestBuildOutputDeliveryAsync();
        return true;
    }
}
