using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Protects the focused Support Matrix host wiring during Workbench retirement.</summary>
[Collection(CanonicalCapabilityCatalogPublicationGroup.Name)]
public sealed class CanonicalSupportMatrixHostTests
{
    /// <summary>Shared publication reload coverage cannot run beside capability-bound executions.</summary>
    [Fact]
    public void HostTestsUseCanonicalPublicationSerializationCollection()
    {
        object attribute = Assert.Single(
            typeof(CanonicalSupportMatrixHostTests).GetCustomAttributes(
                typeof(CollectionAttribute),
                inherit: false));
        CollectionAttribute collection = Assert.IsType<CollectionAttribute>(attribute);

        Assert.Equal(CanonicalCapabilityCatalogPublicationGroup.Name, collection.Name);
    }

    /// <summary>An in-flight worker load cannot block the UI reporting query.</summary>
    [Fact]
    public async Task QueryReturnsLoadingWhileBackgroundWarmIsInFlight()
    {
        CapabilityCatalogLoadResult seed =
            new CanonicalCapabilityCatalogMigrationSource().Load(
                TestContext.Current.CancellationToken);
        using var source = new BlockingProbeSource(seed.Candidate!);
        var host = new CanonicalCapabilityCatalogHost(source);
        var query = new CanonicalSupportMatrixQuery(() => host.LatestReload);
        var warm = Task.Run(
            () => host.Warm(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.True(source.LoadStarted.Wait(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));
        try
        {
            Task<CanonicalSupportMatrixQueryResult> queryTask = Task.Run(
                query.Query,
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
        }

        await warm;
        Assert.Equal(CanonicalSupportMatrixCatalogState.Current, query.Query().State);
    }

    /// <summary>Concurrent reloads cannot publish reporting state out of catalog order.</summary>
    [Fact]
    public async Task HostSerializesReloadAndReportingPublication()
    {
        CapabilityCatalogLoadResult seed =
            new CanonicalCapabilityCatalogMigrationSource().Load(
                TestContext.Current.CancellationToken);
        var source = new ConcurrentProbeSource(seed.Candidate!);
        var host = new CanonicalCapabilityCatalogHost(source);
        var query = new CanonicalSupportMatrixQuery(() => host.LatestReload);

        Assert.Equal(CanonicalSupportMatrixCatalogState.Loading, query.Query().State);
        Assert.Equal(0, source.LoadCount);

        CapabilityCatalogReloadResult[] reloads = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(
                () => host.Reload(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken)));

        Assert.Equal(1, source.MaximumConcurrentLoads);
        int latestGeneration = reloads.Max(static reload =>
            int.Parse(
                reload.Snapshot!.CatalogVersion.Split('.')[^1],
                System.Globalization.CultureInfo.InvariantCulture));
        CapabilityCatalogReloadResult latest = host.LatestReload!;
        Assert.Equal(
            $"1.0.{latestGeneration}",
            latest.Snapshot!.CatalogVersion);
        Assert.Same(
            latest.Snapshot,
            host.Read(static catalog => catalog.CurrentSnapshot));
    }

    /// <summary>The focused query and compatibility facade observe one catalog publication.</summary>
    [Fact]
    public void FocusedQueryUsesTheSharedCanonicalCatalogPublication()
    {
        ICanonicalSupportMatrixQuery query =
            WorkbenchHostServices.CreateCanonicalSupportMatrixQuery();
        CapabilityCatalogReloadResult reload =
            WorkbenchCompositionService.ReloadCanonicalCapabilityCatalog(
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
        internal ManualResetEventSlim LoadStarted { get; } = new(false);

        internal ManualResetEventSlim AllowLoad { get; } = new(false);

        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            LoadStarted.Set();
            AllowLoad.Wait(cancellationToken);
            return CapabilityCatalogLoadResult.Success(candidate);
        }

        public void Dispose()
        {
            LoadStarted.Dispose();
            AllowLoad.Dispose();
        }
    }
}
