using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>The normal run-result projection opens the confirmation for its committed output.</summary>
    [Fact]
    public async Task CompletedBuildProjectionOpensOutputConfirmation()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        WorkbenchRunResult result = await CreateDpReplaceInspectionResultAsync();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        await viewModel.ProjectAndApplyRunResultAsync(
            result with { CommittedOutputId = outputPath },
            build: true,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsBuildCompletedModalOpen);
        Assert.Equal(outputPath, viewModel.BuildCompletedOutputPath);
    }

    /// <summary>A committed Build opens one actionable confirmation and OK clears its state.</summary>
    [Fact]
    public void CommittedBuildShowsOutputConfirmation()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));
        Assert.True(viewModel.IsBuildCompletedModalOpen);
        Assert.Equal(outputPath, viewModel.BuildCompletedOutputPath);

        viewModel.CloseBuildCompletedModalCommand.Execute(null);

        Assert.False(viewModel.IsBuildCompletedModalOpen);
        Assert.Equal(string.Empty, viewModel.BuildCompletedOutputPath);
    }

    /// <summary>Preview, blocked Build, and uncommitted success cannot claim that a BIN is ready.</summary>
    [Fact]
    public void OutputConfirmationRequiresSuccessfulCommittedBuild()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: false));
        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: false, outputPath), build: true));
        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath: null), build: true));
        Assert.False(viewModel.IsBuildCompletedModalOpen);
    }

    /// <summary>The folder action derives only a fully qualified parent directory from the committed BIN path.</summary>
    [Fact]
    public void OutputFolderNavigationRequiresFullyQualifiedBinPath()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");

        DirectoryInfo directory = Assert.IsType<DirectoryInfo>(BuildCompletedModal.TryGetOutputDirectory(outputPath));

        Assert.Equal(Path.GetDirectoryName(outputPath), directory.FullName);
        Assert.Null(BuildCompletedModal.TryGetOutputDirectory("firmware.bin"));
        Assert.Null(BuildCompletedModal.TryGetOutputDirectory(string.Empty));
    }

    private static WorkbenchRunResult CreateRunResult(bool succeeded, string? outputPath)
    {
        return new WorkbenchRunResult(
            succeeded,
            succeeded ? "succeeded" : "blocked",
            "test-profile",
            4,
            "hash",
            "firmware.bin",
            outputPath,
            "{}");
    }
}
