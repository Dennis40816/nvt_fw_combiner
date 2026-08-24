using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class CanonicalCapabilityCatalogMigrationTests
{
    /// <summary>The production source emits each real route completion before one typed terminal result.</summary>
    [Fact]
    public async Task SourceReportsCanonicalRouteMaterializationProgress()
    {
        Assert.NotNull(typeof(CompositionHostServices).GetMethod(
            nameof(CompositionHostServices.Create),
            Type.EmptyTypes));
        var host = CompositionHostServices.Create();

        List<CanonicalCapabilityCatalogLoadUpdate> first = await ReadUpdatesAsync(
            host.CanonicalCatalogLoader.LoadAsync(TestContext.Current.CancellationToken));
        CapabilityCatalogReloadResult firstResult = TerminalResult(first);
        Assert.True(firstResult.Succeeded);
        List<double> reports = ProgressValues(first);

        CanonicalCapabilityCatalogSnapshot snapshot = host.Catalog.GetCurrentSnapshot();
        int routeCount = snapshot.Capabilities.Count + snapshot.DynamicRoutes.Count;
        Assert.True(routeCount > 1);
        Assert.Equal(
            Enumerable.Range(0, routeCount + 1)
                .Select(completed => (double)completed / (routeCount + 1))
                .Append(1),
            reports);

        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);

        List<CanonicalCapabilityCatalogLoadUpdate> retry = await ReadUpdatesAsync(
            host.CanonicalCatalogLoader.LoadAsync(TestContext.Current.CancellationToken));
        Assert.True(TerminalResult(retry).Succeeded);
        Assert.Equal(reports, ProgressValues(retry));

        CanonicalCapabilityCatalogSnapshot published = host.Catalog.GetCurrentSnapshot();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReadUpdatesAsync(
            host.CanonicalCatalogLoader.LoadAsync(cancelled.Token)));
        Assert.Same(published, host.Catalog.GetCurrentSnapshot());
    }

    /// <summary>One accepted policy revision classifies and resolves each exact route once.</summary>
    [Fact]
    public void SourceEvaluatesEachRouteOnceBeforeOneDisclosure()
    {
        CanonicalCapabilityPolicySnapshot policy =
            BuiltInCanonicalCapabilityPolicy.Load();
        var classifications = new Dictionary<string, int>(StringComparer.Ordinal);
        var resolutions = new Dictionary<string, int>(StringComparer.Ordinal);
        int policyLoads = 0;
        int disclosureLoads = 0;
        var source = new CanonicalCapabilityCatalogSource(
            () =>
            {
                policyLoads++;
                return policy;
            },
            identity =>
            {
                classifications[identity.RouteId] =
                    classifications.GetValueOrDefault(identity.RouteId) + 1;
                return CanonicalDynamicRouteInventory.IsDynamic(identity);
            },
            identity =>
            {
                resolutions[identity.RouteId] =
                    resolutions.GetValueOrDefault(identity.RouteId) + 1;
                return CanonicalCompiledRouteInventory.Resolve(identity);
            },
            identity =>
            {
                resolutions[identity.RouteId] =
                    resolutions.GetValueOrDefault(identity.RouteId) + 1;
                return CanonicalDynamicRouteInventory.Resolve(identity);
            },
            (definitions, dynamicDefinitions) =>
            {
                disclosureLoads++;
                return CanonicalCapabilityDisclosureInventory.Create(
                    definitions,
                    dynamicDefinitions);
            });

        CapabilityCatalogLoadResult result = source.Load(
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, policyLoads);
        Assert.Equal(1, disclosureLoads);
        Assert.Equal(policy.Routes.Count, classifications.Count);
        Assert.Equal(policy.Routes.Count, resolutions.Count);
        Assert.All(policy.Routes, route =>
        {
            Assert.Equal(1, classifications[route.Identity.RouteId]);
            Assert.Equal(1, resolutions[route.Identity.RouteId]);
        });
    }

    /// <summary>Concurrent operations retain distinct FIFO progress and terminal-result streams.</summary>
    [Fact]
    public async Task ConcurrentCatalogLoadsKeepProgressRequestScoped()
    {
        var firstEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int invocation = 0;
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource(
                () =>
                {
                    int current = Interlocked.Increment(ref invocation);
                    if (current == 1)
                    {
                        _ = firstEntered.TrySetResult();
                        releaseFirst.Task.GetAwaiter().GetResult();
                    }

                    return BuiltInCanonicalCapabilityPolicy.Load() with
                    {
                        CatalogVersion = $"parallel-{current}",
                        SourceSha256 = new string(current == 1 ? 'a' : 'b', 64),
                    };
                }));

        Task<List<CanonicalCapabilityCatalogLoadUpdate>> first = ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        await firstEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Task<List<CanonicalCapabilityCatalogLoadUpdate>> second = ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        _ = releaseFirst.TrySetResult();

        List<CanonicalCapabilityCatalogLoadUpdate>[] operations =
            await Task.WhenAll(first, second);
        CapabilityCatalogReloadResult firstResult = TerminalResult(operations[0]);
        CapabilityCatalogReloadResult secondResult = TerminalResult(operations[1]);
        Assert.True(firstResult.Succeeded);
        Assert.True(secondResult.Succeeded);
        Assert.Equal("parallel-1", firstResult.Snapshot!.CatalogVersion);
        Assert.Equal("parallel-2", secondResult.Snapshot!.CatalogVersion);
        Assert.NotSame(firstResult.Snapshot, secondResult.Snapshot);
        Assert.NotEqual(
            firstResult.Snapshot.ResolutionToken,
            secondResult.Snapshot.ResolutionToken);
        Assert.Equal(2, invocation);
        Assert.Equal(ProgressValues(operations[0]), ProgressValues(operations[1]));
    }

    /// <summary>Cancellation after route work begins cannot publish and a later request starts cleanly.</summary>
    [Fact]
    public async Task InFlightCancellationRetainsPublicationAndRetryStartsFresh()
    {
        var routeEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRoute = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int attempt = 0;
        int currentAttempt = 0;
        int materializedRoutes = 0;
        var source = new CanonicalCapabilityCatalogSource(
            () =>
            {
                currentAttempt = Interlocked.Increment(ref attempt);
                materializedRoutes = 0;
                return BuiltInCanonicalCapabilityPolicy.Load();
            },
            CanonicalDynamicRouteInventory.IsDynamic,
            identity =>
            {
                if (currentAttempt == 2 && Interlocked.Increment(ref materializedRoutes) == 1)
                {
                    _ = routeEntered.TrySetResult();
                    releaseRoute.Task.GetAwaiter().GetResult();
                }

                return CanonicalCompiledRouteInventory.Resolve(identity);
            },
            CanonicalDynamicRouteInventory.Resolve,
            CanonicalCapabilityDisclosureInventory.Create);
        var catalog = new CanonicalCapabilityCatalog(source);

        List<CanonicalCapabilityCatalogLoadUpdate> seed = await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        CanonicalCapabilityCatalogSnapshot published = TerminalResult(seed).Snapshot!;
        using var cancellation = new CancellationTokenSource();
        var cancelledUpdates = new List<CanonicalCapabilityCatalogLoadUpdate>();
        Task cancelledLoad = ReadUpdatesAsync(
            catalog.LoadAsync(cancellation.Token),
            cancelledUpdates);

        await routeEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        _ = releaseRoute.TrySetResult();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelledLoad);
        Assert.Equal([0d], ProgressValues(cancelledUpdates));
        Assert.Same(published, catalog.GetCurrentSnapshot());

        List<CanonicalCapabilityCatalogLoadUpdate> retry = await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        CapabilityCatalogReloadResult retryResult = TerminalResult(retry);
        Assert.True(retryResult.Succeeded);
        Assert.NotSame(published, retryResult.Snapshot);
        Assert.Equal(3, attempt);
    }

    /// <summary>A typed failure after partial route progress stays terminal and retains the last-known-good snapshot.</summary>
    [Fact]
    public async Task PartialRouteFailureRetainsLastKnownGoodAndRetryIsIsolated()
    {
        int attempt = 0;
        int currentAttempt = 0;
        int materializedRoutes = 0;
        var source = new CanonicalCapabilityCatalogSource(
            () =>
            {
                currentAttempt = Interlocked.Increment(ref attempt);
                materializedRoutes = 0;
                return BuiltInCanonicalCapabilityPolicy.Load();
            },
            CanonicalDynamicRouteInventory.IsDynamic,
            identity => currentAttempt == 2 && Interlocked.Increment(ref materializedRoutes) == 3
                ? throw new InvalidDataException("Route materialization failed after progress.")
                : CanonicalCompiledRouteInventory.Resolve(identity),
            CanonicalDynamicRouteInventory.Resolve,
            CanonicalCapabilityDisclosureInventory.Create);
        var catalog = new CanonicalCapabilityCatalog(source);

        CapabilityCatalogReloadResult seed = TerminalResult(await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken)));

        List<CanonicalCapabilityCatalogLoadUpdate> failed = await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        CapabilityCatalogReloadResult failure = TerminalResult(failed);
        Assert.False(failure.Succeeded);
        Assert.True(failure.RetainedLastKnownGood);
        Assert.Same(seed.Snapshot, failure.Snapshot);
        Assert.Equal(CapabilityCatalogIssueCodes.SourceInvalid, Assert.Single(failure.Issues).Code);
        Assert.Equal(3, ProgressValues(failed).Count);
        Assert.Equal(0, ProgressValues(failed)[0]);
        Assert.DoesNotContain(1d, ProgressValues(failed));

        CapabilityCatalogReloadResult retry = TerminalResult(await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken)));
        Assert.True(retry.Succeeded);
        Assert.NotSame(seed.Snapshot, retry.Snapshot);
        Assert.Equal(3, attempt);
    }

    /// <summary>Typed failure and unexpected exceptions close only their own stream before Retry.</summary>
    [Fact]
    public async Task FailedCatalogLoadClosesProgressAndRetryStartsFresh()
    {
        int invocation = 0;
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource(
                () => Interlocked.Increment(ref invocation) is 1 or 3
                    ? throw new InvalidDataException("Catalog unavailable.")
                    : BuiltInCanonicalCapabilityPolicy.Load()));

        List<CanonicalCapabilityCatalogLoadUpdate> failed = await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        CapabilityCatalogReloadResult failure = TerminalResult(failed);
        Assert.False(failure.Succeeded);
        Assert.Empty(ProgressValues(failed));

        List<CanonicalCapabilityCatalogLoadUpdate> retry = await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        CapabilityCatalogReloadResult success = TerminalResult(retry);
        Assert.True(success.Succeeded);
        Assert.True(ProgressValues(retry).Count > 1);

        List<CanonicalCapabilityCatalogLoadUpdate> retained = await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken));
        CapabilityCatalogReloadResult retainedFailure = TerminalResult(retained);
        Assert.False(retainedFailure.Succeeded);
        Assert.True(retainedFailure.RetainedLastKnownGood);
        Assert.Same(success.Snapshot, retainedFailure.Snapshot);
        Assert.DoesNotContain(1d, ProgressValues(retained));

        var unexpectedCatalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource(
                () => throw new InvalidOperationException("Unexpected failure.")));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadUpdatesAsync(
            unexpectedCatalog.LoadAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>A streamed invalid candidate cannot replace the last-known-good snapshot and Retry is fresh.</summary>
    [Fact]
    public async Task StreamedInvalidCandidateRetainsLastKnownGoodBeforeRetry()
    {
        CapabilityCatalogLoadResult valid =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken);
        Assert.True(valid.Succeeded);
        var invalid = new CanonicalCapabilityCatalogCandidate(
            "canonical-capability-catalog",
            "invalid-empty",
            new string('c', 64),
            []);
        var catalog = new CanonicalCapabilityCatalog(
            new QueuedCandidateSource(
                valid,
                CapabilityCatalogLoadResult.Success(invalid),
                valid));

        CapabilityCatalogReloadResult seed = TerminalResult(await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken)));
        CapabilityCatalogReloadResult rejected = TerminalResult(await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken)));

        Assert.False(rejected.Succeeded);
        Assert.True(rejected.RetainedLastKnownGood);
        Assert.Same(seed.Snapshot, rejected.Snapshot);
        Assert.Equal(
            CapabilityCatalogIssueCodes.InvalidCandidate,
            Assert.Single(rejected.Issues).Code);

        CapabilityCatalogReloadResult retry = TerminalResult(await ReadUpdatesAsync(
            catalog.LoadAsync(TestContext.Current.CancellationToken)));
        Assert.True(retry.Succeeded);
        Assert.NotSame(seed.Snapshot, retry.Snapshot);
    }

    private static async Task<List<CanonicalCapabilityCatalogLoadUpdate>> ReadUpdatesAsync(
        IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> updates)
    {
        var collected = new List<CanonicalCapabilityCatalogLoadUpdate>();
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in updates.WithCancellation(
                           TestContext.Current.CancellationToken))
        {
            collected.Add(update);
        }

        return collected;
    }

    private static async Task ReadUpdatesAsync(
        IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> updates,
        List<CanonicalCapabilityCatalogLoadUpdate> collected)
    {
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in updates.WithCancellation(
                           TestContext.Current.CancellationToken))
        {
            collected.Add(update);
        }
    }

    private static CapabilityCatalogReloadResult TerminalResult(
        IReadOnlyList<CanonicalCapabilityCatalogLoadUpdate> updates)
    {
        CanonicalCapabilityCatalogLoadUpdate terminal = Assert.Single(
            updates,
            static update => update.Result is not null);
        Assert.Same(terminal, updates[^1]);
        return terminal.Result!;
    }

    private static List<double> ProgressValues(
        IEnumerable<CanonicalCapabilityCatalogLoadUpdate> updates)
    {
        return [.. updates.Where(static update => update.Progress.HasValue)
            .Select(static update => update.Progress!.Value)];
    }

    private sealed class QueuedCandidateSource(
        params CapabilityCatalogLoadResult[] results) :
        ICanonicalCapabilityCatalogSource
    {
        private readonly Queue<CapabilityCatalogLoadResult> _results = new(results);

        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _results.Dequeue();
        }
    }
}
