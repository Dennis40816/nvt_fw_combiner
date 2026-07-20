using Avalonia.Controls;
using Avalonia.Interactivity;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Collects the user-confirmed CtrlRAM Build firmware-version choice before selecting an output path.</summary>
public sealed partial class CtrlRamFirmwareVersionModal : UserControl
{
    /// <summary>Initializes the CtrlRAM firmware-version confirmation view.</summary>
    public CtrlRamFirmwareVersionModal()
    {
        InitializeComponent();
    }

    private async void ConfirmCtrlRamFirmwareVersionBuildButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        (bool succeeded, WorkbenchCtrlRamFirmwareVersionEdit? edit) =
            await viewModel.TryCreateCtrlRamFirmwareVersionEditAsync();
        if (!succeeded || TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        await viewModel.RefreshSelectedReplaceFirmwareInspectionsAsync();
        if (!viewModel.CanBuildReplace ||
            !viewModel.IsCtrlRamReplaceModeSelected ||
            !await viewModel.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync())
        {
            viewModel.CloseCtrlRamFirmwareVersionModal();
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickReplacedFirmwareOutputPathAsync(
            storageProvider,
            viewModel.ReplaceOutputFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        if (!viewModel.CanBuildReplace ||
            !await viewModel.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync())
        {
            viewModel.CloseCtrlRamFirmwareVersionModal();
            return;
        }

        viewModel.CloseCtrlRamFirmwareVersionModal();
        await viewModel.BuildReplaceAsync(outputPath, edit);
    }
}
