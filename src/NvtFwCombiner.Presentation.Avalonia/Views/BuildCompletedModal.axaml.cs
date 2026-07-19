using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms a committed Build and offers direct navigation to its output folder.</summary>
public sealed partial class BuildCompletedModal : UserControl
{
    /// <summary>Initializes the successful Build confirmation.</summary>
    public BuildCompletedModal()
    {
        InitializeComponent();
    }

    internal static DirectoryInfo? TryGetOutputDirectory(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathFullyQualified(outputPath))
        {
            return null;
        }

        try
        {
            return new FileInfo(outputPath).Directory;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private async void OpenOutputFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not { Launcher: { } launcher } ||
            TryGetOutputDirectory(viewModel.BuildCompletedOutputPath) is not { } outputDirectory)
        {
            viewModel.NotifyBuildCompletedOpenFolderFailed();
            return;
        }

        try
        {
            if (await launcher.LaunchDirectoryInfoAsync(outputDirectory))
            {
                viewModel.CloseBuildCompletedModal();
            }
            else
            {
                viewModel.NotifyBuildCompletedOpenFolderFailed();
            }
        }
        catch (IOException)
        {
            viewModel.NotifyBuildCompletedOpenFolderFailed();
        }
        catch (UnauthorizedAccessException)
        {
            viewModel.NotifyBuildCompletedOpenFolderFailed();
        }
    }
}
