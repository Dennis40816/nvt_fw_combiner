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
        return OutputFolderLauncher.TryGetOutputDirectory(outputPath);
    }

    private async void OpenOutputFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (await OutputFolderLauncher.TryOpenAsync(
                TopLevel.GetTopLevel(this)?.Launcher,
                viewModel.BuildCompletedOutputPath))
        {
            viewModel.CloseBuildCompletedModal();
            return;
        }

        viewModel.NotifyBuildCompletedOpenFolderFailed();
    }
}

internal static class OutputFolderLauncher
{
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

    internal static async Task<bool> TryOpenAsync(ILauncher? launcher, string outputPath)
    {
        if (launcher is null || TryGetOutputDirectory(outputPath) is not { } outputDirectory)
        {
            return false;
        }

        try
        {
            return await launcher.LaunchDirectoryInfoAsync(outputDirectory);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
