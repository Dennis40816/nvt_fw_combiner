using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class RunAndHexEditorTests
{
    /// <summary>The run lifecycle yields before invoking blocking work and keeps that work off the caller thread.</summary>
    [Fact]
    public async Task CompositionProgressPrecedesBackgroundRunWork()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
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
        viewModel.RunSession.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunPresentationViewModel.IsRunInProgress) && viewModel.RunSession.IsRunInProgress)
            {
                progressSequence = Interlocked.Increment(ref eventSequence);
            }
        };

        try
        {
            await uiThread.InvokeAsync(async () =>
            {
                Task runTask = viewModel.RunSession.RunCompositionAsync(
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

                wasActiveBeforeWorker = viewModel.RunSession.IsRunInProgress && !workerStarted.Task.IsCompleted;
                await workerStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
                releaseWorker.SetResult();
                await runTask;
                wasInactiveAfterWorker = !viewModel.RunSession.IsRunInProgress;
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
        viewModel.RunSession.CompositionProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunProgressViewModel.CurrentPhase))
            {
                progressThreadIds.Add(Environment.CurrentManagedThreadId);
                phases.Add(Assert.IsType<CompositionRunPhase>(viewModel.RunSession.CompositionProgress.CurrentPhase));
            }
        };

        await uiThread.InvokeAsync(async () => await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null));

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
        Assert.False(viewModel.RunSession.IsRunInProgress);
    }

    /// <summary>Build announces the committed artifact before background report projection becomes ready.</summary>
    [Fact]
    public async Task BuildSeparatesArtifactCommitFromReportReadiness()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-artifact-report-boundary");
        MainWindowViewModel viewModel = ConfigureRunnableGeneralMerge(workspace);
        string outputPath = workspace.PathFor("output.bin");
        List<CompositionRunDeliveryState> states = [];
        string? committedLabel = null;
        viewModel.RunSession.CompositionProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(CompositionRunProgressViewModel.DeliveryState))
            {
                return;
            }

            states.Add(viewModel.RunSession.CompositionProgress.DeliveryState);
            if (viewModel.RunSession.CompositionProgress.DeliveryState == CompositionRunDeliveryState.ArtifactCommitted)
            {
                committedLabel = viewModel.RunSession.CompositionProgress.CurrentStepLabel;
            }
        };

        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.Contains(CompositionRunDeliveryState.ArtifactCommitted, states);
        Assert.Equal(CompositionRunDeliveryState.ReportReady, states[^1]);
        Assert.Equal("Output ready; preparing report in background", committedLabel);
        Assert.Equal(outputPath, viewModel.RunSession.CompositionProgress.CommittedOutputId);
        Assert.Equal(CompositionRunDeliveryState.ReportReady, viewModel.RunSession.CompositionProgress.DeliveryState);
        Assert.Equal("Report ready", viewModel.RunSession.CompositionProgress.CurrentStepLabel);
        Assert.True(File.Exists(outputPath));
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
    }

    /// <summary>Cancelling a planning-stage run stops its unattached observer and releases command ownership.</summary>
    [Fact]
    public async Task CancellingPlanningRunStopsProgressObserver()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.IsReducedMotionEnabled = true;
        var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool usedStaticProgress = false;
        using var uiThread = new UiThreadTestContext();

        await uiThread.InvokeAsync(async () =>
        {
            Task runTask = viewModel.RunSession.RunCompositionAsync(
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
            usedStaticProgress = viewModel.RunSession.IsRunInProgress && !viewModel.RunSession.ShouldAnimateRunProgress;
            viewModel.RunSession.CancelActiveRun();
            await runTask;
        });

        Assert.False(viewModel.RunSession.IsRunInProgress);
        Assert.False(viewModel.RunSession.HasTypedRunProgress);
        Assert.True(usedStaticProgress);
    }

    /// <summary>Navigation cannot hide the active run's progress or change its captured number-selector shape.</summary>
    [Fact]
    public async Task ActiveRunKeepsProgressContextAcrossNavigation()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
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
                Task runTask = viewModel.RunSession.RunCompositionAsync(
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
                    viewModel.RunSession.IsRunInProgress &&
                    viewModel.IsDeviceContextVisible &&
                    !viewModel.WorkflowSession.IsNumberSelectorVisible &&
                    viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible;
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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
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
        viewModel.RunSession.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                notifications.Add(args.PropertyName);
            }
        };
        viewModel.WorkflowSession.PropertyChanged += (_, args) =>
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
                Task runTask = viewModel.RunSession.RunCompositionAsync(
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
                viewModel.WorkflowSession.SelectedIc = "NT51927";
                viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
                activeContextLabel = viewModel.RunSession.ActiveRunContextLabel;
                activeDeviceStatus = viewModel.WorkflowSession.DeviceContextStatus;
                selectionWasReadOnly = !viewModel.WorkflowSession.IsDeviceContextSelectionVisible;
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
            nameof(WorkflowSessionPresentationViewModel.IsDeviceContextSelectionVisible),
            nameof(WorkflowSessionPresentationViewModel.IsDeviceContextNumberSelectionVisible),
            nameof(WorkflowSessionPresentationViewModel.IsDeviceContextFamilyBadgeVisible),
            nameof(CompositionRunPresentationViewModel.DisplayedDeviceIc),
            nameof(CompositionRunPresentationViewModel.DisplayedDeviceNumber),
            nameof(CompositionRunPresentationViewModel.ActiveRunIc),
            nameof(CompositionRunPresentationViewModel.ActiveRunNumber),
            nameof(CompositionRunPresentationViewModel.ActiveRunMode),
            nameof(CompositionRunPresentationViewModel.ActiveRunContextLabel),
        ];
        Assert.All(activeContextBindings, propertyName => Assert.Contains(propertyName, startNotifications));
        Assert.All(activeContextBindings, propertyName => Assert.Contains(propertyName, completionNotifications));
        Assert.False(viewModel.RunSession.IsRunInProgress);
        Assert.True(viewModel.WorkflowSession.IsDeviceContextSelectionVisible);
        Assert.True(viewModel.WorkflowSession.IsDeviceContextNumberSelectionVisible);
        Assert.Empty(viewModel.RunSession.ActiveRunIc);
        Assert.Empty(viewModel.RunSession.ActiveRunNumber);
        Assert.Empty(viewModel.RunSession.ActiveRunMode);
    }

    /// <summary>An observer fault propagates only after the active-run cancellation source is released.</summary>
    [Fact]
    public async Task ProgressObserverFaultCannotRetainRunOwnership()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-progress-observer-fault");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        AuthoringMappingState mapping = GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
            "map-1",
            sourcePath,
            "0x0",
            "0x4",
            "0x4");
        Assert.True(GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
            [mapping],
            out GeneralMappingDraftState? mappingsDraft,
            out _));
        Assert.True(GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
            "0x10",
            outputFillByte: null,
            out GeneralMergeInitializer? initializer));
        GeneralMergeDraftState draft = GeneralMergeAuthoringUseCase.CreateDraft(
            initializer!,
            mappingsDraft!);
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralMerge);
        GeneralAuthoringSessionPreparation prepared =
            await TestHost.GeneralAuthoring
                .PrepareMergeSessionAsync(
                session,
                "NT51926",
                draft,
                TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues));
        ActiveSessionSnapshot acceptedSession = prepared.AcceptedSession!;
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.RunSession.CompositionProgress.PropertyChanged += (_, args) =>
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
                viewModel.RunSession.RunCompositionAsync(
                    build: false,
                    (progress, cancellationToken) => TestHost.CompositionExecution.ExecuteAsync(
                        new AcceptedCompositionExecutionRequest(
                            acceptedSession,
                            new Dictionary<string, string>(StringComparer.Ordinal),
                            build: false),
                        progress,
                        cancellationToken),
                    (_, _) => { }));

            Assert.Equal("Synthetic progress observer failure.", exception.Message);
        });

        Assert.False(viewModel.RunSession.IsRunInProgress);
    }

    /// <summary>A queued run retains its IC and mapping inputs captured before the dispatcher yield.</summary>
    [Fact]
    public async Task CompositionRunUsesCapturedUiInputs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-run-snapshot");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x0";
        mapping.TargetStartAddress = "0x4";
        mapping.Length = "0x4";
        viewModel.SetSlotFile(mapping.MappingId, sourcePath);

        Task previewTask = viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        GeneralMergeMappingViewModel currentMapping = Assert.Single(
            viewModel.Merge.GeneralMergeMappings);
        currentMapping.TargetStartAddress = "0x8";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            currentMapping.MappingId,
            sourcePath,
            TestContext.Current.CancellationToken);
        await previewTask;
        await viewModel.Merge.GeneralMergeReadinessRefreshTask;

        Assert.Equal("NT51926", viewModel.Reports.LoadedReport.IcId);
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement operation = Assert.Single(report.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal(4, operation.GetProperty("TargetRange").GetProperty("Start").GetInt64());
        Assert.False(viewModel.RunSession.IsRunInProgress);

        Assert.True(
            viewModel.Merge.PreviewMergeCommand.CanExecute(null),
            $"Readiness: {viewModel.Merge.MergeReadinessStatus}; " +
            $"row issue: {currentMapping.IssueMessage}; " +
            $"stamp: {currentMapping.AcceptedFileStamp}");
        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
        Assert.Equal("NT51927", viewModel.Reports.LoadedReport.IcId);
        using var currentReport = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement currentOperation = Assert.Single(
            currentReport.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal(8, currentOperation.GetProperty("TargetRange").GetProperty("Start").GetInt64());
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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel(language);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);
        List<string> activeLabels = [];
        bool wasInProgress = false;
        int labelNotifications = 0;
        viewModel.RunSession.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunPresentationViewModel.RunProgressAccessibleLabel))
            {
                labelNotifications++;
            }

            if (args.PropertyName == nameof(CompositionRunPresentationViewModel.IsRunInProgress))
            {
                if (!wasInProgress && viewModel.RunSession.IsRunInProgress)
                {
                    activeLabels.Add(viewModel.RunSession.RunProgressAccessibleLabel);
                }

                wasInProgress = viewModel.RunSession.IsRunInProgress;
            }
        };

        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
        await viewModel.Merge.BuildMergeAsync(workspace.PathFor("output.bin"));

        Assert.Equal([previewLabel, buildLabel], activeLabels);
        Assert.Equal(2, labelNotifications);
        Assert.False(viewModel.RunSession.IsRunInProgress);
        Assert.True(viewModel.RunSession.CompositionProgress.HasTypedProgress);
        Assert.Equal(
            CompositionRunPhase.PreparingReport,
            viewModel.RunSession.CompositionProgress.CurrentPhase);
    }

    private static MainWindowViewModel ConfigureRunnableGeneralMerge(TempWorkspace workspace)
    {
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x0";
        mapping.TargetStartAddress = "0x4";
        mapping.Length = "0x4";
        viewModel.SetSlotFile(mapping.MappingId, sourcePath);
        return viewModel;
    }
}
