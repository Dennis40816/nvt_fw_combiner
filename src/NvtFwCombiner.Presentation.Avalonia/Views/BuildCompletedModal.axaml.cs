using Avalonia.Controls;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms a committed Build and offers direct navigation to its output BIN.</summary>
public sealed partial class BuildCompletedModal : UserControl
{
    /// <summary>Initializes the successful Build confirmation.</summary>
    public BuildCompletedModal()
    {
        InitializeComponent();
    }

    private void RevealOutputFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (FileRevealLauncher.TryReveal(viewModel.BuildCompletedOutputPath))
        {
            viewModel.CloseBuildCompletedModal();
            return;
        }

        viewModel.NotifyBuildCompletedOpenFolderFailed();
    }
}
