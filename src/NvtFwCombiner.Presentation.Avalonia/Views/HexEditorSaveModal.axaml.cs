using Avalonia.Controls;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirmation surface for exporting staged Hex Editor bytes as a generated BIN.</summary>
public sealed partial class HexEditorSaveModal : UserControl
{
    /// <summary>Initializes the generated Avalonia view.</summary>
    public HexEditorSaveModal()
    {
        InitializeComponent();
    }

    private async void ConfirmHexEditorSaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            !viewModel.CanSave ||
            TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickEditedFirmwareOutputPathAsync(
            storageProvider,
            viewModel.SuggestedOutputFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        viewModel.CancelSaveCommand.Execute(null);
        await viewModel.SaveAsAsync(outputPath);
    }
}
