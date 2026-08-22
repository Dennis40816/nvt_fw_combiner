using System.Text;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <inheritdoc/>
[Collection(UiProcessWideObservationCollection.Name)]
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
        ReportHistoryTestStore.Save(historyPath, [original]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReportHistoryTestStore.SaveAsync(historyPath, [cancelled], cancellation.Token));

        Assert.Equal("original.json", Assert.Single(ReportHistoryTestStore.Load(historyPath)).SourceName);

        await ReportHistoryTestStore.SaveAsync(
            historyPath,
            [latest],
            TestContext.Current.CancellationToken);

        ReportHistorySnapshot loaded = Assert.Single(ReportHistoryTestStore.Load(historyPath));
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
            await ReportHistoryTestStore.SaveAsync(historyPath, snapshots, cancellationToken);
        });
        coordinator.Queue([CreateSnapshot("latest-before-close.json")]);
        await saveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task completion = coordinator.CompleteAsync();

        Assert.False(completion.IsCompleted);
        releaseSave.SetResult();
        await completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            "latest-before-close.json",
            Assert.Single(ReportHistoryTestStore.Load(historyPath)).SourceName);
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

    /// <summary>An oversized best-effort history file is rejected before its JSON payload is read.</summary>
    [Fact]
    public void LoadRejectsOversizedHistoryFile()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-size");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
        using (FileStream stream = File.Create(historyPath))
        {
            stream.SetLength(ReportHistoryFileStore.MaximumHistoryFileBytes + 1);
        }

        Assert.Empty(ReportHistoryTestStore.Load(historyPath));
    }

    /// <summary>Restoring a large history does not allocate a second whole-file text value.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadLargeHistoryAvoidsWholeFileTextAllocation(bool useLegacyUtf16Encoding)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-allocation");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        const int reportCharacterCount = 4 * 1024 * 1024;
        string reportJson = $"\"{new string('A', reportCharacterCount - 2)}\"";
        ReportHistoryTestStore.Save(
            historyPath,
            [new ReportHistorySnapshot("large.json", reportJson, string.Empty)]);
        if (useLegacyUtf16Encoding)
        {
            string persistedJson = File.ReadAllText(historyPath);
            File.WriteAllText(historyPath, persistedJson, Encoding.Unicode);
        }

        _ = ReportHistoryTestStore.Load(historyPath);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ReportHistorySnapshot loaded = Assert.Single(ReportHistoryTestStore.Load(historyPath));
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(reportJson, loaded.ReportJson);
        Assert.InRange(allocatedBytes, 0, reportCharacterCount * 3L);
    }

    /// <summary>An atomic save can replace the path while an existing reader keeps a complete old snapshot.</summary>
    [Fact]
    public async Task AsyncSaveReplacesSnapshotHeldByReader()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-concurrent-save");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        ReportHistorySnapshot original = CreateSnapshot("original.json");
        ReportHistorySnapshot latest = CreateSnapshot("latest.json");
        ReportHistoryTestStore.Save(historyPath, [original]);
        using var originalReader = new FileStream(
            historyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete);

        await ReportHistoryTestStore.SaveAsync(
            historyPath,
            [latest],
            TestContext.Current.CancellationToken);

        using var textReader = new StreamReader(
            originalReader,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);
        string originalJson = textReader.ReadToEnd();
        Assert.Contains("original.json", originalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("latest.json", originalJson, StringComparison.Ordinal);
        Assert.Equal("latest.json", Assert.Single(ReportHistoryTestStore.Load(historyPath)).SourceName);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(historyPath)!, "*.tmp"));
    }

    /// <summary>Missing state and a valid unsupported schema both fail back to empty history.</summary>
    [Fact]
    public void LoadFallsBackForMissingFileAndUnsupportedSchema()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-schema");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        Assert.Empty(ReportHistoryTestStore.Load(historyPath));
        ReportHistoryTestStore.Save(historyPath, [CreateSnapshot("unsupported.json")]);
        string unsupportedJson = File.ReadAllText(historyPath).Replace(
            "\"SchemaVersion\": 1",
            "\"SchemaVersion\": 2",
            StringComparison.Ordinal);
        File.WriteAllText(historyPath, unsupportedJson);

        Assert.Empty(ReportHistoryTestStore.Load(historyPath));
    }

    /// <summary>A maximum raw report survives the encoder's sixfold literal-less-than expansion.</summary>
    [Fact]
    public async Task MaximumRawReportRoundTripsInsideHardEnvelope()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-maximum-import");
        string historyPath = workspace.PathFor("report-history.v1.json");
        const int maximumRawBytes = 10 * 1024 * 1024;
        string reportJson = CreateRematerializableLiteralLessThanReport(maximumRawBytes);
        MainWindowViewModel expectedViewModel = PresentationTestHost.CreateViewModel(
            ShellLanguage.ChineseTraditional);
        expectedViewModel.Reports.LoadReportJson(reportJson, "maximum.json");
        ReportHistoryEntryViewModel expected = Assert.Single(expectedViewModel.Reports.ReportHistoryEntries);

        await ReportHistoryTestStore.SaveAsync(
            historyPath,
            expectedViewModel.Reports.ExportReportHistory(),
            TestContext.Current.CancellationToken);

        Assert.InRange(new FileInfo(historyPath).Length, 1, ReportHistoryFileStore.MaximumHistoryFileBytes);
        IReadOnlyList<ReportHistorySnapshot> persisted = ReportHistoryTestStore.Load(historyPath);
        Assert.Equal(ReportHistoryMetadataSnapshot.Empty, Assert.Single(persisted).Metadata);
        MainWindowViewModel restoredViewModel = PresentationTestHost.CreateViewModel(
            ShellLanguage.ChineseTraditional);
        Assert.Equal(ReportPublicationOutcome.Published, (await restoredViewModel.Reports.LoadReportHistoryAsync(
            _ => Task.FromResult(persisted),
            TestContext.Current.CancellationToken)).Outcome);
        ReportHistoryEntryViewModel restored = Assert.Single(restoredViewModel.Reports.ReportHistoryEntries);
        Assert.Equal(reportJson, restored.ReportJson);
        Assert.Equal(
            Assert.Single(expectedViewModel.Reports.ExportReportHistory()).Metadata,
            Assert.Single(restoredViewModel.Reports.ExportReportHistory()).Metadata);
    }

    /// <summary>The hard encoded-envelope bound prunes the oldest entry even below the soft payload budget.</summary>
    [Fact]
    public async Task EncodedEnvelopePrunesOldestEntryDeterministically()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-envelope-prune");
        string historyPath = workspace.PathFor("report-history.v1.json");
        const int halfSoftBudget = 8 * 1024 * 1024;
        var newest = new ReportHistorySnapshot(
            "newest.json",
            CreateLiteralLessThanJson(halfSoftBudget),
            string.Empty);
        var oldest = new ReportHistorySnapshot(
            "oldest.json",
            CreateLiteralLessThanJson(halfSoftBudget),
            string.Empty);

        await ReportHistoryTestStore.SaveAsync(
            historyPath,
            [newest, oldest],
            TestContext.Current.CancellationToken);

        ReportHistorySnapshot loaded = Assert.Single(ReportHistoryTestStore.Load(historyPath));
        Assert.Equal("newest.json", loaded.SourceName);
    }

    /// <summary>An in-process newest entry that cannot fit never replaces the previous persisted history.</summary>
    [Fact]
    public async Task EntryTooLargeToPersistPreservesPreviousFile()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history-entry-too-large");
        string historyPath = workspace.PathFor("report-history.v1.json");
        ReportHistoryTestStore.Save(historyPath, [CreateSnapshot("previous.json")]);
        var oversized = new ReportHistorySnapshot(
            "oversized.json",
            CreateRematerializableLiteralLessThanReport(10 * 1024 * 1024),
            string.Empty,
            CreateMetadata(new string('A', 5 * 1024 * 1024)));

        ReportHistoryPersistenceException exception =
            await Assert.ThrowsAsync<ReportHistoryPersistenceException>(() =>
                ReportHistoryTestStore.SaveAsync(
                    historyPath,
                    [oversized],
                    TestContext.Current.CancellationToken));

        Assert.Equal(ReportHistoryPersistenceFailure.EntryTooLargeToPersist, exception.Failure);
        Assert.Equal("previous.json", Assert.Single(ReportHistoryTestStore.Load(historyPath)).SourceName);
    }

    private static ReportHistorySnapshot CreateSnapshot(string sourceName)
    {
        return new ReportHistorySnapshot(
            sourceName,
            ReportJsonSamples.Succeeded(runId: Path.GetFileNameWithoutExtension(sourceName)),
            string.Empty);
    }

    private static string CreateLiteralLessThanJson(int rawUtf8Bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rawUtf8Bytes, 2);
        return $"\"{new string('<', rawUtf8Bytes - 2)}\"";
    }

    private static string CreateRematerializableLiteralLessThanReport(int rawUtf8Bytes)
    {
        const string OriginalProfileId = "literal-less-than-profile";
        string report = ReportJsonSamples.Succeeded(profileId: OriginalProfileId, runId: "literal-less-than-report");
        int profileOffset = report.IndexOf(OriginalProfileId, StringComparison.Ordinal);
        Assert.True(profileOffset >= 0);
        const int DerivedBytes = 768 * 1024;
        string prefix = $"{report[..profileOffset]}{new string('<', DerivedBytes)}" +
            $"{report[(profileOffset + OriginalProfileId.Length)..^1]},\"Padding\":\"";
        const string Suffix = "\"}";
        int paddingBytes = rawUtf8Bytes - Encoding.UTF8.GetByteCount(prefix) - Suffix.Length;
        ArgumentOutOfRangeException.ThrowIfNegative(paddingBytes);
        return $"{prefix}{new string('<', paddingBytes)}{Suffix}";
    }

    private static ReportHistoryMetadataSnapshot CreateMetadata(string title)
    {
        return new(
            title,
            "Succeeded",
            "context",
            "output",
            "hash",
            "commands",
            "issues",
            "evidence",
            "run",
            "2026-08-13T00:00:00Z",
            "NT51926",
            "mode",
            "experience",
            "Merge");
    }

    private static LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>> CreateCoordinator(
        Func<IReadOnlyList<ReportHistorySnapshot>, CancellationToken, Task> saveAsync)
    {
        return new LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>>(
            saveAsync,
            snapshots => [.. snapshots]);
    }
}
