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

    /// <summary>A queued run retains its IC and mapping inputs captured before the dispatcher yield.</summary>
    [Fact]
    public async Task CompositionRunUsesCapturedUiInputs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-run-snapshot");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedMergeMode = "General";
        viewModel.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x0";
        mapping.TargetStartAddress = "0x4";
        mapping.Length = "0x4";
        viewModel.SetSlotFile(mapping.MappingId, sourcePath);

        Task previewTask = viewModel.PreviewMergeCommand.ExecuteAsync(null);
        viewModel.SelectedIc = "NT51927";
        mapping.TargetStartAddress = "0x8";
        await previewTask;

        Assert.Equal("NT51926", viewModel.LoadedReport.IcId);
        using var report = JsonDocument.Parse(viewModel.LoadedReportJson);
        JsonElement operation = Assert.Single(report.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal(4, operation.GetProperty("TargetRange").GetProperty("Start").GetInt64());
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
        await viewModel.BuildMergeAsync(workspace.PathFor("output.bin"));

        Assert.Equal([previewLabel, buildLabel], activeLabels);
        Assert.Equal(2, labelNotifications);
        Assert.False(viewModel.IsRunInProgress);
    }
}
