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
        ModalInitialFocus.Register(this, () => CloseButton);
    }

    private void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UiEventAdapter.Run(this, "ReplaceSelectionModal.BuildReplace", _ => HandleBuildReplaceButton_OnClickAsync(sender, e));
    }

    private async Task HandleBuildReplaceButton_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReplacePresentationViewModel viewModel || !viewModel.CanBuildReplace)
        {
            return;
        }

        _ = await MainWindow.OpenReplaceBuildSettingsAsync(viewModel);
    }
}
