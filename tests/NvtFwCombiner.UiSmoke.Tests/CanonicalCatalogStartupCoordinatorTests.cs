using System.Runtime.CompilerServices;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Behavioral coverage for the Presentation-owned catalog startup sequence.</summary>
public sealed class CanonicalCatalogStartupCoordinatorTests
{
    /// <summary>Real route progress is coalesced into readable deciles before one successful publication.</summary>
    [Fact]
    public async Task SuccessfulLoadReportsOrderedPhasesAndPublishesExactlyOnce()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        CanonicalCapabilityCatalogLoadUpdate[] updates =
        [
            .. Enumerable.Range(0, 80)
                .Select(completed => new CanonicalCapabilityCatalogLoadUpdate(
                    completed / 80d,
                    Result: null)),
            new CanonicalCapabilityCatalogLoadUpdate(1, success),
        ];
        var events = new List<string>();
        var reports = new List<CanonicalCatalogStartupProgress>();
        int publications = 0;

        CapabilityCatalogReloadResult result =
            await CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                new ScriptedLoader(updates),
                (progress, _) =>
                {
                    reports.Add(progress);
                    events.Add($"progress:{progress.Value:0.0}:{progress.Phase}");
                    return ValueTask.CompletedTask;
                },
                _ =>
                {
                    publications++;
                    events.Add("publish");
                    return ValueTask.CompletedTask;
                },
                TestContext.Current.CancellationToken);

        Assert.Same(success, result);
        Assert.Equal(1, publications);
        Assert.Equal(
            [0.1, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1],
            reports.Select(static report => report.Value));
        Assert.Equal(CanonicalCatalogStartupPhase.Dispatched, reports[0].Phase);
        Assert.All(
            reports.Skip(1).Take(8),
            static report => Assert.Equal(
                CanonicalCatalogStartupPhase.MaterializingRoutes,
                report.Phase));
        Assert.Equal(CanonicalCatalogStartupPhase.ApplyingState, reports[^2].Phase);
        Assert.Equal(CanonicalCatalogStartupPhase.Ready, reports[^1].Phase);
        Assert.Equal(
            [
                "progress:0.9:ApplyingState",
                "publish",
                "progress:1.0:Ready",
            ],
            events.TakeLast(3));
    }

    /// <summary>A typed cold or last-known-good failure never projects successful UI state.</summary>
    [Fact]
    public async Task TypedFailureStopsBeforeApplyingAndPublishing()
    {
        CapabilityCatalogReloadResult published = await LoadSuccessfulResultAsync();
        CapabilityCatalogReloadResult[] failures =
        [
            Failure(retainedLastKnownGood: false),
            Failure(retainedLastKnownGood: true, published.Snapshot),
        ];

        foreach (CapabilityCatalogReloadResult failure in failures)
        {
            var reports = new List<CanonicalCatalogStartupProgress>();
            int publications = 0;
            CapabilityCatalogReloadResult result =
                await CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                    new ScriptedLoader(
                        new CanonicalCapabilityCatalogLoadUpdate(0, Result: null),
                        new CanonicalCapabilityCatalogLoadUpdate(0.4, Result: null),
                        new CanonicalCapabilityCatalogLoadUpdate(Progress: null, failure)),
                    (progress, _) =>
                    {
                        reports.Add(progress);
                        return ValueTask.CompletedTask;
                    },
                    _ =>
                    {
                        publications++;
                        return ValueTask.CompletedTask;
                    },
                    TestContext.Current.CancellationToken);

            Assert.Same(failure, result);
            Assert.Equal(0, publications);
            Assert.DoesNotContain(
                reports,
                static report => report.Phase is
                    CanonicalCatalogStartupPhase.ApplyingState or
                    CanonicalCatalogStartupPhase.Ready);
        }
    }

    /// <summary>Missing, duplicate, post-terminal, and decreasing updates fail closed.</summary>
    [Fact]
    public async Task MalformedStreamsCannotPublish()
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
                new CanonicalCapabilityCatalogLoadUpdate(Progress: null, success),
                new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(0.4, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(Progress: null, success),
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
                new CanonicalCapabilityCatalogLoadUpdate(
                    0.5,
                    Failure(retainedLastKnownGood: false)),
            ],
            [
                new CanonicalCapabilityCatalogLoadUpdate(1, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success),
            ],
        ];

        foreach (CanonicalCapabilityCatalogLoadUpdate[] updates in malformed)
        {
            int publications = 0;
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                    new ScriptedLoader(updates),
                    static (_, _) => ValueTask.CompletedTask,
                    _ =>
                    {
                        publications++;
                        return ValueTask.CompletedTask;
                    },
                    TestContext.Current.CancellationToken));
            Assert.Equal(0, publications);
        }
    }

    /// <summary>Cancellation while a stream is active stops before applying or publishing.</summary>
    [Fact]
    public async Task CancellationStopsActiveStreamBeforePublication()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        int publications = 0;
        Task load = CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
            new BlockingLoader(entered),
            static (_, _) => ValueTask.CompletedTask,
            _ =>
            {
                publications++;
                return ValueTask.CompletedTask;
            },
            cancellation.Token);

        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await load);
        Assert.Equal(0, publications);
    }

    /// <summary>A successful terminal item does not advance to 80 or apply state until the stream drains.</summary>
    [Fact]
    public async Task SuccessfulTerminalWaitsForEndOfStreamBeforeApplying()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        var terminalObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEndOfStream = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reports = new List<CanonicalCatalogStartupProgress>();
        int publications = 0;
        Task<CapabilityCatalogReloadResult> load =
            CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                new GatedTerminalLoader(success, terminalObserved, releaseEndOfStream),
                (progress, _) =>
                {
                    reports.Add(progress);
                    return ValueTask.CompletedTask;
                },
                _ =>
                {
                    publications++;
                    return ValueTask.CompletedTask;
                },
                TestContext.Current.CancellationToken);

        await terminalObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(reports, static report => report.Value >= 0.8);
        Assert.Equal(0, publications);

        _ = releaseEndOfStream.TrySetResult();
        Assert.Same(success, await load);
        Assert.Equal(1, publications);
        Assert.Equal(1, reports[^1].Value);
    }

    /// <summary>A UI-state application failure never reports Ready and a later attempt can succeed.</summary>
    [Fact]
    public async Task ApplyingStateFailureNeverReportsReadyAndRetryCanSucceed()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        var reports = new List<CanonicalCatalogStartupProgress>();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
                (progress, _) =>
                {
                    reports.Add(progress);
                    return ValueTask.CompletedTask;
                },
                static _ => throw new InvalidOperationException("Presentation failed."),
                TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            reports,
            static report => report.Phase == CanonicalCatalogStartupPhase.Ready);

        int publications = 0;
        CapabilityCatalogReloadResult retry =
            await CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
                static (_, _) => ValueTask.CompletedTask,
                _ =>
                {
                    publications++;
                    return ValueTask.CompletedTask;
                },
                TestContext.Current.CancellationToken);
        Assert.Same(success, retry);
        Assert.Equal(1, publications);
    }

    private static async Task<CapabilityCatalogReloadResult> LoadSuccessfulResultAsync()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("catalog-startup-test");
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
        public IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            CancellationToken cancellationToken)
        {
            return ReadAsync(updates, cancellationToken);
        }

        private static async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> ReadAsync(
            IReadOnlyList<CanonicalCapabilityCatalogLoadUpdate> updates,
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
