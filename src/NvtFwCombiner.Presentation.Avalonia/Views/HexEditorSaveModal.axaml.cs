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
        if (DataContext is not MainWindowViewModel viewModel ||
            !viewModel.CanBuildHexEditor ||
            TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickEditedFirmwareOutputPathAsync(
            storageProvider,
            viewModel.ReplaceOutputFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        viewModel.CancelHexEditorSaveCommand.Execute(null);
        await viewModel.BuildHexEditorAsync(outputPath);
    }
}
