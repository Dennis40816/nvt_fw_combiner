using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Bootstrap;
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
        CompositionRunProgressFeed? planningOnlyProgress = null;
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
                    async (progress, cancellationToken) =>
                    {
                        planningOnlyProgress = progress;
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
        Assert.NotNull(planningOnlyProgress);
        Assert.False(planningOnlyProgress.IsAttached);
    }

    /// <summary>Typed Application progress returns to the captured UI context before Presentation mutates state.</summary>
    [Fact]
    public async Task TypedCompositionProgressReturnsToUiThread()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-typed-progress-thread");
        MainWindowViewModel viewModel = ConfigureRunnableGeneralMerge(workspace);
        List<int> progressThreadIds = [];
        List<CompositionRunPhase> phases = [];
        using var uiThread = new UiThreadTestContext();
        viewModel.CompositionProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunProgressViewModel.CurrentPhase))
            {
                progressThreadIds.Add(Environment.CurrentManagedThreadId);
                phases.Add(Assert.IsType<CompositionRunPhase>(viewModel.CompositionProgress.CurrentPhase));
            }
        };

        await uiThread.InvokeAsync(async () => await viewModel.PreviewMergeCommand.ExecuteAsync(null));

        Assert.NotEmpty(progressThreadIds);
        Assert.All(progressThreadIds, threadId => Assert.Equal(uiThread.ThreadId, threadId));
        Assert.Equal(
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.PreparingReport,
            ],
            phases);
        Assert.False(viewModel.IsRunInProgress);
    }

    /// <summary>Cancelling a planning-stage run stops its unattached observer and releases command ownership.</summary>
    [Fact]
    public async Task CancellingPlanningRunStopsProgressObserver()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.IsReducedMotionEnabled = true;
        var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool usedStaticProgress = false;
        using var uiThread = new UiThreadTestContext();

        await uiThread.InvokeAsync(async () =>
        {
            Task runTask = viewModel.RunCompositionAsync(
                build: false,
                async (_, cancellationToken) =>
                {
                    workerStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    throw new InvalidOperationException("Cancelled fake work unexpectedly resumed.");
                },
                (_, _) => { });

            await workerStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            usedStaticProgress = viewModel.IsRunInProgress && !viewModel.ShouldAnimateRunProgress;
            viewModel.CancelActiveRun();
            await runTask;
        });

        Assert.False(viewModel.IsRunInProgress);
        Assert.False(viewModel.HasTypedRunProgress);
        Assert.True(usedStaticProgress);
    }

    /// <summary>Navigation cannot hide the active run's progress or change its captured number-selector shape.</summary>
    [Fact]
    public async Task ActiveRunKeepsProgressContextAcrossNavigation()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool progressRemainedVisible = false;
        bool contextClosedAfterRun = false;
        using var uiThread = new UiThreadTestContext();

        try
        {
            await uiThread.InvokeAsync(async () =>
            {
                Task runTask = viewModel.RunCompositionAsync(
                    build: false,
                    async (_, cancellationToken) =>
                    {
                        workerStarted.SetResult();
                        await releaseWorker.Task.WaitAsync(cancellationToken);
                        throw new InvalidOperationException("Expected navigation fake completion.");
                    },
                    (_, _) => { });
                await workerStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

                viewModel.ShowHomeCommand.Execute(null);
                progressRemainedVisible = viewModel.IsHomeVisible &&
                    viewModel.IsRunInProgress &&
                    viewModel.IsDeviceContextVisible &&
                    !viewModel.IsNumberSelectorVisible &&
                    viewModel.IsNumberSelectorPlaceholderVisible;
                releaseWorker.SetResult();
                await runTask;
                contextClosedAfterRun = !viewModel.IsDeviceContextVisible;
            });
        }
        finally
        {
            _ = releaseWorker.TrySetResult();
        }

        Assert.True(progressRemainedVisible);
        Assert.True(contextClosedAfterRun);
    }

    /// <summary>The progress surface keeps the captured IC, Number, and mode when shell selection changes.</summary>
    [Fact]
    public async Task ActiveRunDisplaysItsCapturedDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string activeContextLabel = string.Empty;
        string activeDeviceStatus = string.Empty;
        bool selectionWasReadOnly = false;
        string[] startNotifications = [];
        string[] completionNotifications = [];
        List<string> notifications = [];
        using var uiThread = new UiThreadTestContext();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        try
        {
            await uiThread.InvokeAsync(async () =>
            {
                Task runTask = viewModel.RunCompositionAsync(
                    build: false,
                    async (_, cancellationToken) =>
                    {
                        workerStarted.SetResult();
                        await releaseWorker.Task.WaitAsync(cancellationToken);
                        throw new InvalidOperationException("Expected captured-context fake completion.");
                    },
                    (_, _) => { });
                await workerStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

                startNotifications = [.. notifications];
                viewModel.SelectedIc = "NT51927";
                viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
                activeContextLabel = viewModel.ActiveRunContextLabel;
                activeDeviceStatus = viewModel.DeviceContextStatus;
                selectionWasReadOnly = !viewModel.IsDeviceContextSelectionVisible;
                notifications.Clear();
                releaseWorker.SetResult();
                await runTask;
                completionNotifications = [.. notifications];
            });
        }
        finally
        {
            _ = releaseWorker.TrySetResult();
        }

        Assert.Equal("DP · NT51926 / cascade", activeContextLabel);
        Assert.StartsWith("NT51926 / cascade:", activeDeviceStatus, StringComparison.Ordinal);
        Assert.True(selectionWasReadOnly);
        string[] activeContextBindings =
        [
            nameof(MainWindowViewModel.IsDeviceContextSelectionVisible),
            nameof(MainWindowViewModel.IsDeviceContextNumberSelectionVisible),
            nameof(MainWindowViewModel.IsDeviceContextFamilyBadgeVisible),
            nameof(MainWindowViewModel.DisplayedDeviceIc),
            nameof(MainWindowViewModel.DisplayedDeviceNumber),
            nameof(MainWindowViewModel.ActiveRunIc),
            nameof(MainWindowViewModel.ActiveRunNumber),
            nameof(MainWindowViewModel.ActiveRunMode),
            nameof(MainWindowViewModel.ActiveRunContextLabel),
        ];
        Assert.All(activeContextBindings, propertyName => Assert.Contains(propertyName, startNotifications));
        Assert.All(activeContextBindings, propertyName => Assert.Contains(propertyName, completionNotifications));
        Assert.False(viewModel.IsRunInProgress);
        Assert.True(viewModel.IsDeviceContextSelectionVisible);
        Assert.True(viewModel.IsDeviceContextNumberSelectionVisible);
        Assert.Empty(viewModel.ActiveRunIc);
        Assert.Empty(viewModel.ActiveRunNumber);
        Assert.Empty(viewModel.ActiveRunMode);
    }

    /// <summary>An observer fault propagates only after the active-run cancellation source is released.</summary>
    [Fact]
    public async Task ProgressObserverFaultCannotRetainRunOwnership()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-progress-observer-fault");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.CompositionProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunProgressViewModel.CurrentPhase))
            {
                throw new InvalidOperationException("Synthetic progress observer failure.");
            }
        };
        using var uiThread = new UiThreadTestContext();

        await uiThread.InvokeAsync(async () =>
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                viewModel.RunCompositionAsync(
                    build: false,
                    (progress, cancellationToken) => WorkbenchCompositionService.RunGeneralMergeWithProgressAsync(
                        "NT51926",
                        "0x10",
                        [new WorkbenchGeneralMergeMappingInput("map-1", sourcePath, "0x0", "0x4", "0x4")],
                        build: false,
                        progress,
                        cancellationToken),
                    (_, _) => { }));

            Assert.Equal("Synthetic progress observer failure.", exception.Message);
        });

        Assert.False(viewModel.IsRunInProgress);
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
        Assert.True(viewModel.CompositionProgress.HasTypedProgress);
        Assert.Equal(
            CompositionRunPhase.PreparingReport,
            viewModel.CompositionProgress.CurrentPhase);
    }

    private static MainWindowViewModel ConfigureRunnableGeneralMerge(TempWorkspace workspace)
    {
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
        return viewModel;
    }
}
