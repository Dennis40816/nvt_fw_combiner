using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Locks deterministic fail-closed selection across Registry replicas.</summary>
public sealed class ReplicatedUpdateSourceRegistryTests
{
    /// <summary>The highest valid monotonic revision wins regardless of replica order.</summary>
    [Fact]
    public async Task HighestValidRevisionWins()
    {
        UpdateSourceRegistrySnapshot primary = Snapshot(7, 'a');
        UpdateSourceRegistrySnapshot backup = Snapshot(8, 'b');
        var registry = new ReplicatedUpdateSourceRegistry(
            [new StubRegistry(Success(primary)), new StubRegistry(Success(backup))]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Same(backup, result.Snapshot);
        Assert.Equal(UpdateSourceRegistryLoadIssue.None, result.Issue);
    }

    /// <summary>A newer primary wins by the same revision rule; ordering is only a tie-breaker.</summary>
    [Fact]
    public async Task NewerPrimaryWinsOverOlderBackup()
    {
        UpdateSourceRegistrySnapshot primary = Snapshot(8, 'b');
        var registry = new ReplicatedUpdateSourceRegistry(
            [new StubRegistry(Success(primary)), new StubRegistry(Success(Snapshot(7, 'a')))]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Same(primary, result.Snapshot);
        Assert.Collection(
            result.Replicas!,
            replica => Assert.True(replica.IsSelected),
            replica =>
            {
                Assert.False(replica.IsSelected);
                Assert.Equal(7, replica.RegistryRevision);
            });
    }

    /// <summary>An invalid replica cannot hide the other valid complete publication.</summary>
    [Fact]
    public async Task OneInvalidReplicaFallsBackToOtherValidReplica()
    {
        UpdateSourceRegistrySnapshot backup = Snapshot(4, 'c');
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new StubRegistry(new(null, UpdateSourceRegistryLoadIssue.InvalidManifest)),
                new StubRegistry(Success(backup)),
            ]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Same(backup, result.Snapshot);
        Assert.Equal(UpdateSourceRegistryLoadIssue.None, result.Issue);
    }

    /// <summary>A synchronously blocked mapped-drive probe cannot hide a valid backup forever.</summary>
    [Fact]
    public async Task TimedOutPrimaryAllowsValidBackup()
    {
        using var releasePrimary = new ManualResetEventSlim();
        UpdateSourceRegistrySnapshot backup = Snapshot(4, 'c');
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new SynchronouslyBlockingRegistry(releasePrimary),
                new StubRegistry(Success(backup)),
            ],
            TimeSpan.FromMilliseconds(100));

        try
        {
            UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
                TestContext.Current.CancellationToken);

            Assert.Same(backup, result.Snapshot);
            Assert.Equal(UpdateSourceRegistryLoadIssue.None, result.Issue);
            Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTimedOut, result.Replicas![0].Issue);
            Assert.True(result.Replicas[1].IsSelected);
        }
        finally
        {
            releasePrimary.Set();
        }
    }

    /// <summary>Repeated checks never accumulate physical reads behind one blocked replica.</summary>
    [Fact]
    public async Task RepeatedTimeoutsKeepOnePhysicalReadPerReplica()
    {
        using var releasePrimary = new ManualResetEventSlim();
        var blocked = new SynchronouslyBlockingRegistry(releasePrimary);
        UpdateSourceRegistrySnapshot backup = Snapshot(4, 'c');
        var registry = new ReplicatedUpdateSourceRegistry(
            [blocked, new StubRegistry(Success(backup))],
            TimeSpan.FromMilliseconds(100));

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
                    TestContext.Current.CancellationToken);
                Assert.Same(backup, result.Snapshot);
                Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTimedOut, result.Replicas![0].Issue);
            }

            Assert.Equal(1, blocked.LoadCount);
        }
        finally
        {
            releasePrimary.Set();
        }
    }

    /// <summary>A single explicit locator receives the same bounded single-flight protection.</summary>
    [Fact]
    public async Task SingleReplicaRepeatedTimeoutsKeepOnePhysicalRead()
    {
        using var release = new ManualResetEventSlim();
        var blocked = new SynchronouslyBlockingRegistry(release);
        var registry = new ReplicatedUpdateSourceRegistry(
            [blocked],
            TimeSpan.FromMilliseconds(100));

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
                    TestContext.Current.CancellationToken);
                Assert.Null(result.Snapshot);
                Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTimedOut, result.Issue);
                Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTimedOut, result.Replicas![0].Issue);
            }

            Assert.Equal(1, blocked.LoadCount);
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>Concurrent checks share the same blocked physical read instead of multiplying it.</summary>
    [Fact]
    public async Task ConcurrentTimeoutsKeepOnePhysicalReadPerReplica()
    {
        using var releasePrimary = new ManualResetEventSlim();
        var blocked = new SynchronouslyBlockingRegistry(releasePrimary);
        UpdateSourceRegistrySnapshot backup = Snapshot(4, 'c');
        var available = new SignalingRegistry(Success(backup));
        var time = new ManualTimeProvider();
        var registry = new ReplicatedUpdateSourceRegistry(
            [blocked, available],
            TimeSpan.FromMilliseconds(100),
            time);

        try
        {
            Task<UpdateSourceRegistryLoadResult>[] pending =
            [..
                Enumerable.Range(0, 8).Select(
                    _ => registry.LoadAsync(TestContext.Current.CancellationToken).AsTask())
            ];
            await blocked.FirstLoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            await available.FirstLoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromMilliseconds(100));

            UpdateSourceRegistryLoadResult[] results = await Task.WhenAll(pending);

            Assert.All(results, result =>
            {
                Assert.Same(backup, result.Snapshot);
                Assert.Equal(
                    UpdateSourceRegistryLoadIssue.RegistryTimedOut,
                    result.Replicas![0].Issue);
                Assert.True(result.Replicas[1].IsSelected);
            });
            Assert.Equal(1, blocked.LoadCount);
            Assert.Equal(1, available.LoadCount);
        }
        finally
        {
            releasePrimary.Set();
        }
    }

    /// <summary>A late completed read is retired so one later check can recover without restart.</summary>
    [Fact]
    public async Task CompletedAbandonedReadAllowsOneFreshPhysicalRead()
    {
        using var releasePrimary = new ManualResetEventSlim();
        var blocked = new SynchronouslyBlockingRegistry(releasePrimary);
        UpdateSourceRegistrySnapshot backup = Snapshot(4, 'c');
        var registry = new ReplicatedUpdateSourceRegistry(
            [blocked, new StubRegistry(Success(backup))],
            TimeSpan.FromMilliseconds(100));

        UpdateSourceRegistryLoadResult timedOut = await registry.LoadAsync(
            TestContext.Current.CancellationToken);
        releasePrimary.Set();
        await blocked.FirstLoadCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
        UpdateSourceRegistryLoadResult? recovered = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            UpdateSourceRegistryLoadResult candidate = await registry.LoadAsync(
                TestContext.Current.CancellationToken);
            if (candidate.Replicas![0].Issue != UpdateSourceRegistryLoadIssue.RegistryTimedOut)
            {
                recovered = candidate;
                break;
            }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTimedOut, timedOut.Replicas![0].Issue);
        Assert.NotNull(recovered);
        Assert.Same(backup, recovered.Snapshot);
        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryUnavailable, recovered.Replicas![0].Issue);
        Assert.Equal(2, blocked.LoadCount);
    }

    /// <summary>When no replica is valid, the primary typed failure remains deterministic.</summary>
    [Fact]
    public async Task NoValidReplicaReturnsPrimaryFailure()
    {
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new StubRegistry(new(null, UpdateSourceRegistryLoadIssue.RegistryMissing)),
                new StubRegistry(new(null, UpdateSourceRegistryLoadIssue.InvalidManifest)),
            ]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(result.Snapshot);
        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryMissing, result.Issue);
        Assert.Equal(
            [UpdateSourceRegistryLoadIssue.RegistryMissing, UpdateSourceRegistryLoadIssue.InvalidManifest],
            result.Replicas!.Select(static replica => replica.Issue));
        Assert.All(result.Replicas!, static replica => Assert.False(replica.IsSelected));
    }

    /// <summary>Caller cancellation wins over the replica deadline and remains an exception.</summary>
    [Fact]
    public async Task CallerCancellationPropagatesWhileReplicaIsSynchronouslyBlocked()
    {
        using var releasePrimary = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var registry = new ReplicatedUpdateSourceRegistry(
            [new SynchronouslyBlockingRegistry(releasePrimary)],
            TimeSpan.FromSeconds(5));

        try
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => registry.LoadAsync(cancellation.Token).AsTask());
        }
        finally
        {
            releasePrimary.Set();
        }
    }

    /// <summary>Identical replicas retain primary precedence without synthesizing another snapshot.</summary>
    [Fact]
    public async Task SameRevisionAndDigestSelectPrimary()
    {
        UpdateSourceRegistrySnapshot primary = Snapshot(9, 'd');
        UpdateSourceRegistrySnapshot backup = Snapshot(9, 'd');
        var registry = new ReplicatedUpdateSourceRegistry(
            [new StubRegistry(Success(primary)), new StubRegistry(Success(backup))]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Same(primary, result.Snapshot);
    }

    /// <summary>Equal revisions with different exact bytes are an authority conflict.</summary>
    [Fact]
    public async Task SameRevisionDifferentDigestFailsClosed()
    {
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new StubRegistry(Success(Snapshot(9, 'd'))),
                new StubRegistry(Success(Snapshot(9, 'e'))),
            ]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(result.Snapshot);
        Assert.Equal(UpdateSourceRegistryLoadIssue.ReplicaConflict, result.Issue);
    }

    /// <summary>The runtime model rejects a Registry from another logical authority.</summary>
    [Fact]
    public void ForeignRegistryAuthorityCannotEnterTheRuntimeModel()
    {
        _ = Assert.Throws<ArgumentException>(() => new UpdateSourceRegistrySnapshot(
            "foreign-registry",
            10,
            new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            new UpdateCatalogPublicationAssertion("1.0.1", 1, new string('e', 64)),
            new string('e', 64),
            [new UpdateSourceRegistryEntry(
                Path.GetFullPath(Path.Combine("registry-tests", "10.json")),
                UpdateSourceRegistryEntryStatus.Latest)]));
    }

    private static UpdateSourceRegistrySnapshot Snapshot(long revision, char digestCharacter)
    {
        string catalogPath = Path.GetFullPath(Path.Combine("registry-tests", $"{revision}.json"));
        return new UpdateSourceRegistrySnapshot(
            UpdateSourceRegistrySnapshot.ProductionRegistryId,
            revision,
            new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            new UpdateCatalogPublicationAssertion("1.0.1", 1, new string(digestCharacter, 64)),
            new string(digestCharacter, 64),
            [new UpdateSourceRegistryEntry(catalogPath, UpdateSourceRegistryEntryStatus.Latest)]);
    }

    private static UpdateSourceRegistryLoadResult Success(UpdateSourceRegistrySnapshot snapshot)
    {
        return new(snapshot, UpdateSourceRegistryLoadIssue.None);
    }

    private sealed class StubRegistry(UpdateSourceRegistryLoadResult result) : IUpdateSourceRegistry
    {
        public ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SignalingRegistry(UpdateSourceRegistryLoadResult result) : IUpdateSourceRegistry
    {
        private int _loadCount;

        internal int LoadCount => Volatile.Read(ref _loadCount);

        internal TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Interlocked.Increment(ref _loadCount);
            _ = FirstLoadStarted.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SynchronouslyBlockingRegistry(ManualResetEventSlim release)
        : IUpdateSourceRegistry
    {
        private int _loadCount;

        internal int LoadCount => Volatile.Read(ref _loadCount);

        internal TaskCompletionSource FirstLoadCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _loadCount);
            _ = FirstLoadStarted.TrySetResult();
            release.Wait(CancellationToken.None);
            _ = FirstLoadCompleted.TrySetResult();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new UpdateSourceRegistryLoadResult(
                null,
                UpdateSourceRegistryLoadIssue.RegistryUnavailable));
        }
    }
}
