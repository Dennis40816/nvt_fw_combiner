using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Current and reopened reports project identical bytes through the shared viewport.</summary>
    [Fact]
    public async Task ReportHexDiffUsesVerifiedAndPersistedReplayBytes()
    {
        WorkbenchRunResult result = await CreateDpReplaceInspectionResultAsync();
        var report = ReportReviewViewModel.FromJsonCancellable(
            result.ReportJson,
            "preview report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);

        Assert.True(report.HexDiff.IsAvailable);
        Assert.True(report.HexDiff.HasDifferenceWorkspace);
        Assert.False(report.HexDiff.IsReportedRangeMode);
        Assert.Equal("Complete Hex Diff", report.HexDiff.AvailabilityTitle);
        Assert.Equal(WorkbenchAddressSpaceIds.OutputImage, report.HexDiff.OutputSpaceId);
        Assert.Equal(WorkbenchAddressSpaceIds.ReferenceBase, report.HexDiff.ReferenceSpaceId);
        Assert.Equal(0x40000 / 16, report.HexDiff.TotalRowCount);
        Assert.Same(HexViewportCapabilityProfile.ReportDiff, report.HexDiff.ViewportSnapshot.Profile);
        Assert.InRange(report.HexDiff.ViewportSnapshot.Rows.Count, 1, 12);
        ReportHexDiffRangeViewModel selected = Assert.IsType<ReportHexDiffRangeViewModel>(report.HexDiff.SelectedRange);
        Assert.Equal(0x100, selected.Start);
        Assert.True(selected.IsSelected);
        Assert.Contains("output-image", selected.AccessibleRange, StringComparison.Ordinal);
        Assert.Contains("half-open", selected.AccessibleRange, StringComparison.Ordinal);
        Assert.Equal(
            $"[0x{selected.Start:X6}, 0x{selected.EndExclusive:X6})",
            selected.DisplayRange);
        HexViewportCell currentChangedCell = report.HexDiff.ViewportSnapshot.Rows
            .SelectMany(static row => row.Cells)
            .Single(cell => cell.Address == 0x100);
        Assert.Equal((byte)0xA5, currentChangedCell.PrimaryValue);
        _ = Assert.NotNull(currentChangedCell.ComparisonValue);
        Assert.True(currentChangedCell.IsDataChanged);
        Assert.Equal(0, report.HexDiff.RangeScrollMaximum);

        var reopened = ReportReviewViewModel.FromJson(result.ReportJson, "persisted report");
        Assert.True(reopened.HexDiff.IsAvailable);
        Assert.True(reopened.HexDiff.IsReportedRangeMode);
        Assert.Equal("Replayable Report Hex Diff", reopened.HexDiff.AvailabilityTitle);
        ReportHexDiffRangeViewModel reopenedRange = Assert.IsType<ReportHexDiffRangeViewModel>(
            reopened.HexDiff.SelectedRange);
        Assert.True(reopenedRange.HasReplay);
        Assert.Contains("two context rows", reopenedRange.ReplayCoverage, StringComparison.Ordinal);
        HexViewportCell reopenedChangedCell = reopened.HexDiff.ViewportSnapshot.Rows
            .SelectMany(static row => row.Cells)
            .Single(cell => cell.Address == 0x100);
        Assert.Equal(currentChangedCell.PrimaryValue, reopenedChangedCell.PrimaryValue);
        Assert.Equal(currentChangedCell.ComparisonValue, reopenedChangedCell.ComparisonValue);
        Assert.Equal(currentChangedCell.Decorations, reopenedChangedCell.Decorations);
        Assert.False(reopened.HexDiff.ViewportSnapshot.ShowComparisonRows);
        reopened.HexDiff.ShowOriginalRows = true;
        Assert.True(reopened.HexDiff.ViewportSnapshot.ShowComparisonRows);
        Assert.Contains("\"Replay\"", result.ReportJson, StringComparison.Ordinal);
        Assert.DoesNotContain("InspectionSnapshot", result.ReportJson, StringComparison.Ordinal);

        string noDifferenceJson = ReportJsonSamples.Succeeded(
            runId: result.InspectionSnapshot!.RunId,
            outputSize: result.OutputSize,
            outputSha256: result.OutputSha256,
            compositionKind: "Replace");
        var noDifferenceReport = ReportReviewViewModel.FromJsonCancellable(
            noDifferenceJson,
            "no difference report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);
        Assert.True(noDifferenceReport.HexDiff.IsAvailable);
        Assert.False(noDifferenceReport.HexDiff.HasDifferenceWorkspace);
        Assert.True(noDifferenceReport.HexDiff.HasNoViewportBytes);
    }

    /// <summary>The report navigator labels describe navigation, explanation, and mutation detail clearly.</summary>
    [Fact]
    public void ReportHexDiffNavigatorUsesClearLocalizedNavigationLanguage()
    {
        var english = ShellTextResources.For(ShellLanguage.English);
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);

        Assert.Equal("Viewing", english.HexDiffSelectedRangeLabel);
        Assert.Equal("Why", english.HexDiffWhyLabel);
        Assert.Equal("Mutation details", english.ChangedRangesTitle);
        Assert.Equal("目前檢視", traditionalChinese.HexDiffSelectedRangeLabel);
        Assert.Equal("原因", traditionalChinese.HexDiffWhyLabel);
        Assert.Equal("異動明細", traditionalChinese.ChangedRangesTitle);
    }

    /// <summary>Unverified snapshots and invalid ranges never resurrect preview bytes as a trusted viewport.</summary>
    [Fact]
    public async Task ReportHexDiffRejectsUnverifiedSnapshotAndRangeIdentity()
    {
        WorkbenchRunResult result = await CreateDpReplaceInspectionResultAsync();
        using var source = JsonDocument.Parse(result.ReportJson);
        string runId = source.RootElement.GetProperty("RunId").GetString()!;
        (string Name, string Json)[] invalidReports =
        [
            (
                "output SHA mismatch",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    "different-output-sha",
                    (0, 4, 4, 4))),
            (
                "output size mismatch",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize - 1,
                    result.OutputSha256,
                    (0, 4, 4, 4))),
            (
                "out-of-bounds range",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (result.OutputSize - 1, result.OutputSize + 1, 2, 2))),
            (
                "overlapping ranges",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 4, 4, 4),
                    (3, 5, 2, 2))),
        ];

        foreach ((string name, string json) in invalidReports)
        {
            var report = ReportReviewViewModel.FromJsonCancellable(
                json,
                name,
                outputArtifactPath: null,
                result.InspectionSnapshot,
                ShellLanguage.English,
                TestContext.Current.CancellationToken);

            Assert.False(report.HexDiff.IsAvailable);
            Assert.True(report.HexDiff.HasDifferenceWorkspace);
            Assert.False(report.HexDiff.IsReportedRangeMode);
            Assert.True(report.HexDiff.HasNoViewportBytes);
        }
    }

    /// <summary>A legacy preview remains useful in report text but is not presented as replayable bytes.</summary>
    [Fact]
    public void ReportHexDiffMarksLegacyPreviewWithoutReplayUnavailable()
    {
        string json = ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
            "historical-range-preview",
            outputSize: 0x400,
            outputSha256: "historical-output-sha",
            (0x100, 0x140, 0x40, 4));

        var report = ReportReviewViewModel.FromJson(json, "historical report");

        Assert.False(report.HexDiff.IsAvailable);
        Assert.False(report.HexDiff.IsReportedRangeMode);
        ReportHexDiffRangeViewModel range = Assert.IsType<ReportHexDiffRangeViewModel>(report.HexDiff.SelectedRange);
        Assert.False(range.HasReplay);
        Assert.Contains("legacy report", range.ReplayCoverage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable", report.HexDiff.AvailabilityDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(report.HexDiff.ViewportSnapshot.Rows);
    }

    /// <summary>Long persisted ranges scroll only inside their replay segment and retain a 12-row window.</summary>
    [Fact]
    public async Task ReportHexDiffKeepsLongRangeScrollingLocalAndBounded()
    {
        WorkbenchRunResult result = await CreateDpReplaceInspectionResultAsync(changeLength: 0x200);
        var report = ReportReviewViewModel.FromJson(result.ReportJson, "persisted long range");

        Assert.True(report.HexDiff.IsReportedRangeMode);
        ReportHexDiffRangeViewModel range = Assert.IsType<ReportHexDiffRangeViewModel>(report.HexDiff.SelectedRange);
        Assert.Equal(0x200, range.Length);
        Assert.True(report.HexDiff.RangeScrollMaximum > 0);
        int maximum = report.HexDiff.RangeScrollMaximum;
        report.HexDiff.RangeScrollRow = int.MaxValue;

        Assert.Equal(maximum, report.HexDiff.RangeScrollRow);
        Assert.Equal(12, report.HexDiff.ViewportSnapshot.Rows.Count);
        Assert.True(report.HexDiff.FirstVisibleOffset >= range.Replay!.Range.Start);
        Assert.True(report.HexDiff.ViewportSnapshot.Rows[^1].Cells[^1].Address < range.Replay.Range.EndExclusive);

        report.HexDiff.HandleViewportIntent(new HexViewportInteractionIntent(
            HexViewportInteractionTrigger.Scroll,
            Address: null,
            default,
            Delta: -3));
        Assert.Equal(maximum - 3, report.HexDiff.RangeScrollRow);
        Assert.Same(range, report.HexDiff.SelectedRange);
    }

    /// <summary>A 10,000-range report exposes lazy rows for a virtualized semantic navigator.</summary>
    [Fact]
    public async Task ReportHexDiffKeepsLargeRangeNavigationBounded()
    {
        WorkbenchRunResult result = await CreateDpReplaceInspectionResultAsync();
        using var source = JsonDocument.Parse(result.ReportJson);
        string runId = source.RootElement.GetProperty("RunId").GetString()!;
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(
            count: 10_000,
            sectionCount: 8,
            runId: runId,
            outputSize: result.OutputSize,
            outputSha256: result.OutputSha256);

        var report = ReportReviewViewModel.FromJsonCancellable(
            json,
            "large report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);

        Assert.True(report.HexDiff.IsAvailable);
        Assert.Equal(10_000, report.HexDiff.Ranges.Count);
        Assert.Equal(1, report.HexDiff.MaterializedRangeCount);
        Assert.InRange(report.HexDiff.ViewportSnapshot.Rows.Count, 1, 12);
        ReportHexDiffRangeViewModel first = report.HexDiff.Ranges[0];
        Assert.Same(first, report.HexDiff.SelectedRange);

        ReportHexDiffRangeViewModel distantSelection = report.HexDiff.Ranges[9_000];
        Assert.Equal(2, report.HexDiff.MaterializedRangeCount);
        report.HexDiff.SelectRangeCommand.Execute(distantSelection);
        Assert.Same(distantSelection, report.HexDiff.SelectedRange);
        Assert.False(first.IsSelected);
        Assert.True(distantSelection.IsSelected);
        Assert.Equal(2, report.HexDiff.MaterializedRangeCount);

        string mismatchedJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(
            count: 1,
            sectionCount: 1,
            runId: "another-run",
            outputSize: result.OutputSize,
            outputSha256: result.OutputSha256);
        var mismatch = ReportReviewViewModel.FromJsonCancellable(
            mismatchedJson,
            "mismatched report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);
        Assert.False(mismatch.HexDiff.IsAvailable);
        Assert.True(mismatch.HexDiff.HasNoViewportBytes);
    }

    private static async Task<WorkbenchRunResult> CreateDpReplaceInspectionResultAsync(int changeLength = 2)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-report-hex-diff");
        byte[] baseBytes = CreatePattern(0x40000, 0x51);
        byte[] replacementBytes = (byte[])baseBytes.Clone();
        for (int index = 0; index < changeLength; index++)
        {
            replacementBytes[0x100 + index] ^= 0xFF;
        }

        replacementBytes[0x100] = 0xA5;
        replacementBytes[0x101] = 0x5A;
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", replacementBytes);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-dp"] = replacementPath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "DP",
            paths,
            build: false,
            TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.ReportJson);
        _ = Assert.IsType<Application.Composition.CompositionRunInspectionSnapshot>(result.InspectionSnapshot);
        return result;
    }
}
