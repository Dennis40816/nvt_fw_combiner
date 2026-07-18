using System.Text;
using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies report history can reopen earlier reports without adding a new run.</summary>
    [Fact]
    public async Task ReportHistoryTracksSessionReportsAndReopensEarlierEntry()
    {
        string previewJson = ReportJsonSamples.Succeeded(
            runId: "preview-run",
            outputSize: 16,
            outputSha256: "abcdef0123456789abcdef");
        string buildJson = ReportJsonSamples.CtrlRamCommandSucceeded();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(previewJson, "preview-report.json");
        viewModel.LoadReportJson(buildJson, "build-report.json");

        Assert.True(viewModel.HasReportHistory);
        Assert.Equal(2, viewModel.ReportHistoryCount);
        Assert.Equal("2 reports in history", viewModel.ReportHistorySummary);
        Assert.Equal("nt51927-ctrlram-replace (NT51927)", viewModel.ReportHistoryEntries[0].Title);
        Assert.Equal("1 command", viewModel.ReportHistoryEntries[0].CommandSummary);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", viewModel.ReportHistoryEntries[1].Title);
        Assert.Equal("abcdef0123456789...", viewModel.ReportHistoryEntries[1].OutputHash);

        viewModel.ShowReportHistoryCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.True(viewModel.IsReportHistoryViewOpen);
        Assert.False(viewModel.IsReportReviewViewOpen);

        await viewModel.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(viewModel.ReportHistoryEntries[1]);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.IsReportReviewViewOpen);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal("preview-report.json", viewModel.LoadedReport.SourceName);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", viewModel.LoadedReport.Title);
        Assert.Equal(previewJson, viewModel.LoadedReportJson);
        Assert.Equal(2, viewModel.ReportHistoryCount);

        viewModel.ShowReportHistoryCommand.Execute(null);
        viewModel.RemoveReportHistoryEntryCommand.Execute(viewModel.ReportHistoryEntries[0]);

        Assert.True(viewModel.HasReportHistory);
        Assert.Equal(1, viewModel.ReportHistoryCount);
        Assert.Equal("1 report in history", viewModel.ReportHistorySummary);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", Assert.Single(viewModel.ReportHistoryEntries).Title);

        viewModel.ClearReportHistoryCommand.Execute(null);

        Assert.False(viewModel.HasReportHistory);
        Assert.True(viewModel.IsReportHistoryEmpty);
        Assert.Equal(0, viewModel.ReportHistoryCount);
        Assert.Equal("No reports in history", viewModel.ReportHistorySummary);
        Assert.False(viewModel.CanOpenReportHistory);
        Assert.False(viewModel.ShowReportHistoryCommand.CanExecute(null));
        Assert.False(viewModel.ClearReportHistoryCommand.CanExecute(null));
        Assert.True(viewModel.HasLoadedReport);
        Assert.Equal("preview-report.json", viewModel.LoadedReport.SourceName);
    }

    /// <summary>Verifies local report history reports oversized storage and can be cleared in one action.</summary>
    [Fact]
    public void ReportHistoryFlagsOversizedStorageForOneClickCleanup()
    {
        string json = ReportJsonSamples.Succeeded();
        string paddedJson = json.Insert(json.LastIndexOf('}'), $",\"Padding\":\"{new string('A', 1024 * 1024)}\"");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(paddedJson, "large-report.json");
        viewModel.ShowReportHistoryCommand.Execute(null);

        Assert.True(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.HasReportHistoryStorageWarning);
        Assert.Contains("MB", viewModel.ReportHistoryStorageSummary, StringComparison.Ordinal);
        Assert.Contains("Clear history", viewModel.ReportHistoryStorageWarning, StringComparison.Ordinal);

        viewModel.ClearReportHistoryCommand.Execute(null);

        Assert.False(viewModel.HasReportHistoryStorageWarning);
        Assert.Equal("0 B stored locally", viewModel.ReportHistoryStorageSummary);
        Assert.Empty(viewModel.ExportReportHistory());
    }

    /// <summary>Verifies persisted report history snapshots restore report metadata and artifact path context.</summary>
    [Fact]
    public void ReportHistorySnapshotsRestoreAcrossViewModels()
    {
        string json = ReportJsonSamples.Succeeded(
            profileId: "nt51927-ctrlram-replace",
            modeId: "ctrlram-replace",
            experienceId: "ctrlram-replace",
            compositionKind: "Replace",
            runId: "persisted-build-run",
            startedAtUtc: "2026-07-01T00:05:00Z",
            outputFileName: "build.bin",
            outputSize: 32,
            committed: true,
            outputSha256: "0123456789abcdef012345");
        ReportHistorySnapshot snapshot = new(
            "build-report.json",
            json,
            "C:/nfc/output/build.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportHistory([snapshot]);

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal("Open report", viewModel.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.ReportActionStatus);
        Assert.Equal("build-report.json", viewModel.LoadedReport.SourceName);
        Assert.True(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal("C:/nfc/output/build.bin", viewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal("C:/nfc/output/build.bin", Assert.Single(viewModel.ReportHistoryEntries).ArtifactPath);

        IReadOnlyList<ReportHistorySnapshot> exported = viewModel.ExportReportHistory();
        ReportHistorySnapshot exportedSnapshot = Assert.Single(exported);
        Assert.Equal("build-report.json", exportedSnapshot.SourceName);
        Assert.Equal(json, exportedSnapshot.ReportJson);
        Assert.Equal("C:/nfc/output/build.bin", exportedSnapshot.OutputArtifactPath);
        Assert.Equal("nt51927-ctrlram-replace (NT51927)", exportedSnapshot.Metadata.Title);
        Assert.Equal("Succeeded", exportedSnapshot.Metadata.Status);
        Assert.Equal("Replace / ctrlram-replace / NT51927", exportedSnapshot.Metadata.Context);
        Assert.Equal("0123456789abcdef...", exportedSnapshot.Metadata.OutputHash);
        Assert.Equal("persisted-build-run", exportedSnapshot.Metadata.RunId);
        Assert.Equal("0 inputs / 0 steps / 0 mutations", exportedSnapshot.Metadata.EvidenceSummary);

        MainWindowViewModel restoredViewModel = ShellViewModelFactory.Create();
        restoredViewModel.LoadReportHistory(exported);

        Assert.Equal("nt51927-ctrlram-replace (NT51927)", restoredViewModel.LoadedReport.Title);
        Assert.Equal("C:/nfc/output/build.bin", restoredViewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal(1, restoredViewModel.ReportHistoryCount);
    }

    /// <summary>Verifies local report history persistence round-trips and fails closed for bad JSON.</summary>
    [Fact]
    public void ReportHistoryFileStoreRoundTripsSnapshots()
    {
        string json = ReportJsonSamples.Succeeded(
            runId: "persisted-preview-run",
            outputSize: 16,
            outputSha256: "abcdef0123456789abcdef");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        var metadata = new ReportHistoryMetadataSnapshot(
            "nt51927-standard-merge-gen-flash (NT51927)",
            "Succeeded",
            "Merge / standard-merge / NT51927",
            "preview.bin / 16 bytes",
            "abcdef0123456789...",
            "No external command",
            "No issue",
            "0 inputs / 0 steps / 0 mutations",
            "persisted-preview-run",
            "2026-07-01T00:00:00Z",
            "NT51927",
            "standard-merge",
            "standard-merge",
            "Merge");
        ReportHistorySnapshot snapshot = new(
            "preview-report.json",
            json,
            "C:/nfc/output/preview.bin",
            metadata);

        ReportHistoryFileStore.Save(historyPath, [snapshot]);

        IReadOnlyList<ReportHistorySnapshot> loaded = ReportHistoryFileStore.Load(historyPath);
        ReportHistorySnapshot loadedSnapshot = Assert.Single(loaded);
        Assert.Equal("preview-report.json", loadedSnapshot.SourceName);
        Assert.Equal(json, loadedSnapshot.ReportJson);
        Assert.Equal("C:/nfc/output/preview.bin", loadedSnapshot.OutputArtifactPath);
        Assert.Equal(metadata, loadedSnapshot.Metadata);

        string legacyJson = $$"""
            {
              "SchemaVersion": 1,
              "Entries": [
                {
                  "SourceName": "legacy-report.json",
                  "ReportJson": {{JsonSerializer.Serialize(json)}},
                  "OutputArtifactPath": ""
                }
              ]
            }
            """;
        File.WriteAllText(historyPath, legacyJson);

        ReportHistorySnapshot legacySnapshot = Assert.Single(ReportHistoryFileStore.Load(historyPath));
        Assert.Equal("legacy-report.json", legacySnapshot.SourceName);
        Assert.Equal(ReportHistoryMetadataSnapshot.Empty, legacySnapshot.Metadata);

        File.WriteAllText(historyPath, "{not valid json");

        Assert.Empty(ReportHistoryFileStore.Load(historyPath));
    }

    /// <summary>Async persistence atomically promotes complete snapshots and never publishes cancelled work.</summary>
    [Fact]
    public async Task ReportHistoryFileStoreAsyncSavePreservesLastCompleteSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-async");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        ReportHistorySnapshot original = new(
            "original.json",
            ReportJsonSamples.Succeeded(runId: "original-run"),
            string.Empty);
        ReportHistorySnapshot cancelled = new(
            "cancelled.json",
            ReportJsonSamples.Succeeded(runId: "cancelled-run"),
            string.Empty);
        ReportHistorySnapshot latest = new(
            "latest.json",
            ReportJsonSamples.Succeeded(runId: "latest-run"),
            string.Empty);
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
    public async Task ReportHistoryPersistenceCoordinatorKeepsLatestQueuedSnapshot()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> savedSources = [];
        var coordinator = new ReportHistoryPersistenceCoordinator(async (snapshots, _) =>
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

        coordinator.Queue([CreatePersistenceSnapshot("first.json")]);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Queue([CreatePersistenceSnapshot("superseded.json")]);
        coordinator.Queue([CreatePersistenceSnapshot("latest.json")]);
        releaseFirst.SetResult();

        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first.json", "latest.json"], savedSources);
    }

    /// <summary>Shutdown waits for the latest save and seals the coordinator against later writes.</summary>
    [Fact]
    public async Task ReportHistoryPersistenceCoordinatorCompletesLatestSaveBeforeShutdown()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-close");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        TaskCompletionSource saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ReportHistoryPersistenceCoordinator(async (snapshots, cancellationToken) =>
        {
            saveStarted.SetResult();
            await releaseSave.Task;
            await ReportHistoryFileStore.SaveAsync(historyPath, snapshots, cancellationToken);
        });
        coordinator.Queue([CreatePersistenceSnapshot("latest-before-close.json")]);
        await saveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task completion = coordinator.CompleteAsync();

        Assert.False(completion.IsCompleted);
        releaseSave.SetResult();
        await completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            "latest-before-close.json",
            Assert.Single(ReportHistoryFileStore.Load(historyPath)).SourceName);
        _ = Assert.Throws<InvalidOperationException>(
            () => coordinator.Queue([CreatePersistenceSnapshot("after-close.json")]));
    }

    /// <summary>An unexpected best-effort save fault is observed without poisoning later persistence.</summary>
    [Fact]
    public async Task ReportHistoryPersistenceCoordinatorRecoversAfterSaveFault()
    {
        List<string> attemptedSources = [];
        var coordinator = new ReportHistoryPersistenceCoordinator((snapshots, _) =>
        {
            string source = Assert.Single(snapshots).SourceName;
            attemptedSources.Add(source);
            return string.Equals(source, "faulted.json", StringComparison.Ordinal)
                ? Task.FromException(new InvalidOperationException("synthetic persistence failure"))
                : Task.CompletedTask;
        });

        coordinator.Queue([CreatePersistenceSnapshot("faulted.json")]);
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Queue([CreatePersistenceSnapshot("recovered.json")]);

        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["faulted.json", "recovered.json"], attemptedSources);
        _ = Assert.IsType<InvalidOperationException>(coordinator.LastFailure);
    }

    private static ReportHistorySnapshot CreatePersistenceSnapshot(string sourceName)
    {
        return new ReportHistorySnapshot(
            sourceName,
            ReportJsonSamples.Succeeded(runId: Path.GetFileNameWithoutExtension(sourceName)),
            string.Empty);
    }

    /// <summary>Metadata-backed history stays compact and materializes only the entry opened for review.</summary>
    [Fact]
    public async Task ReportHistoryDefersOlderReviewMaterialization()
    {
        string latestJson = ReportJsonSamples.Succeeded(runId: "latest-run");
        string deferredJson = ReportJsonSamples.Succeeded(runId: "deferred-run");
        ReportHistoryMetadataSnapshot latestMetadata = ReportHistoryMetadataSnapshot.Empty with
        {
            Title = "Latest report",
            Status = "Succeeded",
            RunId = "latest-run",
        };
        ReportHistoryMetadataSnapshot deferredMetadata = ReportHistoryMetadataSnapshot.Empty with
        {
            Title = "Deferred report",
            Status = "Stored",
            RunId = "deferred-run",
        };
        ReportHistorySnapshot latest = new("latest.json", latestJson, string.Empty, latestMetadata);
        ReportHistorySnapshot deferred = new("deferred.json", deferredJson, "C:/output/deferred.bin", deferredMetadata);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportHistory([latest, deferred]);

        Assert.Equal(2, viewModel.ReportHistoryCount);
        Assert.Equal("latest-run", viewModel.LoadedReport.RunId);
        ReportHistoryEntryViewModel deferredEntry = viewModel.ReportHistoryEntries[1];
        Assert.Equal("Deferred report", deferredEntry.Title);
        Assert.Equal(
            Encoding.UTF8.GetByteCount(deferredJson) + Encoding.UTF8.GetByteCount(deferred.OutputArtifactPath),
            deferredEntry.StoredByteCount);
        Assert.Same(deferred, deferredEntry.ToSnapshot());

        await viewModel.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(deferredEntry);

        Assert.Equal(deferredJson, viewModel.LoadedReportJson);
        Assert.Equal("deferred-run", viewModel.LoadedReport.RunId);
        Assert.NotEqual(deferredEntry.Title, viewModel.LoadedReport.Title);
        Assert.Equal(2, viewModel.ReportHistoryCount);
    }

    /// <summary>A metadata-backed invalid latest report becomes a readable error instead of breaking restore.</summary>
    [Fact]
    public void ReportHistoryInvalidLatestShapeDegradesToError()
    {
        ReportHistoryMetadataSnapshot metadata = ReportHistoryMetadataSnapshot.Empty with
        {
            Title = "Invalid latest report",
            Status = "Stored",
        };
        ReportHistorySnapshot invalid = new("invalid-latest.json", "[]", string.Empty, metadata);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportHistory([invalid]);

        Assert.Equal(1, viewModel.ReportHistoryCount);
        Assert.Equal("Invalid JSON", viewModel.LoadedReport.Status);
        Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("[]", viewModel.LoadedReportJson);
    }

    /// <summary>A metadata-backed entry with malformed raw JSON is excluded during restore.</summary>
    [Fact]
    public void ReportHistoryMalformedMetadataBackedJsonIsExcluded()
    {
        ReportHistoryMetadataSnapshot metadata = ReportHistoryMetadataSnapshot.Empty with
        {
            Title = "Malformed report",
            Status = "Stored",
        };
        ReportHistorySnapshot malformed = new("malformed.json", "{not json", string.Empty, metadata);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportHistory([malformed]);

        Assert.Empty(viewModel.ReportHistoryEntries);
        Assert.False(viewModel.HasLoadedReport);
        Assert.Equal(string.Empty, viewModel.LoadedReportJson);
    }

    /// <summary>A metadata-backed invalid older report degrades safely only when the user opens it.</summary>
    [Fact]
    public async Task ReportHistoryInvalidDeferredShapeDegradesOnOpen()
    {
        string latestJson = ReportJsonSamples.Succeeded(runId: "latest-safe-run");
        ReportHistoryMetadataSnapshot latestMetadata = ReportHistoryMetadataSnapshot.Empty with
        {
            Title = "Latest safe report",
            Status = "Succeeded",
        };
        ReportHistoryMetadataSnapshot invalidMetadata = ReportHistoryMetadataSnapshot.Empty with
        {
            Title = "Invalid deferred report",
            Status = "Stored",
        };
        ReportHistorySnapshot latest = new("latest-safe.json", latestJson, string.Empty, latestMetadata);
        ReportHistorySnapshot invalid = new(
            "invalid-deferred.json",
            "{\"Operations\":[0]}",
            string.Empty,
            invalidMetadata);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportHistory([latest, invalid]);

        Assert.Equal("latest-safe-run", viewModel.LoadedReport.RunId);
        ReportHistoryEntryViewModel invalidEntry = viewModel.ReportHistoryEntries[1];

        await viewModel.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(invalidEntry);

        Assert.Equal("Invalid JSON", viewModel.LoadedReport.Status);
        Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal(invalid.ReportJson, viewModel.LoadedReportJson);
        Assert.Equal(2, viewModel.ReportHistoryCount);
    }
}
