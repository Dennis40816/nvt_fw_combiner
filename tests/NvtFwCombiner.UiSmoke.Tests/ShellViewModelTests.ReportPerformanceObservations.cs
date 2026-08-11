using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Emits non-gating Node B/C observations for the bounded 10,000-range Hex Diff path.</summary>
    [Fact]
    public async Task ReportHexDiffEmitsColdWarmProjectionAndRangeSelectionObservations()
    {
        CompositionRunResult result = await CreateDpReplaceInspectionResultAsync();
        using var source = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        string runId = source.RootElement.GetProperty("RunId").GetString()!;
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(
            count: 10_000,
            sectionCount: 8,
            runId: runId,
            outputSize: result.OutputSize,
            outputSha256: result.OutputSha256,
            reviewEvery: 100);
        using var process = Process.GetCurrentProcess();

        process.Refresh();
        long workingSetBefore = process.WorkingSet64;
        int coldGen0Before = GC.CollectionCount(0);
        int coldGen1Before = GC.CollectionCount(1);
        int coldGen2Before = GC.CollectionCount(2);
        long coldAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long coldTimestamp = Stopwatch.GetTimestamp();
        var cold = ReportReviewViewModel.FromJsonCancellable(
            json,
            "cold large preview report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);
        TimeSpan coldElapsed = Stopwatch.GetElapsedTime(coldTimestamp);
        long coldAllocated = GC.GetAllocatedBytesForCurrentThread() - coldAllocatedBefore;
        int coldGen0 = GC.CollectionCount(0) - coldGen0Before;
        int coldGen1 = GC.CollectionCount(1) - coldGen1Before;
        int coldGen2 = GC.CollectionCount(2) - coldGen2Before;

        process.Refresh();
        long workingSetAfterCold = process.WorkingSet64;
        int warmGen0Before = GC.CollectionCount(0);
        int warmGen1Before = GC.CollectionCount(1);
        int warmGen2Before = GC.CollectionCount(2);
        long warmAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long warmTimestamp = Stopwatch.GetTimestamp();
        var warm = ReportReviewViewModel.FromJsonCancellable(
            json,
            "warm large preview report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);
        TimeSpan warmElapsed = Stopwatch.GetElapsedTime(warmTimestamp);
        long warmAllocated = GC.GetAllocatedBytesForCurrentThread() - warmAllocatedBefore;
        int warmGen0 = GC.CollectionCount(0) - warmGen0Before;
        int warmGen1 = GC.CollectionCount(1) - warmGen1Before;
        int warmGen2 = GC.CollectionCount(2) - warmGen2Before;

        process.Refresh();
        long workingSetAfterWarm = process.WorkingSet64;
        long testhostLifetimePeakWorkingSet = process.PeakWorkingSet64;

        ReportHexDiffRangeViewModel target = warm.HexDiff.Ranges[1];
        long selectionAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long selectionTimestamp = Stopwatch.GetTimestamp();
        warm.HexDiff.SelectRangeCommand.Execute(target);
        TimeSpan selectionElapsed = Stopwatch.GetElapsedTime(selectionTimestamp);
        long selectionAllocated = GC.GetAllocatedBytesForCurrentThread() - selectionAllocatedBefore;

        Assert.True(cold.HexDiff.IsAvailable);
        Assert.True(warm.HexDiff.IsAvailable);
        Assert.Equal(10_000, cold.HexDiff.Ranges.Count);
        Assert.Equal(1, cold.HexDiff.MaterializedRangeCount);
        Assert.InRange(cold.HexDiff.ViewportSnapshot.Rows.Count, 1, 12);
        Assert.Equal(10_000, warm.HexDiff.Ranges.Count);
        Assert.Equal(2, warm.HexDiff.MaterializedRangeCount);
        ReportHexDiffRangeViewModel selected = Assert.IsType<ReportHexDiffRangeViewModel>(
            warm.HexDiff.SelectedRange);
        Assert.Same(target, selected);
        Assert.True(selected.IsReviewRequired);
        Assert.True(selected.IsSelected);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"HEX_DIFF_BASELINE ranges=10000 outputSha256={result.OutputSha256} jsonChars={json.Length} " +
            $"coldProjectionMs={coldElapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} " +
            $"coldAllocated={coldAllocated} " +
            $"coldGc0={coldGen0} coldGc1={coldGen1} coldGc2={coldGen2} " +
            $"warmProjectionMs={warmElapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} " +
            $"warmAllocated={warmAllocated} " +
            $"warmGc0={warmGen0} warmGc1={warmGen1} warmGc2={warmGen2} " +
            $"selectionMs={selectionElapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} " +
            $"selectionAllocated={selectionAllocated} " +
            $"workingSetBefore={workingSetBefore} workingSetAfterCold={workingSetAfterCold} " +
            $"workingSetAfterWarm={workingSetAfterWarm} " +
            $"testhostLifetimePeakWorkingSet={testhostLifetimePeakWorkingSet}");
    }
}
