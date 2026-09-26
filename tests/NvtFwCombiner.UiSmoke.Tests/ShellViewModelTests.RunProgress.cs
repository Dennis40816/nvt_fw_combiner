using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
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
                    viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode),
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

    /// <summary>Cancellation after commit keeps the exact committed receipt visible during report preparation.</summary>
    [Theory]
    [InlineData(
        "English",
        "Build output committed; report unavailable",
        "bytes",
        "Report unavailable: Cancelled after output commit.",
        "Output ready; report unavailable")]
    [InlineData(
        "ChineseTraditional",
        "Build 輸出已寫入，報告無法使用",
        "位元組",
        "報告無法使用：輸出寫入後已取消。",
        "輸出已就緒，報告無法使用")]
    public async Task PostcommitReportCancellationKeepsCommittedOutputVisible(
        string languageName,
        string title,
        string sizeUnit,
        string reportFailure,
        string progressLabel)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-postcommit-report");
        MainWindowViewModel viewModel = ConfigureRunnableGeneralMerge(
            workspace,
            Enum.Parse<ShellLanguage>(languageName));
        string outputPath = workspace.PathFor("output.bin");
        int interrupted = 0;
        viewModel.RunSession.CompositionProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(CompositionRunProgressViewModel.DeliveryState) ||
                viewModel.RunSession.CompositionProgress.DeliveryState != CompositionRunDeliveryState.ArtifactCommitted)
            {
                return;
            }

            interrupted++;
            viewModel.RunSession.CancelActiveRun();
        };

        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.Equal(1, interrupted);
        AssertCommittedOutputWithoutReport(viewModel, outputPath, title, sizeUnit, reportFailure, progressLabel);
    }

    /// <summary>
    /// Every report-materialization, I/O, or access failure in post-commit projection publishes the committed
    /// receipt instead of escaping.
    /// </summary>
    [Theory]
    [InlineData(
        nameof(JsonException),
        "English",
        "Build output committed; report unavailable",
        "bytes",
        "Report unavailable: Synthetic post-commit failure.",
        "Output ready; report unavailable")]
    [InlineData(
        nameof(FormatException),
        "English",
        "Build output committed; report unavailable",
        "bytes",
        "Report unavailable: Synthetic post-commit failure.",
        "Output ready; report unavailable")]
    [InlineData(
        nameof(IOException),
        "English",
        "Build output committed; report unavailable",
        "bytes",
        "Report unavailable: Synthetic post-commit failure.",
        "Output ready; report unavailable")]
    [InlineData(
        nameof(OverflowException),
        "ChineseTraditional",
        "Build 輸出已寫入，報告無法使用",
        "位元組",
        "報告無法使用：Synthetic post-commit failure.",
        "輸出已就緒，報告無法使用")]
    [InlineData(
        nameof(JsonException),
        "ChineseTraditional",
        "Build 輸出已寫入，報告無法使用",
        "位元組",
        "報告無法使用：Synthetic post-commit failure.",
        "輸出已就緒，報告無法使用")]
    [InlineData(
        nameof(UnauthorizedAccessException),
        "ChineseTraditional",
        "Build 輸出已寫入，報告無法使用",
        "位元組",
        "報告無法使用：Synthetic post-commit failure.",
        "輸出已就緒，報告無法使用")]
    public async Task PostcommitProjectionFailureKeepsCommittedOutputVisible(
        string exceptionName,
        string languageName,
        string title,
        string sizeUnit,
        string reportFailure,
        string progressLabel)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-postcommit-report-failure");
        MainWindowViewModel viewModel = ConfigureRunnableGeneralMerge(
            workspace,
            Enum.Parse<ShellLanguage>(languageName));
        string outputPath = workspace.PathFor("output.bin");
        int injected = 0;
        viewModel.RunSession.PropertyChanged += (_, args) =>
        {
            if (injected > 0 ||
                args.PropertyName != nameof(CompositionRunPresentationViewModel.LastRunResult) ||
                viewModel.RunSession.CompositionProgress.DeliveryState != CompositionRunDeliveryState.ArtifactCommitted ||
                !string.Equals(viewModel.RunSession.LastRunResult.Output, outputPath, StringComparison.Ordinal))
            {
                return;
            }

            // The committed run result is being projected; fail that post-commit projection once.
            injected++;
            throw CreateSyntheticFailure(exceptionName, "Synthetic post-commit failure.");
        };

        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.Equal(1, injected);
        AssertCommittedOutputWithoutReport(viewModel, outputPath, title, sizeUnit, reportFailure, progressLabel);
    }

    private static void AssertCommittedOutputWithoutReport(
        MainWindowViewModel viewModel,
        string outputPath,
        string title,
        string sizeUnit,
        string reportFailure,
        string progressLabel)
    {
        byte[] committedBytes = File.ReadAllBytes(outputPath);
        string sha256 = Convert.ToHexString(SHA256.HashData(committedBytes)).ToLowerInvariant();
        UiRunResultViewModel result = viewModel.RunSession.LastRunResult;
        Assert.Equal(title, result.Title);
        Assert.Equal(outputPath, result.Output);
        Assert.Contains($"{committedBytes.Length} {sizeUnit}", result.Detail, StringComparison.Ordinal);
        Assert.Contains(sha256, result.Detail, StringComparison.Ordinal);
        Assert.EndsWith(reportFailure, result.Detail, StringComparison.Ordinal);
        Assert.False(result.Succeeded);
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.False(viewModel.RunSession.IsRunInProgress);
        Assert.Equal(outputPath, viewModel.RunSession.CompositionProgress.CommittedOutputId);
        Assert.Equal(
            CompositionRunDeliveryState.ReportUnavailable,
            viewModel.RunSession.CompositionProgress.DeliveryState);
        Assert.Equal(progressLabel, viewModel.RunSession.CompositionProgress.CurrentStepLabel);
        Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);
        Assert.True(viewModel.IsLatestOutputActionVisible);
    }

    /// <summary>A committed output whose report is unavailable still offers the latest-output shortcut.</summary>
    [Fact]
    public async Task PostcommitReportUnavailableRetainsLatestOutputShortcut()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-postcommit-latest-output");
        MainWindowViewModel viewModel = ConfigureRunnableGeneralMerge(workspace);
        string outputPath = workspace.PathFor("output.bin");
        viewModel.RunSession.CompositionProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunProgressViewModel.DeliveryState) &&
                viewModel.RunSession.CompositionProgress.DeliveryState == CompositionRunDeliveryState.ArtifactCommitted)
            {
                viewModel.RunSession.CancelActiveRun();
            }
        };
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        Assert.False(viewModel.IsLatestOutputActionVisible);

        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.Equal("Build output committed; report unavailable", viewModel.RunSession.LastRunResult.Title);
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.Equal(string.Empty, viewModel.BuildResult.OutputPath);
        Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);
        Assert.True(viewModel.BuildResult.HasLatestCommittedOutput);
        Assert.True(viewModel.IsLatestOutputActionVisible);
        Assert.Contains(nameof(MainWindowViewModel.IsLatestOutputActionVisible), notifications);
    }

    /// <summary>A report-publication failure after commit reaches the fallback before any success modal opens.</summary>
    [Theory]
    [InlineData(nameof(InvalidOperationException))]
    [InlineData(nameof(IOException))]
    [InlineData(nameof(UnauthorizedAccessException))]
    public async Task PostcommitReportPublicationFailureKeepsBuildCompletedClosed(string exceptionName)
    {
        const string outputPath = "committed-output.bin";
        var harness = new ReportPublicationFailureHarness(
            CreateSyntheticFailure(exceptionName, "Synthetic report publication failure."));
        CompositionRunResult committed = CreateSyntheticBuildResult(outputPath);

        UiRunResultViewModel? result = await harness.RunAsync(committed);

        Assert.True(harness.PublicationFailed);
        Assert.NotNull(result);
        Assert.Same(harness.Owner.LastRunResult, result);
        Assert.Equal("Build output committed; report unavailable", result.Title);
        Assert.Equal(
            $"{committed.OutputSize} bytes / SHA-256 {committed.OutputSha256}. " +
                "Report unavailable: Synthetic report publication failure.",
            result.Detail);
        Assert.Equal(outputPath, result.Output);
        Assert.False(result.Succeeded);
        Assert.False(harness.BuildResult.IsOpen);
        Assert.Equal(outputPath, harness.BuildResult.LatestCommittedOutputPath);
        Assert.Equal(0, harness.ErrorReportLoads);
        Assert.False(harness.RunSession.IsRunInProgress);
        Assert.Equal(
            CompositionRunDeliveryState.ReportUnavailable,
            harness.RunSession.CompositionProgress.DeliveryState);
        Assert.False(harness.Reports.HasLoadedReport);
        Assert.Equal(string.Empty, harness.Reports.LoadedReportJson);
        Assert.Empty(harness.Reports.ReportHistoryEntries);
        Assert.Empty(harness.Reports.RunReportEntries);
        Assert.False(harness.Reports.HasReportToast);
    }

    /// <summary>
    /// A report publication that fails after the output commits never exposes the generated report: the loaded
    /// report, its history and its notification stay exactly as they were, and no observer is notified at all.
    /// </summary>
    [Fact]
    public async Task PostcommitReportPublicationFailureNeverExposesGeneratedReport()
    {
        var harness = new ReportPublicationFailureHarness(
            new InvalidOperationException("Synthetic report publication failure."));
        string previousJson = ReportJsonSamples.Succeeded(runId: "previous-report");
        harness.Reports.LoadReportJson(previousJson, "previous.json");
        ReportReviewViewModel previousReport = harness.Reports.LoadedReport;
        ReportHistoryEntryViewModel previousEntry = Assert.Single(harness.Reports.ReportHistoryEntries);
        string previousToast = harness.Reports.ReportToastText;
        List<string> notifications = [];
        RecordReportNotifications(harness.Reports, notifications);

        UiRunResultViewModel? result = await harness.RunAsync(CreateSyntheticBuildResult("committed-output.bin"));

        Assert.True(harness.PublicationFailed);
        Assert.NotNull(result);
        Assert.Equal("Build output committed; report unavailable", result.Title);
        Assert.Same(previousReport, harness.Reports.LoadedReport);
        Assert.Equal(previousJson, harness.Reports.LoadedReportJson);
        Assert.Same(previousEntry, Assert.Single(harness.Reports.ReportHistoryEntries));
        Assert.Same(previousEntry, Assert.Single(harness.Reports.RunReportEntries));
        Assert.Equal(previousToast, harness.Reports.ReportToastText);
        Assert.True(harness.Reports.HasReportToast);
        Assert.False(harness.Reports.IsReportModalOpen);

        // No notification at all, not merely a restored last one: no binding or history persistence saw the report.
        Assert.Empty(notifications);
    }

    /// <summary>
    /// A generated report whose modal cannot open is never published: the pre-open hook runs before the report
    /// changes any state or notifies anyone, the failure notifies no observer, and the next publication succeeds
    /// with a contiguous history sequence.
    /// </summary>
    [Fact]
    public void GeneratedReportThatCannotOpenIsNeverPublished()
    {
        bool failOpen = false;
        List<string> notifications = [];
        List<(ReportReviewViewModel Report, int NotificationCount)> openHookObservations = [];
        ReportPresentationViewModel? reports = null;
        reports = new ReportPresentationViewModel(
            () => ShellTextResources.For(ShellLanguage.English),
            () =>
            {
                openHookObservations.Add((reports!.LoadedReport, notifications.Count));
                if (failOpen)
                {
                    throw new InvalidOperationException("Synthetic report open failure.");
                }
            });
        string previousJson = ReportJsonSamples.Succeeded(runId: "previous-report");
        reports.LoadReportJson(previousJson, "previous.json");
        ReportReviewViewModel previousReport = reports.LoadedReport;
        ReportHistoryEntryViewModel previousEntry = Assert.Single(reports.ReportHistoryEntries);
        string previousToastTitle = reports.ShellToastTitle;
        string previousToast = reports.ReportToastText;
        double previousToastOpacity = reports.ReportToastOpacity;
        string generatedJson = ReportJsonSamples.Succeeded(
            runId: "generated-report",
            startedAtUtc: "2026-07-02T00:00:00Z");
        ReportReviewViewModel generated = ReportReviewViewModel.FromJson(generatedJson, "build report");
        RecordReportNotifications(reports, notifications);
        failOpen = true;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => reports.PublishGeneratedReport(generated, generatedJson, "Build", show: true));

        Assert.Equal("Synthetic report open failure.", exception.Message);
        Assert.Same(previousReport, reports.LoadedReport);
        Assert.Equal(previousJson, reports.LoadedReportJson);
        Assert.Same(previousEntry, Assert.Single(reports.ReportHistoryEntries));
        Assert.Same(previousEntry, Assert.Single(reports.RunReportEntries));
        Assert.Equal(previousToastTitle, reports.ShellToastTitle);
        Assert.Equal(previousToast, reports.ReportToastText);
        Assert.True(reports.HasReportToast);
        Assert.Equal(previousToastOpacity, reports.ReportToastOpacity);
        Assert.False(reports.IsReportModalOpen);
        Assert.Empty(notifications);

        failOpen = false;
        reports.PublishGeneratedReport(generated, generatedJson, "Build", show: true);

        Assert.Same(generated, reports.LoadedReport);
        Assert.Equal(generatedJson, reports.LoadedReportJson);
        Assert.Equal(2, reports.ReportHistoryEntries.Count);
        Assert.Equal(2, reports.ReportHistoryEntries[0].Sequence);
        Assert.Same(previousEntry, reports.ReportHistoryEntries[1]);
        Assert.True(reports.IsReportModalOpen);
        Assert.Contains("property LoadedReport", notifications);

        // Both attempts ran the hook while the previous report was still loaded and nothing had been notified.
        Assert.Collection(
            openHookObservations,
            observed =>
            {
                Assert.Same(previousReport, observed.Report);
                Assert.Equal(0, observed.NotificationCount);
            },
            observed =>
            {
                Assert.Same(previousReport, observed.Report);
                Assert.Equal(0, observed.NotificationCount);
            });
    }

    /// <summary>A report failure for a Build that committed no output keeps the original failure handling.</summary>
    [Fact]
    public async Task UncommittedBuildReportFailureKeepsOriginalHandling()
    {
        var ioHarness = new ReportPublicationFailureHarness(new IOException("Synthetic report publication failure."));

        UiRunResultViewModel? failed = await ioHarness.RunAsync(CreateSyntheticBuildResult(committedOutputPath: null));

        Assert.True(ioHarness.PublicationFailed);
        Assert.NotNull(failed);
        Assert.Equal("Build failed", failed.Title);
        Assert.Equal("Synthetic report publication failure.", failed.Detail);
        Assert.Equal("No output", failed.Output);
        Assert.Equal(1, ioHarness.ErrorReportLoads);
        Assert.False(ioHarness.BuildResult.IsOpen);
        Assert.False(ioHarness.BuildResult.HasLatestCommittedOutput);

        var jsonHarness = new ReportPublicationFailureHarness(new JsonException("Synthetic report publication failure."));

        _ = await Assert.ThrowsAsync<JsonException>(
            () => jsonHarness.RunAsync(CreateSyntheticBuildResult(committedOutputPath: null)));

        Assert.True(jsonHarness.PublicationFailed);
        Assert.Equal("Build blocked", jsonHarness.Owner.LastRunResult.Title);
        Assert.Equal(0, jsonHarness.ErrorReportLoads);
        Assert.False(jsonHarness.BuildResult.IsOpen);
        Assert.False(jsonHarness.RunSession.IsRunInProgress);
    }

    /// <summary>An I/O failure before any output commits still produces the original Build failure.</summary>
    [Fact]
    public async Task PrecommitIoFailureKeepsOriginalBuildFailure()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        CompositionRunContext context = viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode);
        List<(string Action, string Message)> errorReports = [];

        UiRunResultViewModel? result = await viewModel.RunSession.RunCompositionAsync(
            context,
            build: true,
            (_, _) => throw new IOException("Synthetic pre-commit failure."),
            (action, message) => errorReports.Add((action, message)));

        Assert.NotNull(result);
        Assert.Same(context.Owner.LastRunResult, result);
        Assert.Equal("Build failed", result.Title);
        Assert.Equal("Synthetic pre-commit failure.", result.Detail);
        Assert.Equal("No output", result.Output);
        Assert.False(result.Succeeded);
        Assert.Equal(("Build", "Synthetic pre-commit failure."), Assert.Single(errorReports));
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.False(viewModel.BuildResult.HasLatestCommittedOutput);
        Assert.NotEqual(
            CompositionRunDeliveryState.ReportUnavailable,
            viewModel.RunSession.CompositionProgress.DeliveryState);
    }

    /// <summary>A report-materialization failure before any output commits is not turned into the committed fallback.</summary>
    [Fact]
    public async Task PrecommitJsonFailureIsNotTreatedAsCommittedOutput()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        CompositionRunContext context = viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode);
        UiRunResultViewModel initial = context.Owner.LastRunResult;
        int errorReports = 0;

        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            () => viewModel.RunSession.RunCompositionAsync(
                context,
                build: true,
                (_, _) => throw new JsonException("Synthetic pre-commit failure."),
                (_, _) => errorReports++));

        Assert.Equal("Synthetic pre-commit failure.", exception.Message);
        Assert.Same(initial, context.Owner.LastRunResult);
        Assert.Equal(0, errorReports);
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.False(viewModel.RunSession.IsRunInProgress);
    }

    private static Exception CreateSyntheticFailure(string exceptionName, string message)
    {
        return exceptionName switch
        {
            nameof(JsonException) => new JsonException(message),
            nameof(FormatException) => new FormatException(message),
            nameof(OverflowException) => new OverflowException(message),
            nameof(InvalidOperationException) => new InvalidOperationException(message),
            nameof(IOException) => new IOException(message),
            nameof(UnauthorizedAccessException) => new UnauthorizedAccessException(message),
            _ => throw new ArgumentOutOfRangeException(nameof(exceptionName), exceptionName, null),
        };
    }

    /// <summary>
    /// Records every notification the report owner raises: its properties, its history collection and the
    /// commands that open the report or the history.
    /// </summary>
    private static void RecordReportNotifications(ReportPresentationViewModel reports, List<string> notifications)
    {
        reports.PropertyChanged += (_, args) => notifications.Add($"property {args.PropertyName}");
        reports.ReportHistoryEntries.CollectionChanged += (_, args) => notifications.Add($"history {args.Action}");
        reports.ShowReportCommand.CanExecuteChanged += (_, _) => notifications.Add("command ShowReport");
        reports.ShowReportHistoryCommand.CanExecuteChanged += (_, _) => notifications.Add("command ShowReportHistory");
    }

    /// <summary>Creates a Build result that either committed a small output or was blocked before output.</summary>
    private static CompositionRunResult CreateSyntheticBuildResult(string? committedOutputPath)
    {
        bool committed = committedOutputPath is not null;
        DateTimeOffset timestamp = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        byte[] outputBytes = committed ? [0x01, 0x02, 0x03, 0x04] : [];
        var report = new CompositionRunReport(
            "ui-smoke-report-publication",
            "test-profile",
            "1.0.0",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace,
            timestamp,
            timestamp,
            [],
            [],
            [],
            committed
                ? []
                : [new CompositionIssue(
                    "processor.tool.missing",
                    "Combiner executable is not available.",
                    "run-ctrlram-postbuild")],
            new OutputArtifactSummary(
                committed ? "firmware.bin" : "No output",
                outputBytes.Length,
                committed ? Convert.ToHexString(SHA256.HashData(outputBytes)).ToLowerInvariant() : "empty-hash",
                committed));
        return new CompositionRunResult(
            committed ? CompositionExecutionStatus.Succeeded : CompositionExecutionStatus.Failed,
            outputBytes,
            report,
            committedOutputPath,
            previewToken: null,
            inspectionOutputSpaceId: null,
            inspectionReferenceSpaceId: null,
            inspectionReferenceBytes: null,
            inspectionOutputBytes: null,
            outcomeStatus: committed ? "Succeeded" : "Blocked");
    }

    /// <summary>
    /// Drives the real run lifecycle owner with explicit bindings and fails the first report publication after
    /// the run result is published.
    /// </summary>
    private sealed class ReportPublicationFailureHarness
    {
        private readonly ShellTextResources _text = ShellTextResources.For(ShellLanguage.English);
        private readonly Exception _failure;
        private bool _publicationArmed;

        internal ReportPublicationFailureHarness(Exception failure)
        {
            _failure = failure;
            Reports = new ReportPresentationViewModel(GetReportText, static () => { });
            BuildResult = new BuildResultViewModel(new UnusedFileRevealService(), static () => "Open folder failed.");
            RunSession = new CompositionRunPresentationViewModel(
                ShellLanguage.English,
                new CompositionRunStateBindings(
                    () => _text,
                    static () => "NT51927",
                    static () => "single",
                    () => Owner,
                    () => [Owner],
                    static () => string.Empty,
                    static () => true,
                    () => Reports,
                    BuildResult.TryShow,
                    BuildResult.RetainLatestCommittedOutput,
                    static () => { },
                    static () => { }));
            Owner.PropertyChanged += (_, args) =>
            {
                // The run result is published immediately before its report; arm one publication failure.
                if (args.PropertyName == nameof(WorkflowRunState.LastRunResult) && !PublicationFailed)
                {
                    _publicationArmed = true;
                }
            };
        }

        internal WorkflowRunState Owner { get; } = new();

        internal ReportPresentationViewModel Reports { get; }

        internal BuildResultViewModel BuildResult { get; }

        internal CompositionRunPresentationViewModel RunSession { get; }

        internal bool PublicationFailed { get; private set; }

        internal int ErrorReportLoads { get; private set; }

        internal Task<UiRunResultViewModel?> RunAsync(CompositionRunResult result)
        {
            var context = new CompositionRunContext(
                Owner,
                "ctrlram-replace",
                "NT51927",
                "single",
                ShowsNumberSelector: true,
                string.Empty);
            return RunSession.RunCompositionAsync(
                context,
                build: true,
                (progress, _) =>
                {
                    if (result.CommittedOutputId is { } committedOutputId)
                    {
                        PublishCommittedProgress(progress, result.Report.RunId, committedOutputId);
                    }

                    return ValueTask.FromResult(result);
                },
                (_, _) => ErrorReportLoads++);
        }

        private static void PublishCommittedProgress(
            CompositionRunProgressFeed progress,
            string runId,
            string committedOutputId)
        {
            CompositionRunPhase[] phases =
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.CommittingOutput,
                CompositionRunPhase.PreparingReport,
            ];
            progress.Start(runId);
            progress.Publish(new CompositionRunProgressSnapshot(
                runId,
                CompositionRunPhase.PreparingReport,
                phases,
                phases[..^1],
                committedOutputId));
            progress.Complete();
        }

        private ShellTextResources GetReportText()
        {
            if (!_publicationArmed)
            {
                return _text;
            }

            // The report owner reads its text while publishing the generated report and its notification.
            _publicationArmed = false;
            PublicationFailed = true;
            throw _failure;
        }
    }

    private sealed class UnusedFileRevealService : IFileRevealService
    {
        public bool TryRevealFile(string? filePath)
        {
            return false;
        }
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
                    viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode),
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
            bool materialized = false;
            Assert.False(MainWindow.TryApplyWarmupStep(
                () => materialized = true,
                viewModel.RunSession,
                static () => true));
            Assert.False(materialized);
            using var preloadCancellation = new CancellationTokenSource();
            Task cancelledPreload = MainWindow.WaitForRunIdleAsync(
                viewModel.RunSession,
                preloadCancellation.Token);
            preloadCancellation.Cancel();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelledPreload);
            Assert.True(viewModel.RunSession.IsRunInProgress);
            Task operatorPriorityWait = MainWindow.WaitForRunIdleAsync(
                viewModel.RunSession,
                TestContext.Current.CancellationToken);
            viewModel.RunSession.CancelActiveRun();
            await runTask;
            await operatorPriorityWait;
            Assert.False(MainWindow.TryApplyWarmupStep(
                () => materialized = true,
                viewModel.RunSession,
                static () => false));
            Assert.False(materialized);
            Assert.True(MainWindow.TryApplyWarmupStep(
                () => materialized = true,
                viewModel.RunSession,
                static () => true));
            Assert.True(materialized);
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
                    viewModel.Merge.CaptureRunContext(viewModel.Merge.SelectedMergeMode),
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
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
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
                    viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode),
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

        Assert.Equal("CtrlRAM · NT51926 / cascade", activeContextLabel);
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
                    viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode),
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
        using var uiThread = new UiThreadTestContext();

        await uiThread.InvokeAsync(async () =>
        {
            viewModel.ShowMergeCommand.Execute(null);
            viewModel.WorkflowSession.SelectedIc = "NT51926";
            viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
            viewModel.Merge.GeneralMergeOutputLength = "0x10";
            GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
            mapping.SourceStartAddress = "0x0";
            mapping.TargetStartAddress = "0x4";
            mapping.Length = "0x4";
            await viewModel.WorkflowSession.SetSlotFileAsync(
                mapping.MappingId,
                sourcePath,
                TestContext.Current.CancellationToken);

            CompositionRunContext initialContext = viewModel.Merge.CaptureRunContext(ExperienceIds.GeneralMerge);
            Assert.True(initialContext.IsPublicationCurrent);
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
            await viewModel.Merge.Inspection.ActiveTask;

            CompositionRunContext completedContext = Assert.IsType<CompositionRunContext>(viewModel.Merge.RunState.CompletedContext);
            Assert.Same(initialContext.AcceptedSession, completedContext.AcceptedSession);
            Assert.False(initialContext.IsPublicationCurrent);
            Assert.Equal("NT51926", completedContext.Ic);
            Assert.False(completedContext.IsPublicationCurrent);
            Assert.True(viewModel.Merge.RunState.LastRunResult.Succeeded);
            Assert.True(viewModel.Reports.HasReportHistory);
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
        });
    }

    /// <summary>The global progress surface names the active Preview or Build in the selected language.</summary>
    [Theory]
    [InlineData("English", "Preview in progress", "Build in progress")]
    [InlineData("ChineseTraditional", "正在預覽", "正在建立")]
    public async Task CompositionProgressNamesTheActiveAction(
        string languageName,
        string previewLabel,
        string buildLabel)
    {
        ShellLanguage language = Enum.Parse<ShellLanguage>(languageName);
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-run-progress");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel(language);
        viewModel.ShowMergeCommand.Execute(null);
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

    private static MainWindowViewModel ConfigureRunnableGeneralMerge(
        TempWorkspace workspace,
        ShellLanguage language = ShellLanguage.English)
    {
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel(language);
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
