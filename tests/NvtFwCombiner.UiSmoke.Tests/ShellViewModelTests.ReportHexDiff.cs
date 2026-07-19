using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Complete in-session bytes drive a bounded full-image viewport and the original-row toggle.</summary>
    [Fact]
    public async Task ReportHexDiffUsesVerifiedSessionSnapshotWithoutPersistingBytes()
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
        Assert.Equal(18, report.HexDiff.ViewportRowCount);
        Assert.Equal((0x40000 / 16) - 18, report.HexDiff.DocumentScrollMaximum);
        Assert.True(report.HexDiff.HasCompleteOutputScroll);
        Assert.Equal(18, report.HexDiff.VisibleRows.Count);
        ReportHexDiffRangeViewModel selected = Assert.IsType<ReportHexDiffRangeViewModel>(report.HexDiff.SelectedRange);
        Assert.Equal(0x100, selected.Start);
        Assert.True(selected.IsSelected);
        Assert.Contains("output-image", selected.AccessibleRange, StringComparison.Ordinal);
        Assert.Contains("half-open", selected.AccessibleRange, StringComparison.Ordinal);
        Assert.Contains(selected.Title, selected.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains(selected.Status, selected.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains(selected.ChangedSummary, selected.AccessibleLabel, StringComparison.Ordinal);
        ReportHexDiffViewportRowViewModel changedRow = Assert.Single(
            report.HexDiff.VisibleRows,
            row => row.Start == 0x100);
        Assert.Equal(0b11, changedRow.ChangedMask);
        Assert.Contains("A5 5A", changedRow.OutputHex, StringComparison.Ordinal);
        Assert.Equal(16, changedRow.OutputBytes.Count);
        Assert.True(changedRow.OutputBytes[0].IsChanged);
        Assert.True(changedRow.OutputBytes[1].IsChanged);
        Assert.All(changedRow.OutputBytes.Skip(2), static cell => Assert.False(cell.IsChanged));
        Assert.False(changedRow.IsOriginalVisible);
        Assert.Contains("changed", changedRow.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("output", changedRow.AccessibleLabel, StringComparison.Ordinal);
        ReportHexDiffViewportRowViewModel unchangedRow = Assert.Single(
            report.HexDiff.VisibleRows,
            row => row.Start == 0x110);
        Assert.Contains("unchanged", unchangedRow.AccessibleLabel, StringComparison.Ordinal);

        report.HexDiff.ShowOriginalRows = true;
        Assert.True(changedRow.IsOriginalVisible);
        Assert.Contains("original", changedRow.AccessibleLabel, StringComparison.Ordinal);
        Assert.All(
            report.HexDiff.VisibleRows.Where(static row => !row.HasChanges),
            static row => Assert.False(row.IsOriginalVisible));

        report.HexDiff.ViewportStartRow = 0;
        Assert.Equal(0, report.HexDiff.FirstVisibleOffset);
        Assert.Same(selected, report.HexDiff.SelectedRange);
        report.HexDiff.ViewportStartRow = 0x200 / 16;
        Assert.Equal(0x200, report.HexDiff.FirstVisibleOffset);
        report.HexDiff.SelectRangeCommand.Execute(selected);
        Assert.Equal(selected, report.HexDiff.SelectedRange);
        Assert.True(selected.IsSelected);
        Assert.Equal(0x100, report.HexDiff.FirstVisibleOffset);
        report.HexDiff.ViewportStartRow = int.MaxValue;
        Assert.Equal(report.HexDiff.DocumentScrollMaximum, report.HexDiff.ViewportStartRow);
        Assert.Equal((long)report.HexDiff.DocumentScrollMaximum * 16, report.HexDiff.FirstVisibleOffset);

        var reopened = ReportReviewViewModel.FromJson(result.ReportJson, "persisted report");
        Assert.False(reopened.HexDiff.IsAvailable);
        Assert.True(reopened.HexDiff.HasDifferenceWorkspace);
        Assert.True(reopened.HexDiff.IsReportedRangeMode);
        Assert.False(reopened.HexDiff.HasCompleteOutputScroll);
        Assert.Equal("Reported-range Hex Diff", reopened.HexDiff.AvailabilityTitle);
        Assert.Contains("stored before/output previews", reopened.HexDiff.AvailabilityDetail, StringComparison.Ordinal);
        ReportHexDiffRangeViewModel reopenedRange = Assert.IsType<ReportHexDiffRangeViewModel>(
            reopened.HexDiff.SelectedRange);
        Assert.Equal(0x100, reopenedRange.Start);
        Assert.True(reopenedRange.IsPreviewComplete);
        Assert.Equal(2, reopenedRange.PreviewByteCount);
        ReportHexDiffViewportRowViewModel reopenedRow = Assert.Single(reopened.HexDiff.VisibleRows);
        Assert.Equal(0x100, reopenedRow.Start);
        Assert.Contains("A5 5A", reopenedRow.OutputHex, StringComparison.Ordinal);
        reopened.HexDiff.ShowOriginalRows = true;
        Assert.True(reopenedRow.IsOriginalVisible);
        Assert.Contains("original", reopenedRow.AccessibleLabel, StringComparison.Ordinal);
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
    }

    /// <summary>Unverified snapshots use report previews only when report ranges remain valid.</summary>
    [Fact]
    public async Task ReportHexDiffRejectsUnverifiedSnapshotAndRangeIdentity()
    {
        WorkbenchRunResult result = await CreateDpReplaceInspectionResultAsync();
        using var source = JsonDocument.Parse(result.ReportJson);
        string runId = source.RootElement.GetProperty("RunId").GetString()!;
        (string Name, string Json, bool HasReportedRangePreview)[] invalidReports =
        [
            (
                "output SHA mismatch",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    "different-output-sha",
                    (0, 4, 4, 4)),
                true),
            (
                "output size mismatch",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize - 1,
                    result.OutputSha256,
                    (0, 4, 4, 4)),
                true),
            (
                "out-of-bounds range",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (result.OutputSize - 1, result.OutputSize + 1, 2, 2)),
                false),
            (
                "overlapping ranges",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 4, 4, 4),
                    (3, 5, 2, 2)),
                false),
            (
                "zero changed-byte count",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 4, 4, 0)),
                false),
            (
                "changed-byte count exceeds range",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 4, 4, 5)),
                false),
            (
                "inconsistent end-exclusive",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 8, 4, 4)),
                false),
        ];

        foreach ((string name, string json, bool hasReportedRangePreview) in invalidReports)
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
            Assert.Equal(hasReportedRangePreview, report.HexDiff.IsReportedRangeMode);
            Assert.Equal(hasReportedRangePreview, report.HexDiff.VisibleRows.Count > 0);
        }
    }

    /// <summary>Historical reports never fabricate bytes beyond their bounded preview.</summary>
    [Fact]
    public void ReportHexDiffMarksAndBoundsTruncatedHistoricalPreviews()
    {
        string json = ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
            "historical-range-preview",
            outputSize: 0x400,
            outputSha256: "historical-output-sha",
            (0x100, 0x140, 0x40, 4));

        var report = ReportReviewViewModel.FromJson(json, "historical report");

        Assert.True(report.HexDiff.IsReportedRangeMode);
        ReportHexDiffRangeViewModel range = Assert.IsType<ReportHexDiffRangeViewModel>(report.HexDiff.SelectedRange);
        Assert.False(range.IsPreviewComplete);
        Assert.Equal(4, range.PreviewByteCount);
        Assert.Contains("first 4 of 64 bytes", range.PreviewCoverage, StringComparison.Ordinal);
        ReportHexDiffViewportRowViewModel row = Assert.Single(report.HexDiff.VisibleRows);
        Assert.Equal(0x100, row.Start);
        Assert.Equal("11 22 33 44", row.OutputHex);

        Assert.Same(range, report.HexDiff.SelectedRange);
        Assert.Equal(0x100, report.HexDiff.FirstVisibleOffset);
        Assert.False(report.HexDiff.HasCompleteOutputScroll);
        _ = Assert.Single(report.HexDiff.VisibleRows);
    }

    /// <summary>A 10,000-range report indexes review-first rows without materializing the full list or image.</summary>
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
            "large preview report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);

        Assert.True(report.HexDiff.IsAvailable);
        Assert.Equal(10_000, report.HexDiff.NavigatorPage.TotalCount);
        Assert.Equal(64, report.HexDiff.NavigatorPage.VisibleCount);
        Assert.InRange(report.HexDiff.MaterializedRangeCount, 1, 64);
        Assert.Equal(18, report.HexDiff.VisibleRows.Count);
        Assert.Equal(0x40000 / 16, report.HexDiff.TotalRowCount);
        ReportHexDiffRangeViewModel first = Assert.IsType<ReportHexDiffRangeViewModel>(
            report.HexDiff.NavigatorPage.Items[0]);
        Assert.True(first.IsReviewRequired);
        Assert.Equal(39_996, first.Start);

        string fragmentedJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(
            count: 10_000,
            sectionCount: 8,
            runId: runId,
            outputSize: result.OutputSize,
            outputSha256: result.OutputSha256,
            reviewEvery: 100);
        var fragmented = ReportReviewViewModel.FromJsonCancellable(
            fragmentedJson,
            "fragmented preview report",
            outputArtifactPath: null,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);
        Assert.Equal(64, fragmented.HexDiff.NavigatorPage.VisibleCount);

        ReportHexDiffRangeViewModel initialSelection = Assert.IsType<ReportHexDiffRangeViewModel>(
            fragmented.HexDiff.SelectedRange);
        Assert.Contains(initialSelection, fragmented.HexDiff.NavigatorPage.Items);

        fragmented.HexDiff.NavigatorPage.NextPageCommand.Execute(null);
        Assert.Equal(1, fragmented.HexDiff.NavigatorPage.PageIndex);
        Assert.Equal(64, fragmented.HexDiff.NavigatorPage.VisibleCount);
        Assert.Same(initialSelection, fragmented.HexDiff.PinnedSelectedRange);
        Assert.Equal(65, fragmented.HexDiff.VisibleNavigatorRowCount);
        Assert.Equal(65, fragmented.HexDiff.MaterializedRangeCount);
        Assert.Equal(1, CountSelectedNavigatorRows(fragmented.HexDiff));

        ReportHexDiffRangeViewModel visiblePageSelection = Assert.IsType<ReportHexDiffRangeViewModel>(
            fragmented.HexDiff.NavigatorPage.Items[0]);
        fragmented.HexDiff.SelectRangeCommand.Execute(visiblePageSelection);
        Assert.Same(visiblePageSelection, fragmented.HexDiff.SelectedRange);
        Assert.Null(fragmented.HexDiff.PinnedSelectedRange);
        Assert.False(initialSelection.IsSelected);
        Assert.True(visiblePageSelection.IsSelected);
        Assert.Equal(64, fragmented.HexDiff.VisibleNavigatorRowCount);
        Assert.Equal(64, fragmented.HexDiff.MaterializedRangeCount);
        Assert.Equal(1, CountSelectedNavigatorRows(fragmented.HexDiff));

        fragmented.HexDiff.NavigatorPage.PreviousPageCommand.Execute(null);
        Assert.Equal(0, fragmented.HexDiff.NavigatorPage.PageIndex);
        Assert.Equal(64, fragmented.HexDiff.NavigatorPage.VisibleCount);
        Assert.Same(visiblePageSelection, fragmented.HexDiff.PinnedSelectedRange);
        Assert.Equal(65, fragmented.HexDiff.VisibleNavigatorRowCount);
        Assert.Equal(65, fragmented.HexDiff.MaterializedRangeCount);
        Assert.Equal(1, CountSelectedNavigatorRows(fragmented.HexDiff));

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
        Assert.Contains("do not match", mismatch.HexDiff.AvailabilityDetail, StringComparison.Ordinal);

    }

    private static int CountSelectedNavigatorRows(ReportHexDiffViewModel hexDiff)
    {
        int pageSelectionCount = hexDiff.NavigatorPage.Items
            .OfType<ReportHexDiffRangeViewModel>()
            .Count(static range => range.IsSelected);
        return pageSelectionCount + (hexDiff.PinnedSelectedRange?.IsSelected == true ? 1 : 0);
    }

    private static async Task<WorkbenchRunResult> CreateDpReplaceInspectionResultAsync()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-report-hex-diff");
        byte[] baseBytes = CreatePattern(0x40000, 0x51);
        byte[] replacementBytes = (byte[])baseBytes.Clone();
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
