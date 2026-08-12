using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Protects focused Support Matrix wiring over the Application-owned catalog.</summary>
public sealed class CanonicalSupportMatrixHostTests
{
    /// <summary>Explicit hosts publish independently without process-wide serialization.</summary>
    [Fact]
    public void ExplicitHostsOwnIndependentCatalogPublications()
    {
        var first = new IsolatedBootstrapTestHost();
        var second = new IsolatedBootstrapTestHost();

        Assert.Equal(
            CanonicalSupportMatrixCatalogState.Loading,
            first.Services.CanonicalSupportMatrixQuery.Query().State);
        Assert.Equal(
            CanonicalSupportMatrixCatalogState.Loading,
            second.Services.CanonicalSupportMatrixQuery.Query().State);

        CapabilityCatalogReloadResult reload = first.Catalog.Reload(
            TestContext.Current.CancellationToken);

        Assert.True(reload.Succeeded);
        Assert.Equal(
            CanonicalSupportMatrixCatalogState.Current,
            first.Services.CanonicalSupportMatrixQuery.Query().State);
        Assert.Equal(
            CanonicalSupportMatrixCatalogState.Loading,
            second.Services.CanonicalSupportMatrixQuery.Query().State);
    }

    /// <summary>An in-flight worker load cannot block the UI reporting query.</summary>
    [Fact(Timeout = 30_000)]
    public async Task QueryReturnsLoadingWhileBackgroundWarmIsInFlight()
    {
        CapabilityCatalogLoadResult seed =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken);
        using var source = new BlockingProbeSource(seed.Candidate!);
        var catalog = new CanonicalCapabilityCatalog(source);
        var warm = Task.Run(
            () => catalog.Warm(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        try
        {
            await source.LoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            Task<CanonicalSupportMatrixQueryResult> queryTask = Task.Run(
                catalog.Query,
                TestContext.Current.CancellationToken);
            Task completed = await Task.WhenAny(
                queryTask,
                Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
            Assert.Same(queryTask, completed);
            Assert.Equal(
                CanonicalSupportMatrixCatalogState.Loading,
                (await queryTask).State);
        }
        finally
        {
            source.AllowLoad.Set();
#pragma warning disable xUnit1051 // Cleanup must observe the worker after test cancellation.
            await warm.WaitAsync(TimeSpan.FromSeconds(5));
#pragma warning restore xUnit1051
        }

        Assert.Equal(CanonicalSupportMatrixCatalogState.Current, catalog.Query().State);
    }

    /// <summary>Concurrent reloads cannot publish reporting state out of catalog order.</summary>
    [Fact]
    public async Task CatalogSerializesReloadAndReportingPublication()
    {
        CapabilityCatalogLoadResult seed =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken);
        var source = new ConcurrentProbeSource(seed.Candidate!);
        var catalog = new CanonicalCapabilityCatalog(source);

        Assert.Equal(CanonicalSupportMatrixCatalogState.Loading, catalog.Query().State);
        Assert.Equal(0, source.LoadCount);

        CapabilityCatalogReloadResult[] reloads = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(
                () => catalog.Reload(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken)));

        Assert.Equal(1, source.MaximumConcurrentLoads);
        int latestGeneration = reloads.Max(static reload =>
            int.Parse(
                reload.Snapshot!.CatalogVersion.Split('.')[^1],
                System.Globalization.CultureInfo.InvariantCulture));
        CapabilityCatalogReloadResult latest = catalog.LatestReload!;
        Assert.Equal(
            $"1.0.{latestGeneration}",
            latest.Snapshot!.CatalogVersion);
        Assert.Same(
            latest.Snapshot,
            catalog.TryGetCurrentSnapshot());
    }

    /// <summary>One explicitly constructed host query observes its own catalog publication.</summary>
    [Fact]
    public void FocusedQueryUsesTheSharedCanonicalCatalogPublication()
    {
        var host = CompositionHostServices.Create();
        ICanonicalSupportMatrixQuery query = host.CanonicalSupportMatrixQuery;
        CapabilityCatalogReloadResult reload =
            host.Catalog.Reload(
                TestContext.Current.CancellationToken);

        CanonicalSupportMatrixQueryResult result = query.Query();

        Assert.True(reload.Succeeded);
        Assert.Equal(CanonicalSupportMatrixCatalogState.Current, result.State);
        Assert.Equal(reload.Snapshot!.ResolutionToken, result.Matrix!.ResolutionToken);
        Assert.Equal(78, result.Matrix.Rows.Count);
        Assert.Equal(
            reload.Snapshot.Capabilities.Count + reload.Snapshot.DynamicRoutes.Count,
            result.Matrix.Rows.Count);
    }

    private sealed class ConcurrentProbeSource(
        CanonicalCapabilityCatalogCandidate seed) :
        ICanonicalCapabilityCatalogSource
    {
        private int _activeLoads;
        private int _generation;
        private int _maximumConcurrentLoads;

        internal int LoadCount => Volatile.Read(ref _generation);

        internal int MaximumConcurrentLoads => Volatile.Read(ref _maximumConcurrentLoads);

        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int active = Interlocked.Increment(ref _activeLoads);
            UpdateMaximum(active);
            try
            {
                Thread.Sleep(5);
                int generation = Interlocked.Increment(ref _generation);
                return CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        seed.CatalogId,
                        $"1.0.{generation}",
                        seed.SourceSha256,
                        seed.Definitions,
                        seed.DynamicDefinitions));
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeLoads);
            }
        }

        private void UpdateMaximum(int active)
        {
            int current = Volatile.Read(ref _maximumConcurrentLoads);
            while (active > current)
            {
                int observed = Interlocked.CompareExchange(
                    ref _maximumConcurrentLoads,
                    active,
                    current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }

    private sealed class BlockingProbeSource(
        CanonicalCapabilityCatalogCandidate candidate) :
        ICanonicalCapabilityCatalogSource,
        IDisposable
    {
        internal TaskCompletionSource LoadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal ManualResetEventSlim AllowLoad { get; } = new(false);

        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            _ = LoadStarted.TrySetResult();
            AllowLoad.Wait(cancellationToken);
            return CapabilityCatalogLoadResult.Success(candidate);
        }

        public void Dispose()
        {
            AllowLoad.Dispose();
        }
    }
}
