using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Behavioral coverage for the sole Presentation shell preload lifecycle.</summary>
public sealed class ShellPreloadSessionTests
{
    /// <summary>The required catalog stage keeps typed progress and completes only after UI publication.</summary>
    [Fact]
    public async Task RequiredCatalogStagePublishesOneTypedAttempt()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        using var session = new ShellPreloadSession(static _ => { });
        var events = new List<string>();
        ((INotifyCollectionChanged)session.Stages).CollectionChanged += (_, _) =>
        {
            ShellPreloadAttemptSnapshot? attempt = session.CatalogStage.CurrentAttempt;
            events.Add($"{attempt?.Identity.AttemptNumber}:{attempt?.State}:{attempt?.Progress:0.00}");
        };

        CapabilityCatalogReloadResult result = await session.RunCatalogAsync(
            new ScriptedLoader(
                new CanonicalCapabilityCatalogLoadUpdate(0, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success)),
            _ =>
            {
                events.Add("apply");
                return ValueTask.CompletedTask;
            },
            retry: false,
            TestContext.Current.CancellationToken);

        Assert.Same(success, result);
        Assert.True(session.Generation > 0);
        ShellPreloadAttemptSnapshot attempt = Assert.IsType<ShellPreloadAttemptSnapshot>(
            session.CatalogStage.CurrentAttempt);
        Assert.Equal(1, attempt.Identity.AttemptNumber);
        Assert.Equal(ShellPreloadAttemptState.Succeeded, attempt.State);
        Assert.Equal(1, attempt.Progress);
        Assert.Null(session.CatalogStage.PreviousAttempt);
        Assert.Equal("apply", events[^2]);
        Assert.EndsWith(":Succeeded:1.00", events[^1], StringComparison.Ordinal);
    }

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
            using var session = new ShellPreloadSession(static _ => { });
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
            Assert.Equal(ShellPreloadAttemptState.Failed, attempt.State);
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
            using var session = new ShellPreloadSession(static _ => { });
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
                ShellPreloadAttemptState.Failed,
                session.CatalogStage.CurrentAttempt?.State);
        }
    }

    /// <summary>Cancellation consumes the attempt, publishes no UI state, and drains cooperatively.</summary>
    [Fact]
    public async Task CancellationStopsTheActiveAttemptBeforeApplication()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new ShellPreloadSession(static _ => { });
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
            ShellPreloadAttemptState.Cancelled,
            session.CatalogStage.CurrentAttempt?.State);
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
        using var session = new ShellPreloadSession(static _ => { });
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
            ShellPreloadAttemptState.Running,
            session.CatalogStage.CurrentAttempt?.State);

        _ = releaseEndOfStream.TrySetResult();
        Assert.Same(success, await load);
        Assert.Equal(1, applications);
        Assert.Equal(
            ShellPreloadAttemptState.Succeeded,
            session.CatalogStage.CurrentAttempt?.State);
    }

    /// <summary>Retry keeps the session generation and retains only the immediately prior terminal.</summary>
    [Fact]
    public async Task RepeatedRetriesUseFreshAttemptsWithBoundedHistory()
    {
        CapabilityCatalogReloadResult failure = Failure(retainedLastKnownGood: false);
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        var terminals = new List<ShellPreloadAttemptIdentity>();
        using var session = new ShellPreloadSession(stage =>
        {
            if (stage.CurrentAttempt?.State is not ShellPreloadAttemptState.Running)
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
        Assert.Equal(ShellPreloadAttemptState.Succeeded, session.CatalogStage.CurrentAttempt?.State);
        Assert.Equal(3, session.CatalogStage.PreviousAttempt?.Identity.AttemptNumber);
        Assert.Equal(ShellPreloadAttemptState.Failed, session.CatalogStage.PreviousAttempt?.State);
        Assert.False(session.CanRetryCatalog);
        Assert.Equal(2, ShellPreloadSession.OptionalWorkerBudget);
        Assert.Equal([1, 2, 3, 4], terminals.Select(static identity => identity.AttemptNumber));
        using var replacement = new ShellPreloadSession(static _ => { });
        Assert.True(replacement.Generation > session.Generation);
    }

    /// <summary>An application failure is terminal and a fresh retry can apply once.</summary>
    [Fact]
    public async Task ApplicationFailureNeverCompletesAndRetrySucceeds()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        using var session = new ShellPreloadSession(static _ => { });
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RunCatalogAsync(
                new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
                static _ => throw new InvalidOperationException("Presentation failed."),
                retry: false,
                TestContext.Current.CancellationToken));

        Assert.Equal(ShellPreloadAttemptState.Failed, session.CatalogStage.CurrentAttempt?.State);
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
        Assert.Equal(ShellPreloadAttemptState.Failed, session.CatalogStage.PreviousAttempt?.State);
    }

    private static async Task<CapabilityCatalogReloadResult> LoadSuccessfulResultAsync()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("shell-preload-test");
        CapabilityCatalogReloadResult result = await PresentationTestHost.LoadCanonicalCatalogAsync(
            services.CanonicalCatalogLoader,
            TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Snapshot);
        return result;
    }

    private static CapabilityCatalogReloadResult Failure(
        bool retainedLastKnownGood,
        CanonicalCapabilityCatalogSnapshot? snapshot = null)
    {
        return new CapabilityCatalogReloadResult(
            Succeeded: false,
            retainedLastKnownGood,
            snapshot,
            [new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.SourceInvalid, "Invalid catalog.")]);
    }

    private sealed class ScriptedLoader(
        params CanonicalCapabilityCatalogLoadUpdate[] updates) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (CanonicalCapabilityCatalogLoadUpdate update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return update;
            }
        }
    }

    private sealed class BlockingLoader(TaskCompletionSource entered) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CanonicalCapabilityCatalogLoadUpdate(0, Result: null);
            _ = entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class GatedTerminalLoader(
        CapabilityCatalogReloadResult result,
        TaskCompletionSource terminalObserved,
        TaskCompletionSource releaseEndOfStream) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CanonicalCapabilityCatalogLoadUpdate(0, Result: null);
            yield return new CanonicalCapabilityCatalogLoadUpdate(1, result);
            _ = terminalObserved.TrySetResult();
            await releaseEndOfStream.Task.WaitAsync(cancellationToken);
        }
    }
}
