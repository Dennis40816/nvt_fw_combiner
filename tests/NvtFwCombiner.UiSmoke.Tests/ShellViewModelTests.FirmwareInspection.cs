using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>UI facts and output naming remain bound to the selected immutable snapshot after capture.</summary>
    [Fact]
    public async Task AsyncFirmwareSelectionProjectsSnapshotWithoutLaterFileReads()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] bytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-inspection-snapshot");
        string path = workspace.Write("base.bin", bytes);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";

        await viewModel.SetSlotFileAsync(
            "replace-base",
            path,
            TestContext.Current.CancellationToken);
        File.Delete(path);

        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW" && fact.Value == "1.4.1");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "DP" && fact.Value == "D01-02");
        Assert.StartsWith(
            "NT51926_FlashCode_DxxxxT0100_",
            viewModel.ReplaceOutputFileName,
            StringComparison.Ordinal);
    }

    /// <summary>A newer file selection cancels and owns the result when an older snapshot is still loading.</summary>
    [Fact]
    public async Task NewerFirmwareSelectionRejectsStaleInspectionResult()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] bytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-stale-inspection");
        string firstPath = workspace.Write("first.bin", bytes);
        string secondPath = workspace.Write("second.bin", bytes);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<WorkbenchFirmwareArtifactSnapshot?> LoadAsync(string path, CancellationToken cancellationToken)
        {
            if (string.Equals(path, firstPath, StringComparison.Ordinal))
            {
                firstStarted.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            return WorkbenchCompositionService.TryCaptureFirmwareArtifact(path);
        }

        var viewModel = new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            LoadAsync)
        {
            SelectedIc = "NT51926"
        };

        Task firstSelection = viewModel.SetSlotFileAsync(
            "replace-base",
            firstPath,
            TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(firstPath, viewModel.ReplaceBaseSlot.FilePath);
        Assert.Empty(viewModel.ReplaceBaseSlot.FirmwareFacts);

        await viewModel.SetSlotFileAsync(
            "replace-base",
            secondPath,
            TestContext.Current.CancellationToken);
        releaseFirst.SetResult();
        await firstSelection;

        Assert.Equal(secondPath, viewModel.ReplaceBaseSlot.FilePath);
        Assert.Equal(
            Path.GetFullPath(secondPath),
            viewModel.ReplaceBaseSlot.ArtifactSnapshot?.ArtifactPath);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW" && fact.Value == "1.4.1");
    }

    /// <summary>The selected path is published first and capture work never starts on the UI thread.</summary>
    [Fact]
    public async Task FirmwareInspectionCaptureRunsOffTheUiThread()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] bytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-inspection-thread");
        string path = workspace.Write("base.bin", bytes);
        var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MainWindowViewModel? viewModel = null;
        int workerThreadId = 0;
        bool pathWasPublishedBeforeCapture = false;

        async Task<WorkbenchFirmwareArtifactSnapshot?> LoadAsync(
            string candidatePath,
            CancellationToken cancellationToken)
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            pathWasPublishedBeforeCapture = string.Equals(
                viewModel!.ReplaceBaseSlot.FilePath,
                candidatePath,
                StringComparison.Ordinal);
            workerStarted.SetResult();
            await releaseWorker.Task.WaitAsync(cancellationToken);
            return WorkbenchCompositionService.TryCaptureFirmwareArtifact(candidatePath);
        }

        viewModel = new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            LoadAsync)
        {
            SelectedIc = "NT51926"
        };
        using var uiThread = new UiThreadTestContext();

        try
        {
            await uiThread.InvokeAsync(async () =>
            {
                Task selection = viewModel.SetSlotFileAsync(
                    "replace-base",
                    path,
                    TestContext.Current.CancellationToken);
                await workerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
                releaseWorker.SetResult();
                await selection;
            });
        }
        finally
        {
            _ = releaseWorker.TrySetResult();
        }

        Assert.True(pathWasPublishedBeforeCapture);
        Assert.NotEqual(uiThread.ThreadId, workerThreadId);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW" && fact.Value == "1.4.1");
    }
}
