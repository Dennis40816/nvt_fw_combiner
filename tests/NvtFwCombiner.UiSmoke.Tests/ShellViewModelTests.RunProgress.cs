using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>The run lifecycle yields before invoking blocking work and keeps that work off the caller thread.</summary>
    [Fact]
    public async Task CompositionProgressPrecedesBackgroundRunWork()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        int eventSequence = 0;
        int progressSequence = 0;
        int workerSequence = 0;
        int workerThreadId = 0;
        bool wasActiveBeforeWorker = false;
        bool wasInactiveAfterWorker = false;
        var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var uiThread = new UiThreadTestContext();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.IsRunInProgress) && viewModel.IsRunInProgress)
            {
                progressSequence = Interlocked.Increment(ref eventSequence);
            }
        };

        try
        {
            await uiThread.InvokeAsync(async () =>
            {
                Task runTask = viewModel.RunCompositionAsync(
                    build: true,
                    async cancellationToken =>
                    {
                        workerThreadId = Environment.CurrentManagedThreadId;
                        workerSequence = Interlocked.Increment(ref eventSequence);
                        workerStarted.SetResult();
                        await releaseWorker.Task.WaitAsync(cancellationToken);
                        throw new InvalidOperationException("Expected blocking fake completion.");
                    },
                    (_, _) => { });

                wasActiveBeforeWorker = viewModel.IsRunInProgress && !workerStarted.Task.IsCompleted;
                await workerStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
                releaseWorker.SetResult();
                await runTask;
                wasInactiveAfterWorker = !viewModel.IsRunInProgress;
            });
        }
        finally
        {
            _ = releaseWorker.TrySetResult();
        }

        Assert.True(wasActiveBeforeWorker);
        Assert.Equal(1, progressSequence);
        Assert.Equal(2, workerSequence);
        Assert.NotEqual(uiThread.ThreadId, workerThreadId);
        Assert.True(wasInactiveAfterWorker);
    }

    /// <summary>A queued run retains the IC and file bindings captured before the dispatcher yield.</summary>
    [Fact]
    public async Task CompositionRunUsesTheCapturedUiInputs()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-run-snapshot");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Task previewTask = viewModel.PreviewMergeCommand.ExecuteAsync(null);
        viewModel.SelectedIc = "NT51927";
        await previewTask;

        Assert.Equal("NT51926", viewModel.LoadedReport.IcId);
        Assert.False(viewModel.IsRunInProgress);
    }

    /// <summary>The global progress surface names the active Preview or Build in the selected language.</summary>
    [Theory]
    [InlineData(ShellLanguage.English, "Preview in progress", "Build in progress")]
    [InlineData(ShellLanguage.ChineseTraditional, "正在預覽", "正在建立")]
    public async Task CompositionProgressNamesTheActiveAction(
        ShellLanguage language,
        string previewLabel,
        string buildLabel)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-run-progress");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(language);
        viewModel.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);
        List<string> activeLabels = [];
        bool wasInProgress = false;
        int labelNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.RunProgressAccessibleLabel))
            {
                labelNotifications++;
            }

            if (args.PropertyName == nameof(MainWindowViewModel.IsRunInProgress))
            {
                if (!wasInProgress && viewModel.IsRunInProgress)
                {
                    activeLabels.Add(viewModel.RunProgressAccessibleLabel);
                }

                wasInProgress = viewModel.IsRunInProgress;
            }
        };

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);
        await viewModel.BuildStandardMergeAsync(workspace.PathFor("output.bin"));

        Assert.Equal([previewLabel, buildLabel], activeLabels);
        Assert.Equal(2, labelNotifications);
        Assert.False(viewModel.IsRunInProgress);
    }
}
