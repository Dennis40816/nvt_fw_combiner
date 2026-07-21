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
        Assert.Equal(outputPath, viewModel.LatestCommittedOutputPath);
        Assert.True(viewModel.HasLatestCommittedOutput);
    }

    /// <summary>A blocked Build opens its structured report immediately so the no-output reason is visible.</summary>
    [Fact]
    public async Task BlockedBuildProjectionOpensFailureReport()
    {
        var result = new WorkbenchRunResult(
            false,
            "blocked",
            "nt51927-ctrlram-replace",
            0,
            string.Empty,
            "No output",
            null,
            ReportJsonSamples.CtrlRamCommandIssue());
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        await viewModel.ProjectAndApplyRunResultAsync(
            result,
            build: true,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.IsBuildCompletedModalOpen);
        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Contains("Combiner executable is not available", viewModel.LastRunResult.Detail, StringComparison.Ordinal);
    }

    /// <summary>A blocked Preview retains the report toast without treating its expected lack of committed output as Build failure.</summary>
    [Fact]
    public async Task BlockedPreviewDoesNotAutoOpenFailureReport()
    {
        var result = new WorkbenchRunResult(
            false,
            "blocked",
            "nt51927-ctrlram-replace",
            0,
            string.Empty,
            "No output",
            null,
            ReportJsonSamples.CtrlRamCommandIssue());
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        await viewModel.ProjectAndApplyRunResultAsync(
            result,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsReportModalOpen);
        Assert.True(viewModel.HasReportToast);
    }

    /// <summary>A Build exception also opens the structured UI error report produced by the workflow callback.</summary>
    [Fact]
    public async Task BuildExceptionOpensFailureReport()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        await viewModel.RunCompositionAsync(
            build: true,
            (_, _) => throw new InvalidOperationException("No exact CtrlRAM route."),
            (action, message) => viewModel.LoadRunErrorReport(
                action,
                "nt51926-ctrlram-replace-workbench",
                "NT51926",
                "single",
                message,
                new Dictionary<string, string>()));

        Assert.True(viewModel.IsReportModalOpen);
        Assert.Equal("No exact CtrlRAM route.", Assert.Single(viewModel.LoadedReport.Issues).Detail);
        Assert.Equal("No output", viewModel.LastRunResult.Output);
    }

    /// <summary>A committed Build opens one confirmation while OK retains the latest-output shortcut.</summary>
    [Fact]
    public void CommittedBuildShowsOutputConfirmation()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));
        Assert.True(viewModel.IsBuildCompletedModalOpen);
        Assert.Equal(outputPath, viewModel.BuildCompletedOutputPath);
        Assert.Equal(outputPath, viewModel.LatestCommittedOutputPath);

        viewModel.CloseBuildCompletedModalCommand.Execute(null);

        Assert.False(viewModel.IsBuildCompletedModalOpen);
        Assert.Equal(string.Empty, viewModel.BuildCompletedOutputPath);
        Assert.Equal(outputPath, viewModel.LatestCommittedOutputPath);
        Assert.True(viewModel.HasLatestCommittedOutput);
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
        Assert.False(viewModel.HasLatestCommittedOutput);
    }

    /// <summary>The latest-output shortcut is scoped to composition pages and survives later failed runs.</summary>
    [Fact]
    public void LatestOutputActionTracksCommittedOutputAndCompositionPage()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        Assert.False(viewModel.IsLatestOutputActionVisible);
        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));
        viewModel.CloseBuildCompletedModal();

        Assert.True(viewModel.IsLatestOutputActionVisible);
        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: false, outputPath: "another.bin"), build: true));
        Assert.Equal(outputPath, viewModel.LatestCommittedOutputPath);

        viewModel.ShowHomeCommand.Execute(null);

        Assert.False(viewModel.IsLatestOutputActionVisible);
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
