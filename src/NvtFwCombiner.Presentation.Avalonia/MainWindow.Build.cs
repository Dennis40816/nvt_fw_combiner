using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private void BuildMergeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UiEventAdapter.Run(this, "MainWindow.BuildMerge", _ => HandleBuildMergeButton_OnClickAsync(sender, e));
    }

    private async Task HandleBuildMergeButton_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        await OpenBuildSettingsAsync(sender, OpenMergeBuildSettingsAsync);
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

    private void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UiEventAdapter.Run(this, "MainWindow.BuildReplace", _ => HandleBuildReplaceButton_OnClickAsync(sender, e));
    }

    private async Task HandleBuildReplaceButton_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        await OpenBuildSettingsAsync(sender, static viewModel => OpenReplaceBuildSettingsAsync(viewModel.Replace));
    }

    internal async Task OpenBuildSettingsAsync(
        object? sender, Func<MainWindowViewModel, Task<bool>> openSettings)
    {
        if (DataContext is MainWindowViewModel viewModel && await openSettings(viewModel))
        {
            CaptureOutputDeliveryReturnFocus(viewModel, sender);
        }
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

        _ = await viewModel.RequestBuildOutputDeliveryAsync();
        return true;
    }
}
