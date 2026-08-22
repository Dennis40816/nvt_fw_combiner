using Avalonia.Controls;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Overlay that reviews selected Replace inputs before launching the Build save dialog.</summary>
public sealed partial class ReplaceSelectionModal : UserControl
{
    /// <summary>Initializes the Replace selection modal.</summary>
    public ReplaceSelectionModal()
    {
        InitializeComponent();
    }

    private async void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReplacePresentationViewModel viewModel || !viewModel.CanBuildReplace)
        {
            return;
        }

        await viewModel.RefreshSelectedFirmwareInspectionsAsync();
        if (!viewModel.CanBuildReplace)
        {
            return;
        }

        if (viewModel.IsCtrlRamReplaceModeSelected)
        {
            _ = await viewModel.RequestCtrlRamBuildSettingsAsync();
            return;
        }

        await viewModel.RequestBuildOutputDeliveryAsync();
    }
}
