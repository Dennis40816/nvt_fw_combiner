using System.Collections.Concurrent;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Behavioral coverage for the sole Presentation shell preload lifecycle.</summary>
public sealed partial class ShellPreloadSessionTests
{
    private static readonly ShellTextResources Text = ShellTextResources.For(ShellLanguage.English);

    /// <summary>Cold and last-known-good failures remain typed failures and never apply UI state.</summary>
    [Fact]
    public async Task TypedCatalogFailureRetainsOneRetryableTerminal()
    {
        CapabilityCatalogReloadResult published = await LoadSuccessfulResultAsync();
        foreach (CapabilityCatalogReloadResult failure in new[]
                 {
                     Failure(retainedLastKnownGood: false),
                     Failure(retainedLastKnownGood: true, published.Snapshot),
                 })
        {
            using ShellPreloadSession session = CreateSession();
            int applications = 0;

            CapabilityCatalogReloadResult result = await session.RunCatalogAsync(
                new ScriptedLoader(
                    new CanonicalCapabilityCatalogLoadUpdate(0, Result: null),
                    new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null),
                    new CanonicalCapabilityCatalogLoadUpdate(Progress: null, failure)),
                _ =>
                {
                    applications++;
                    return ValueTask.CompletedTask;
                },
                retry: false,
                TestContext.Current.CancellationToken);

            Assert.Same(failure, result);
            Assert.Equal(0, applications);
            Assert.True(session.CanRetryCatalog);
            ShellPreloadAttemptSnapshot attempt = Assert.IsType<ShellPreloadAttemptSnapshot>(
                session.CatalogStage.CurrentAttempt);
            Assert.Equal(ShellPreloadStageState.Failed, attempt.State);
            Assert.Equal(0.5, attempt.Progress);
            Assert.Contains(CapabilityCatalogIssueCodes.SourceInvalid, attempt.Diagnostic, StringComparison.Ordinal);
        }
    }

    /// <summary>Missing, duplicate, post-terminal, decreasing, and non-finite updates fail closed.</summary>
    [Fact]
    public async Task MalformedCatalogStreamsCannotApply()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        CanonicalCapabilityCatalogLoadUpdate[][] malformed =
        [
            [new CanonicalCapabilityCatalogLoadUpdate(0, Result: null)],
            [
                new CanonicalCapabilityCatalogLoadUpdate(Progress: null, success),
                new CanonicalCapabilityCatalogLoadUpdate(Progress: null, success),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(0.4, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(Progress: null, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(double.NaN, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(double.PositiveInfinity, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success),
            ],
            [new CanonicalCapabilityCatalogLoadUpdate(Progress: null, success)],
            [
                new CanonicalCapabilityCatalogLoadUpdate(0.5, Failure(retainedLastKnownGood: false)),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(1, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success),
            ],
        ];

        foreach (CanonicalCapabilityCatalogLoadUpdate[] updates in malformed)
        {
            using ShellPreloadSession session = CreateSession();
            int applications = 0;
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.RunCatalogAsync(
                    new ScriptedLoader(updates),
                    _ =>
                    {
                        applications++;
                        return ValueTask.CompletedTask;
                    },
                    retry: false,
                    TestContext.Current.CancellationToken));

            Assert.Equal(0, applications);
            Assert.Equal(
                ShellPreloadStageState.Failed,
                session.CatalogStage.CurrentAttempt?.State);
        }
    }

    /// <summary>Cancellation consumes the attempt, publishes no UI state, and drains cooperatively.</summary>
    [Fact]
    public async Task CancellationStopsTheActiveAttemptBeforeApplication()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using ShellPreloadSession session = CreateSession();
        int applications = 0;
        Task load = session.RunCatalogAsync(
            new BlockingLoader(entered),
            _ =>
            {
                applications++;
                return ValueTask.CompletedTask;
            },
            retry: false,
            TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await session.CancelAndDrainAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await load);
        Assert.Equal(0, applications);
        Assert.Equal(
            ShellPreloadStageState.Cancelled,
            session.CatalogStage.CurrentAttempt?.State);
    }

    /// <summary>A catalog that ignores cancellation cannot publish after the bounded close drain.</summary>
    [Fact]
    public async Task CatalogDrainTimeoutInvalidatesLateUpdates()
    {
        TaskCompletionSource entered = NewSignal();
        TaskCompletionSource release = NewSignal();
        int reports = 0;
        using var session = new ShellPreloadSession(
            _ => reports++,
            Text,
            drainTimeout: TimeSpan.FromMilliseconds(20));
        Task load = session.RunCatalogAsync(
            new UncooperativeLoader(entered, release),
            static _ => ValueTask.CompletedTask,
            retry: false,
            TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Task cancel = session.CancelAndDrainAsync();
        Assert.Equal(ShellPreloadStageState.Cancelled, session.CatalogStage.State);
        await cancel;
        int reportsAfterDrain = reports;
        _ = release.TrySetResult();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await load);

        Assert.Equal(reportsAfterDrain, reports);
    }

    /// <summary>A success terminal does not apply or complete until the typed stream drains.</summary>
    [Fact]
    public async Task SuccessfulTerminalWaitsForStreamDrainBeforeApplication()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        var terminalObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEndOfStream = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using ShellPreloadSession session = CreateSession();
        int applications = 0;
        Task<CapabilityCatalogReloadResult> load = session.RunCatalogAsync(
            new GatedTerminalLoader(success, terminalObserved, releaseEndOfStream),
            _ =>
            {
                applications++;
                return ValueTask.CompletedTask;
            },
            retry: false,
            TestContext.Current.CancellationToken);

        await terminalObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Assert.Equal(0, applications);
        Assert.Equal(
            ShellPreloadStageState.Running,
            session.CatalogStage.CurrentAttempt?.State);

        _ = releaseEndOfStream.TrySetResult();
        Assert.Same(success, await load);
        Assert.Equal(1, applications);
        Assert.Equal(
            ShellPreloadStageState.Succeeded,
            session.CatalogStage.CurrentAttempt?.State);
    }

    /// <summary>Retry keeps the session generation and retains only the immediately prior terminal.</summary>
    [Fact]
    public async Task RepeatedRetriesUseFreshAttemptsWithBoundedHistory()
    {
        CapabilityCatalogReloadResult failure = Failure(retainedLastKnownGood: false);
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        var terminals = new List<ShellPreloadAttemptIdentity>();
        using ShellPreloadSession session = CreateSession(stage =>
        {
            if (stage.CurrentAttempt?.State is not ShellPreloadStageState.Running)
            {
                terminals.Add(stage.CurrentAttempt!.Identity);
            }
        });

        _ = await session.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(Progress: null, failure)),
            static _ => ValueTask.CompletedTask,
            retry: false,
            TestContext.Current.CancellationToken);
        _ = await session.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(Progress: null, failure)),
            static _ => ValueTask.CompletedTask,
            retry: true,
            TestContext.Current.CancellationToken);
        _ = await session.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(Progress: null, failure)),
            static _ => ValueTask.CompletedTask,
            retry: true,
            TestContext.Current.CancellationToken);
        _ = await session.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
            static _ => ValueTask.CompletedTask,
            retry: true,
            TestContext.Current.CancellationToken);

        Assert.True(session.Generation > 0);
        Assert.Equal(4, session.CatalogStage.CurrentAttempt?.Identity.AttemptNumber);
        Assert.Equal(ShellPreloadStageState.Succeeded, session.CatalogStage.CurrentAttempt?.State);
        Assert.Equal(3, session.CatalogStage.PreviousAttempt?.Identity.AttemptNumber);
        Assert.Equal(ShellPreloadStageState.Failed, session.CatalogStage.PreviousAttempt?.State);
        Assert.Null(session.CatalogStage.PreviousAttempt?.Progress);
        Assert.Null(session.CatalogStage.PreviousAttempt?.CompletedWork);
        Assert.Null(session.CatalogStage.PreviousAttempt?.TotalWork);
        Assert.False(session.CanRetryCatalog);
        Assert.Equal(2, ShellPreloadSession.OptionalWorkerBudget);
        Assert.Equal([1, 2, 3, 4], terminals.Select(static identity => identity.AttemptNumber));
        using ShellPreloadSession replacement = CreateSession();
        Assert.True(replacement.Generation > session.Generation);
    }

    /// <summary>An application failure is terminal and a fresh retry can apply once.</summary>
    [Fact]
    public async Task ApplicationFailureNeverCompletesAndRetrySucceeds()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        using ShellPreloadSession session = CreateSession();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RunCatalogAsync(
                new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
                static _ => throw new InvalidOperationException("Presentation failed."),
                retry: false,
                TestContext.Current.CancellationToken));

        Assert.Equal(ShellPreloadStageState.Failed, session.CatalogStage.CurrentAttempt?.State);
        int applications = 0;
        CapabilityCatalogReloadResult retry = await session.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
            _ =>
            {
                applications++;
                return ValueTask.CompletedTask;
            },
            retry: true,
            TestContext.Current.CancellationToken);

        Assert.Same(success, retry);
        Assert.Equal(1, applications);
        Assert.Equal(2, session.CatalogStage.CurrentAttempt?.Identity.AttemptNumber);
        Assert.Equal(1, session.CatalogStage.PreviousAttempt?.Identity.AttemptNumber);
        Assert.Equal(ShellPreloadStageState.Failed, session.CatalogStage.PreviousAttempt?.State);
    }

    /// <summary>Optional work applies the launch page first, respects report dependencies, and bounds workers.</summary>
    [Fact]
    public async Task OptionalStagesPreserveDependenciesAndBoundWorkers()
    {
        using ShellPreloadSession session = CreateSession(includeStartupReport: true);
        session.AdoptReadyCatalog();
        session.SetReducedMotion(true);
        session.PropertyChanged += static (_, _) => throw new InvalidOperationException("status observer failed");
        var events = new ConcurrentQueue<string>();
        TaskCompletionSource historyStarted = NewSignal();
        TaskCompletionSource diagnosticsStarted = NewSignal();
        TaskCompletionSource releaseHistory = NewSignal();
        TaskCompletionSource releaseDiagnostics = NewSignal();
        object workerGate = new();
        int activeWorkers = 0;
        int maximumWorkers = 0;

        async Task RunWorkerAsync(
            string name,
            TaskCompletionSource started,
            TaskCompletionSource release,
            CancellationToken cancellationToken)
        {
            int active = Interlocked.Increment(ref activeWorkers);
            lock (workerGate)
            {
                maximumWorkers = Math.Max(maximumWorkers, active);
            }
            events.Enqueue($"{name}-start");
            _ = started.TrySetResult();
            try
            {
                await release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                events.Enqueue($"{name}-end");
                _ = Interlocked.Decrement(ref activeWorkers);
            }
        }

        Task preload = session.RunOptionalStagesAsync(
            new(
                () =>
                {
                    events.Enqueue("launch");
                    throw new InvalidOperationException("launch observer failed");
                },
                token => RunWorkerAsync("history", historyStarted, releaseHistory, token),
                (progress, _) =>
                {
                    events.Enqueue("report");
                    progress(0, 100);
                    progress(50, 100);
                    string accessibleDecile = session.AccessibleStatus;
                    progress(51, 100);
                    Assert.Equal("51%", Stage(session, ShellPreloadSession.ReportStageId).ProgressLabel);
                    Assert.Equal(accessibleDecile, session.AccessibleStatus);
                    progress(100, 100);
                    return Task.CompletedTask;
                },
                token => RunWorkerAsync("diagnostics", diagnosticsStarted, releaseDiagnostics, token),
                async (progress, isCurrent, token) =>
                {
                    events.Enqueue("views");
                    Assert.True(isCurrent());
                    for (int index = 1; index <= 5; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        progress(index, 5);
                        await Task.Yield();
                    }
                }),
            TestContext.Current.CancellationToken);

        await Task.WhenAll(historyStarted.Task, diagnosticsStarted.Task).WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(Stage(session, ShellPreloadSession.DiagnosticsStageId).IsIndeterminate);
        Assert.Equal("launch", events.First());
        Assert.DoesNotContain("report", events);
        _ = releaseHistory.TrySetResult();
        await WaitUntilAsync(() => events.Contains("report"));
        _ = releaseDiagnostics.TrySetResult();
        await preload;

        Assert.True(maximumWorkers <= ShellPreloadSession.OptionalWorkerBudget);
        Assert.True(events.ToList().IndexOf("history-end") < events.ToList().IndexOf("report"));
        Assert.All(session.Stages, stage => Assert.Equal(ShellPreloadStageState.Succeeded, stage.State));
        ShellPreloadStageSnapshot views = session.Stages.Single(stage => stage.Id == ShellPreloadSession.ViewsStageId);
        Assert.Equal(1, views.Progress);
        Assert.Equal(5, views.CurrentAttempt?.CompletedWork);
        Assert.Equal(5, views.CurrentAttempt?.TotalWork);
        Assert.Equal("5 / 5", views.WorkLabel);
        Assert.Contains("100%", views.AccessibleStatus, StringComparison.Ordinal);
        ShellPreloadStageSnapshot report = Stage(session, ShellPreloadSession.ReportStageId);
        Assert.Equal(1, report.Progress);
        Assert.Equal(100, report.CurrentAttempt?.CompletedWork);
        Assert.Equal(100, report.CurrentAttempt?.TotalWork);
        Assert.Equal("100 / 100", report.WorkLabel);
        Assert.Contains(report.Title, report.RetryAccessibleLabel, StringComparison.Ordinal);
        Assert.Contains(report.Title, report.SkipAccessibleLabel, StringComparison.Ordinal);
    }

    /// <summary>One admitted retry remains inside the initial lifecycle and releases its successor exactly once.</summary>
    [Fact]
    public async Task OptionalRetryIsDrainedBeforeInitialLifecycleCompletes()
    {
        using ShellPreloadSession session = CreateSession(includeStartupReport: true);
        session.AdoptReadyCatalog();
        TaskCompletionSource diagnosticsStarted = NewSignal();
        TaskCompletionSource releaseDiagnostics = NewSignal();
        TaskCompletionSource retryStarted = NewSignal();
        TaskCompletionSource releaseRetry = NewSignal();
        int historyRuns = 0;
        int reportRuns = 0;
        int diagnosticsRuns = 0;
        int viewRuns = 0;
        ShellOptionalPreloadWork work = new(
            static () => { },
            async cancellationToken =>
            {
                if (++historyRuns == 1)
                {
                    throw new InvalidOperationException("history failed");
                }
                _ = retryStarted.TrySetResult();
                await releaseRetry.Task.WaitAsync(cancellationToken);
            },
            (_, _) =>
            {
                reportRuns++;
                return Task.CompletedTask;
            },
            async cancellationToken =>
            {
                diagnosticsRuns++;
                _ = diagnosticsStarted.TrySetResult();
                await releaseDiagnostics.Task.WaitAsync(cancellationToken);
            },
            (progress, isCurrent, _) =>
            {
                viewRuns++;
                Assert.True(isCurrent());
                progress(1, 1);
                return Task.CompletedTask;
            });

        Task preload = session.RunOptionalStagesAsync(work, TestContext.Current.CancellationToken);
        await diagnosticsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => Stage(session, ShellPreloadSession.HistoryStageId).State ==
            ShellPreloadStageState.Failed);
        Assert.Equal(ShellPreloadStageState.Failed, Stage(session, ShellPreloadSession.HistoryStageId).State);
        Assert.Equal(ShellPreloadStageState.DependencyBlocked, Stage(session, ShellPreloadSession.ReportStageId).State);
        Assert.Equal("history failed", Stage(session, ShellPreloadSession.HistoryStageId).CurrentAttempt?.Diagnostic);

        Task<bool> retry = session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken);
        await retryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _ = releaseDiagnostics.TrySetResult();
        await WaitUntilAsync(() => Stage(session, ShellPreloadSession.DiagnosticsStageId).State ==
            ShellPreloadStageState.Succeeded);
        Assert.False(preload.IsCompleted);
        _ = releaseRetry.TrySetResult();
        await preload;
        Assert.True(await retry);

        ShellPreloadStageSnapshot history = Stage(session, ShellPreloadSession.HistoryStageId);
        Assert.Equal(2, history.CurrentAttempt?.Identity.AttemptNumber);
        Assert.Equal(1, history.PreviousAttempt?.Identity.AttemptNumber);
        Assert.Null(history.PreviousAttempt?.Progress);
        Assert.Null(history.PreviousAttempt?.CompletedWork);
        Assert.Null(history.PreviousAttempt?.TotalWork);
        Assert.Equal(ShellPreloadStageState.Succeeded, history.State);
        Assert.Equal(ShellPreloadStageState.Succeeded, Stage(session, ShellPreloadSession.ReportStageId).State);
        Assert.Equal(1, reportRuns);
        Assert.Equal(1, diagnosticsRuns);
        Assert.Equal(1, viewRuns);
    }

    /// <summary>Skipping a failed dependency keeps its successor explicit and leaves no cancellable work.</summary>
    [Fact]
    public async Task OptionalSkipKeepsSuccessorDependencyBlocked()
    {
        using ShellPreloadSession session = CreateSession(includeStartupReport: true);
        session.AdoptReadyCatalog();
        await session.RunOptionalStagesAsync(
            new(
                static () => { },
                static _ => Task.FromException(new InvalidOperationException("history failed")),
                static (_, _) => Task.CompletedTask,
                static _ => Task.CompletedTask,
                static (progress, isCurrent, _) =>
                {
                    Assert.True(isCurrent());
                    progress(1, 1);
                    return Task.CompletedTask;
                }),
            TestContext.Current.CancellationToken);

        Assert.True(session.TrySkipOptional(ShellPreloadSession.HistoryStageId));
        Assert.False(session.TrySkipOptional(ShellPreloadSession.HistoryStageId));

        Assert.Equal(ShellPreloadStageState.Skipped, Stage(session, ShellPreloadSession.HistoryStageId).State);
        Assert.Equal(ShellPreloadStageState.DependencyBlocked, Stage(session, ShellPreloadSession.ReportStageId).State);
        Assert.True(session.CanCancelOptionals);
        await session.CancelOptionalsAndDrainAsync();
        Assert.Equal(ShellPreloadStageState.Cancelled, Stage(session, ShellPreloadSession.ReportStageId).State);
    }

    /// <summary>A delayed callback from a failed attempt cannot update its retry.</summary>
    [Fact]
    public async Task OptionalRetryRejectsDelayedPriorAttemptProgress()
    {
        using ShellPreloadSession session = CreateSession();
        session.AdoptReadyCatalog();
        Action<int, int>? oldProgress = null;
        Func<bool>? oldIsCurrent = null;
        TaskCompletionSource retryStarted = NewSignal();
        TaskCompletionSource releaseRetry = NewSignal();
        int viewRuns = 0;
        await session.RunOptionalStagesAsync(
            new(
                static () => { },
                static _ => Task.CompletedTask,
                null,
                static _ => Task.CompletedTask,
                async (progress, isCurrent, cancellationToken) =>
                {
                    viewRuns++;
                    if (viewRuns == 1)
                    {
                        Assert.True(isCurrent());
                        oldProgress = progress;
                        oldIsCurrent = isCurrent;
                        progress(1, 5);
                        throw new InvalidOperationException("view failed");
                    }

                    _ = retryStarted.TrySetResult();
                    await releaseRetry.Task.WaitAsync(cancellationToken);
                    Assert.True(isCurrent());
                    progress(5, 5);
                }),
            TestContext.Current.CancellationToken);

        Task<bool> retry = session.TryRetryOptionalAsync(
            ShellPreloadSession.ViewsStageId,
            TestContext.Current.CancellationToken);
        await retryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(await session.TryRetryOptionalAsync(
            ShellPreloadSession.ViewsStageId,
            TestContext.Current.CancellationToken));
        Assert.False(oldIsCurrent!());
        _ = Assert.Throws<InvalidOperationException>(() => oldProgress!(2, 5));
        _ = releaseRetry.TrySetResult();
        Assert.True(await retry);
    }

    /// <summary>Optional cancellation drains only remaining preload work and permits an explicit later retry.</summary>
    [Fact]
    public async Task OptionalCancellationDrainsRunningStagesWithoutCancellingCatalog()
    {
        using ShellPreloadSession session = CreateSession();
        session.AdoptReadyCatalog();
        TaskCompletionSource diagnosticsStarted = NewSignal();
        TaskCompletionSource viewsStarted = NewSignal();
        int historyRuns = 0;

        static async Task BlockAsync(TaskCompletionSource started, CancellationToken cancellationToken)
        {
            _ = started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        Task preload = session.RunOptionalStagesAsync(
            new(
                static () => { },
                _ => ++historyRuns == 1
                    ? Task.FromException(new InvalidOperationException("history failed"))
                    : Task.CompletedTask,
                null,
                token => BlockAsync(diagnosticsStarted, token),
                (_, isCurrent, token) =>
                {
                    Assert.True(isCurrent());
                    return BlockAsync(viewsStarted, token);
                }),
            TestContext.Current.CancellationToken);
        await Task.WhenAll(diagnosticsStarted.Task, viewsStarted.Task).WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await session.CancelOptionalsAndDrainAsync();
        await preload;

        Assert.Equal(ShellPreloadStageState.Succeeded, session.CatalogStage.State);
        Assert.Equal(ShellPreloadStageState.Failed, Stage(session, ShellPreloadSession.HistoryStageId).State);
        Assert.All(session.Stages.Where(stage => !stage.IsRequired &&
                stage.Id != ShellPreloadSession.HistoryStageId),
            stage => Assert.Equal(ShellPreloadStageState.Cancelled, stage.State));

        Assert.True(await session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken));
        Assert.Equal(ShellPreloadStageState.Succeeded, Stage(session, ShellPreloadSession.HistoryStageId).State);
        Assert.Equal(2, historyRuns);
    }

    /// <summary>An optional worker that ignores cancellation cannot publish after its bounded drain.</summary>
    [Fact]
    public async Task OptionalDrainTimeoutInvalidatesLateProgress()
    {
        int reports = 0;
        int historyRuns = 0;
        using var session = new ShellPreloadSession(
            _ => reports++,
            Text,
            drainTimeout: TimeSpan.FromMilliseconds(20));
        session.AdoptReadyCatalog();
        TaskCompletionSource started = NewSignal();
        TaskCompletionSource release = NewSignal();
        Action<int, int>? delayedProgress = null;
        Task preload = session.RunOptionalStagesAsync(
            new(
                static () => { },
                _ => ++historyRuns == 1
                    ? Task.FromException(new InvalidOperationException("history failed"))
                    : Task.CompletedTask,
                null,
                static _ => Task.CompletedTask,
                async (progress, isCurrent, cancellationToken) =>
                {
                    delayedProgress = progress;
                    _ = started.TrySetResult();
                    await release.Task;
                    Assert.False(isCurrent());
                }),
            TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Task cancel = session.CancelOptionalsAndDrainAsync();
        Assert.Equal(ShellPreloadStageState.Cancelled,
            Stage(session, ShellPreloadSession.ViewsStageId).State);
        await cancel;
        _ = Assert.Throws<InvalidOperationException>(() => delayedProgress!(1, 1));
        Assert.False(await session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken));
        _ = release.TrySetResult();
        await preload;
        Assert.True(await session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken));
        int reportsAfterRetry = reports;

        Assert.Equal(reportsAfterRetry, reports);
        Assert.Equal(2, historyRuns);
    }

}
