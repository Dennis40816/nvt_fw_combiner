using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <inheritdoc/>
public sealed class ReportHistoryPersistenceTests
{
    /// <summary>Async persistence atomically promotes complete snapshots and never publishes cancelled work.</summary>
    [Fact]
    public async Task AsyncSavePreservesLastCompleteSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-async");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        ReportHistorySnapshot original = CreateSnapshot("original.json");
        ReportHistorySnapshot cancelled = CreateSnapshot("cancelled.json");
        ReportHistorySnapshot latest = CreateSnapshot("latest.json");
        ReportHistoryFileStore.Save(historyPath, [original]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await ReportHistoryFileStore.SaveAsync(historyPath, [cancelled], cancellation.Token);

        Assert.Equal("original.json", Assert.Single(ReportHistoryFileStore.Load(historyPath)).SourceName);

        await ReportHistoryFileStore.SaveAsync(
            historyPath,
            [latest],
            TestContext.Current.CancellationToken);

        ReportHistorySnapshot loaded = Assert.Single(ReportHistoryFileStore.Load(historyPath));
        Assert.Equal("latest.json", loaded.SourceName);
        Assert.Equal(latest.ReportJson, loaded.ReportJson);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(historyPath)!, "*.tmp"));
    }

    /// <summary>Queued persistence serializes writes and drops a superseded snapshot before it starts.</summary>
    [Fact]
    public async Task CoordinatorKeepsLatestQueuedSnapshot()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> savedSources = [];
        LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>> coordinator =
            CreateCoordinator(async (snapshots, _) =>
        {
            string source = Assert.Single(snapshots).SourceName;
            lock (savedSources)
            {
                savedSources.Add(source);
            }

            if (string.Equals(source, "first.json", StringComparison.Ordinal))
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
            }
        });

        coordinator.Queue([CreateSnapshot("first.json")]);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Queue([CreateSnapshot("superseded.json")]);
        coordinator.Queue([CreateSnapshot("latest.json")]);
        releaseFirst.SetResult();

        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first.json", "latest.json"], savedSources);
    }

    /// <summary>Shutdown waits for the latest save and seals the coordinator against later writes.</summary>
    [Fact]
    public async Task CoordinatorCompletesLatestSaveBeforeShutdown()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-close");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        TaskCompletionSource saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>> coordinator =
            CreateCoordinator(async (snapshots, cancellationToken) =>
        {
            saveStarted.SetResult();
            await releaseSave.Task;
            await ReportHistoryFileStore.SaveAsync(historyPath, snapshots, cancellationToken);
        });
        coordinator.Queue([CreateSnapshot("latest-before-close.json")]);
        await saveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task completion = coordinator.CompleteAsync();

        Assert.False(completion.IsCompleted);
        releaseSave.SetResult();
        await completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            "latest-before-close.json",
            Assert.Single(ReportHistoryFileStore.Load(historyPath)).SourceName);
        _ = Assert.Throws<InvalidOperationException>(
            () => coordinator.Queue([CreateSnapshot("after-close.json")]));
    }

    /// <summary>An unexpected best-effort save fault is observed without poisoning later persistence.</summary>
    [Fact]
    public async Task CoordinatorRecoversAfterSaveFault()
    {
        List<string> attemptedSources = [];
        LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>> coordinator =
            CreateCoordinator((snapshots, _) =>
        {
            string source = Assert.Single(snapshots).SourceName;
            attemptedSources.Add(source);
            return string.Equals(source, "faulted.json", StringComparison.Ordinal)
                ? Task.FromException(new InvalidOperationException("synthetic persistence failure"))
                : Task.CompletedTask;
        });

        coordinator.Queue([CreateSnapshot("faulted.json")]);
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Queue([CreateSnapshot("recovered.json")]);

        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["faulted.json", "recovered.json"], attemptedSources);
        _ = Assert.IsType<InvalidOperationException>(coordinator.LastFailure);
    }

    private static ReportHistorySnapshot CreateSnapshot(string sourceName)
    {
        return new ReportHistorySnapshot(
            sourceName,
            ReportJsonSamples.Succeeded(runId: Path.GetFileNameWithoutExtension(sourceName)),
            string.Empty);
    }

    private static LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>> CreateCoordinator(
        Func<IReadOnlyList<ReportHistorySnapshot>, CancellationToken, Task> saveAsync)
    {
        return new LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>>(
            saveAsync,
            snapshots => [.. snapshots]);
    }
}
