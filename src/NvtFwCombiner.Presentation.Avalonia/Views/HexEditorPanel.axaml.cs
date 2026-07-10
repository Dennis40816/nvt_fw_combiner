using Avalonia.Controls;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Experimental profile-bound hexadecimal editor hosted below Replace workflows.</summary>
public sealed partial class HexEditorPanel : UserControl
{
    /// <summary>Initializes the hexadecimal editor panel.</summary>
    public HexEditorPanel()
    {
        InitializeComponent();
    }

    private async void BuildHexEditorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildHexEditor ||
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

        await viewModel.BuildHexEditorAsync(outputPath);
    }
}
