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

        viewModel.Reports.LoadReportJson(previewJson, "preview-report.json");
        viewModel.Reports.LoadReportJson(buildJson, "build-report.json");

        Assert.True(viewModel.Reports.HasReportHistory);
        Assert.Equal(2, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("2 reports in history", viewModel.Reports.ReportHistorySummary);
        Assert.Equal("nt51927-ctrlram-replace (NT51927)", viewModel.Reports.ReportHistoryEntries[0].Title);
        Assert.Equal("1 command", viewModel.Reports.ReportHistoryEntries[0].CommandSummary);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", viewModel.Reports.ReportHistoryEntries[1].Title);
        Assert.Equal("abcdef0123456789...", viewModel.Reports.ReportHistoryEntries[1].OutputHash);

        viewModel.Reports.ShowReportHistoryCommand.Execute(null);

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.True(viewModel.Reports.IsReportHistoryViewOpen);
        Assert.False(viewModel.Reports.IsReportReviewViewOpen);

        await viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(viewModel.Reports.ReportHistoryEntries[1]);

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.False(viewModel.Reports.IsReportHistoryViewOpen);
        Assert.True(viewModel.Reports.IsReportReviewViewOpen);
        Assert.False(viewModel.Reports.HasReportToast);
        Assert.Equal("preview-report.json", viewModel.Reports.LoadedReport.SourceName);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", viewModel.Reports.LoadedReport.Title);
        Assert.Equal(previewJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal(2, viewModel.Reports.ReportHistoryCount);

        viewModel.Reports.ShowReportHistoryCommand.Execute(null);
        viewModel.Reports.RemoveReportHistoryEntryCommand.Execute(viewModel.Reports.ReportHistoryEntries[0]);

        Assert.True(viewModel.Reports.HasReportHistory);
        Assert.Equal(1, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("1 report in history", viewModel.Reports.ReportHistorySummary);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", Assert.Single(viewModel.Reports.ReportHistoryEntries).Title);

        viewModel.Reports.ClearReportHistoryCommand.Execute(null);

        Assert.False(viewModel.Reports.HasReportHistory);
        Assert.True(viewModel.Reports.IsReportHistoryEmpty);
        Assert.Equal(0, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("No reports in history", viewModel.Reports.ReportHistorySummary);
        Assert.False(viewModel.Reports.CanOpenReportHistory);
        Assert.False(viewModel.Reports.ShowReportHistoryCommand.CanExecute(null));
        Assert.False(viewModel.Reports.ClearReportHistoryCommand.CanExecute(null));
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.Equal("preview-report.json", viewModel.Reports.LoadedReport.SourceName);
    }

    /// <summary>A blocked build is recorded as having no artifact, not as a zero-byte output file.</summary>
    [Fact]
    public void ReportHistoryDistinguishesMissingOutputFromZeroByteArtifact()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.Reports.LoadReportJson(ReportJsonSamples.CtrlRamCommandIssue(), "blocked-report.json");

        ReportHistoryEntryViewModel entry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("No output generated", entry.Output);
        Assert.Equal("No output hash", entry.OutputHash);
    }

    /// <summary>Verifies local report history reports oversized storage and can be cleared in one action.</summary>
    [Fact]
    public void ReportHistoryFlagsOversizedStorageForOneClickCleanup()
    {
        string json = ReportJsonSamples.Succeeded();
        string paddedJson = json.Insert(json.LastIndexOf('}'), $",\"Padding\":\"{new string('A', 1024 * 1024)}\"");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.Reports.LoadReportJson(paddedJson, "large-report.json");
        viewModel.Reports.ShowReportHistoryCommand.Execute(null);

        Assert.True(viewModel.Reports.IsReportHistoryViewOpen);
        Assert.True(viewModel.Reports.HasReportHistoryStorageWarning);
        Assert.Contains("MB", viewModel.Reports.ReportHistoryStorageSummary, StringComparison.Ordinal);
        Assert.Contains("Clear history", viewModel.Reports.ReportHistoryStorageWarning, StringComparison.Ordinal);

        viewModel.Reports.ClearReportHistoryCommand.Execute(null);

        Assert.False(viewModel.Reports.HasReportHistoryStorageWarning);
        Assert.Equal("0 B stored locally", viewModel.Reports.ReportHistoryStorageSummary);
        Assert.Empty(viewModel.Reports.ExportReportHistory());
    }

    /// <summary>Large local reports keep the newest review without allowing older history to exceed its byte budget.</summary>
    [Fact]
    public void ReportHistoryKeepsNewestEntryWithinStorageBudget()
    {
        int paddingLength = checked((int)(ReportPresentationViewModel.MaximumReportHistoryStorageBytes / 2) + 1024);
        string firstBase = ReportJsonSamples.Succeeded(runId: "first-large");
        string secondBase = ReportJsonSamples.Succeeded(runId: "second-large");
        string first = firstBase.Insert(
            firstBase.LastIndexOf('}'),
            $",\"Padding\":\"{new string('A', paddingLength)}\"");
        string second = secondBase.Insert(
            secondBase.LastIndexOf('}'),
            $",\"Padding\":\"{new string('B', paddingLength)}\"");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.Reports.LoadReportJson(first, "first-large.json");
        viewModel.Reports.LoadReportJson(second, "second-large.json");

        ReportHistoryEntryViewModel entry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("second-large.json", entry.SourceName);
        Assert.Equal(second, entry.ReportJson);
    }

    /// <summary>Successful JSON projection supplies the history size without a second full JSON scan.</summary>
    [Fact]
    public void ReportHistoryReusesProjectedUtf8ByteCount()
    {
        string json = ReportJsonSamples.Succeeded(runId: "多位元組-report");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.Reports.LoadReportJson(json, "report.json");

        long expected = Encoding.UTF8.GetByteCount(json);
        Assert.Equal(expected, viewModel.Reports.LoadedReport.ReportJsonUtf8ByteCount);
        Assert.Equal(expected, Assert.Single(viewModel.Reports.ReportHistoryEntries).StoredByteCount);
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

        viewModel.Reports.LoadReportHistory([snapshot]);

        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.CanOpenReport);
        Assert.False(viewModel.Reports.HasReportToast);
        Assert.Equal("Open report", viewModel.Reports.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.Reports.ReportActionStatus);
        Assert.Equal("build-report.json", viewModel.Reports.LoadedReport.SourceName);
        Assert.True(viewModel.Reports.LoadedReport.HasOutputArtifactPath);
        Assert.Equal("C:/nfc/output/build.bin", viewModel.Reports.LoadedReport.OutputArtifactPath);
        Assert.Equal("C:/nfc/output/build.bin", Assert.Single(viewModel.Reports.ReportHistoryEntries).ArtifactPath);

        IReadOnlyList<ReportHistorySnapshot> exported = viewModel.Reports.ExportReportHistory();
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
        restoredViewModel.Reports.LoadReportHistory(exported);

        Assert.Equal("nt51927-ctrlram-replace (NT51927)", restoredViewModel.Reports.LoadedReport.Title);
        Assert.Equal("C:/nfc/output/build.bin", restoredViewModel.Reports.LoadedReport.OutputArtifactPath);
        Assert.Equal(1, restoredViewModel.Reports.ReportHistoryCount);
    }

    /// <summary>A slow startup history load cannot replace a report published by a newer UI action.</summary>
    [Fact]
    public async Task DeferredReportHistoryLoadRejectsStalePublication()
    {
        string startupJson = ReportJsonSamples.Succeeded(runId: "startup-history-run");
        string userJson = ReportJsonSamples.Succeeded(runId: "newer-user-run");
        var pending = new TaskCompletionSource<IReadOnlyList<ReportHistorySnapshot>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Task<bool> load = viewModel.Reports.LoadReportHistoryAsync(_ => pending.Task, CancellationToken.None);
        viewModel.Reports.LoadReportJson(userJson, "user-report.json");
        pending.SetResult([new ReportHistorySnapshot("startup.json", startupJson, string.Empty)]);

        Assert.False(await load);
        Assert.Equal("newer-user-run", viewModel.Reports.LoadedReport.RunId);
        Assert.Equal(userJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal("user-report.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
    }

    /// <summary>The production startup path publishes the same latest report and compact history semantics.</summary>
    [Fact]
    public async Task DeferredReportHistoryLoadPublishesPreparedState()
    {
        string startupJson = ReportJsonSamples.Succeeded(runId: "prepared-startup-run");
        var snapshot = new ReportHistorySnapshot(
            "prepared-startup.json",
            startupJson,
            "C:/output/prepared.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        bool published = await viewModel.Reports.LoadReportHistoryAsync(
            _ => Task.FromResult<IReadOnlyList<ReportHistorySnapshot>>([snapshot]),
            CancellationToken.None);

        Assert.True(published);
        Assert.Equal("prepared-startup-run", viewModel.Reports.LoadedReport.RunId);
        Assert.Equal(startupJson, viewModel.Reports.LoadedReportJson);
        ReportHistoryEntryViewModel entry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("prepared-startup.json", entry.SourceName);
        Assert.NotEqual(ReportHistoryMetadataSnapshot.Empty, entry.ToSnapshot().Metadata);
    }

    /// <summary>Closing or cancelling startup prevents a pending history worker from publishing partial state.</summary>
    [Fact]
    public async Task DeferredReportHistoryCancellationPublishesNothing()
    {
        string startupJson = ReportJsonSamples.Succeeded(runId: "cancelled-startup-run");
        var pending = new TaskCompletionSource<IReadOnlyList<ReportHistorySnapshot>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Task<bool> load = viewModel.Reports.LoadReportHistoryAsync(_ => pending.Task, cancellation.Token);
        cancellation.Cancel();
        pending.SetResult([new ReportHistorySnapshot("cancelled.json", startupJson, string.Empty)]);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        Assert.Empty(viewModel.Reports.ReportHistoryEntries);
        Assert.False(viewModel.Reports.HasLoadedReport);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReportJson);
    }

    /// <summary>A slow explicit startup report cannot replace a report published by a newer UI action.</summary>
    [Fact]
    public async Task DeferredReportSourceLoadRejectsStalePublication()
    {
        string startupJson = ReportJsonSamples.Succeeded(runId: "startup-explicit-run");
        string userJson = ReportJsonSamples.Succeeded(runId: "newer-explicit-user-run");
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Task<bool> load = viewModel.Reports.LoadReportJsonAsync(
            _ => pending.Task,
            "startup-report.json",
            CancellationToken.None);
        viewModel.Reports.LoadReportJson(userJson, "user-report.json");
        pending.SetResult(startupJson);

        Assert.False(await load);
        Assert.Equal("newer-explicit-user-run", viewModel.Reports.LoadedReport.RunId);
        Assert.Equal(userJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal("user-report.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
    }

    /// <summary>The production startup source path projects and publishes a current report normally.</summary>
    [Fact]
    public async Task DeferredReportSourceLoadPublishesCurrentReport()
    {
        string startupJson = ReportJsonSamples.Succeeded(runId: "current-explicit-startup-run");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        bool published = await viewModel.Reports.LoadReportJsonAsync(
            _ => Task.FromResult(startupJson),
            "current-startup.json",
            CancellationToken.None);

        Assert.True(published);
        Assert.Equal("current-explicit-startup-run", viewModel.Reports.LoadedReport.RunId);
        Assert.Equal(startupJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal("current-startup.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
    }

    /// <summary>A current startup source failure keeps the existing readable load-error contract.</summary>
    [Fact]
    public async Task DeferredReportSourceFailurePublishesReadableError()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        bool published = await viewModel.Reports.LoadReportJsonAsync(
            _ => Task.FromException<string>(new IOException("startup storage unavailable")),
            "missing-startup.json",
            CancellationToken.None);

        Assert.True(published);
        Assert.Equal("Load failed", viewModel.Reports.LoadedReport.Status);
        Assert.Contains("startup storage unavailable", viewModel.Reports.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReportJson);
        Assert.Equal("missing-startup.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
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

        viewModel.Reports.LoadReportHistory([latest, deferred]);

        Assert.Equal(2, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("latest-run", viewModel.Reports.LoadedReport.RunId);
        ReportHistoryEntryViewModel deferredEntry = viewModel.Reports.ReportHistoryEntries[1];
        Assert.Equal("Deferred report", deferredEntry.Title);
        Assert.Equal(
            Encoding.UTF8.GetByteCount(deferredJson) + Encoding.UTF8.GetByteCount(deferred.OutputArtifactPath),
            deferredEntry.StoredByteCount);
        Assert.Same(deferred, deferredEntry.ToSnapshot());

        await viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(deferredEntry);

        Assert.Equal(deferredJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal("deferred-run", viewModel.Reports.LoadedReport.RunId);
        Assert.NotEqual(deferredEntry.Title, viewModel.Reports.LoadedReport.Title);
        Assert.Equal(2, viewModel.Reports.ReportHistoryCount);
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

        viewModel.Reports.LoadReportHistory([invalid]);

        Assert.Equal(1, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("Invalid JSON", viewModel.Reports.LoadedReport.Status);
        Assert.True(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.Equal("[]", viewModel.Reports.LoadedReportJson);
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

        viewModel.Reports.LoadReportHistory([malformed]);

        Assert.Empty(viewModel.Reports.ReportHistoryEntries);
        Assert.False(viewModel.Reports.HasLoadedReport);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReportJson);
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
            /*lang=json,strict*/ "{\"Operations\":[0]}",
            string.Empty,
            invalidMetadata);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.Reports.LoadReportHistory([latest, invalid]);

        Assert.Equal("latest-safe-run", viewModel.Reports.LoadedReport.RunId);
        ReportHistoryEntryViewModel invalidEntry = viewModel.Reports.ReportHistoryEntries[1];

        await viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(invalidEntry);

        Assert.Equal("Invalid JSON", viewModel.Reports.LoadedReport.Status);
        Assert.True(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.Equal(invalid.ReportJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal(2, viewModel.Reports.ReportHistoryCount);
    }
}
