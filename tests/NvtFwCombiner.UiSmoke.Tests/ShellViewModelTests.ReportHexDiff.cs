using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Complete in-session bytes drive bounded rows, checked jumps, and the original-row toggle.</summary>
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
        Assert.True(report.HexDiff.HasCompleteDifferenceWorkspace);
        Assert.Equal("Complete Hex Diff", report.HexDiff.AvailabilityTitle);
        Assert.Equal(WorkbenchAddressSpaceIds.OutputImage, report.HexDiff.OutputSpaceId);
        Assert.Equal(WorkbenchAddressSpaceIds.ReferenceBase, report.HexDiff.ReferenceSpaceId);
        Assert.Equal(0x40000 / 16, report.HexDiff.TotalRowCount);
        Assert.InRange(report.HexDiff.VisibleRows.Count, 1, 48);
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

        report.HexDiff.JumpAddress = "0x0";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0, report.HexDiff.FirstVisibleOffset);
        Assert.Null(report.HexDiff.SelectedRange);
        Assert.True(report.HexDiff.HasNoSelectedRange);
        Assert.False(selected.IsSelected);
        report.HexDiff.JumpAddress = "0x101";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0x100, report.HexDiff.FirstVisibleOffset);
        Assert.Equal(0x100, report.HexDiff.SelectedRange?.Start);
        Assert.False(report.HexDiff.HasNoSelectedRange);
        Assert.True(selected.IsSelected);
        Assert.Contains("address", report.HexDiff.JumpStatus, StringComparison.Ordinal);
        report.HexDiff.JumpAddress = "0x102";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0x100, report.HexDiff.FirstVisibleOffset);
        Assert.Null(report.HexDiff.SelectedRange);
        Assert.False(selected.IsSelected);
        report.HexDiff.SelectRangeCommand.Execute(selected);
        Assert.Equal(selected, report.HexDiff.SelectedRange);
        Assert.True(selected.IsSelected);
        Assert.Equal(0x100, report.HexDiff.FirstVisibleOffset);
        report.HexDiff.JumpAddress = "0x200";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0x200, report.HexDiff.FirstVisibleOffset);
        Assert.Null(report.HexDiff.SelectedRange);
        report.HexDiff.JumpAddress = "0x300";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0x300, report.HexDiff.FirstVisibleOffset);
        Assert.Null(report.HexDiff.SelectedRange);
        report.HexDiff.JumpAddress = "0x3FFFF";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0x3FFF0, report.HexDiff.FirstVisibleOffset);
        Assert.Null(report.HexDiff.SelectedRange);
        report.HexDiff.JumpAddress = "0x40000";
        report.HexDiff.JumpAddressCommand.Execute(null);
        Assert.Equal(0x3FFF0, report.HexDiff.FirstVisibleOffset);
        Assert.Contains("inside output-image", report.HexDiff.JumpStatus, StringComparison.Ordinal);

        var reopened = ReportReviewViewModel.FromJson(result.ReportJson, "persisted report");
        Assert.False(reopened.HexDiff.IsAvailable);
        Assert.False(reopened.HexDiff.HasCompleteDifferenceWorkspace);
        Assert.True(reopened.HexDiff.HasPreviewFallback);
        Assert.Contains("not attached", reopened.HexDiff.AvailabilityDetail, StringComparison.Ordinal);
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
        Assert.False(noDifferenceReport.HexDiff.HasCompleteDifferenceWorkspace);
        Assert.False(noDifferenceReport.HexDiff.HasPreviewFallback);
    }

    /// <summary>Full-byte inspection fails closed for every report/snapshot identity and range invariant.</summary>
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
            (
                "zero changed-byte count",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 4, 4, 0))),
            (
                "changed-byte count exceeds range",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 4, 4, 5))),
            (
                "inconsistent end-exclusive",
                ReportJsonSamples.ReplaceWithOutputDifferenceRanges(
                    runId,
                    result.OutputSize,
                    result.OutputSha256,
                    (0, 8, 4, 4))),
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
            Assert.True(report.HexDiff.HasPreviewFallback);
        }
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
        Assert.InRange(report.HexDiff.VisibleRows.Count, 1, 48);
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

        fragmented.HexDiff.JumpAddress = "0x9AB0";
        fragmented.HexDiff.JumpAddressCommand.Execute(null);
        ReportHexDiffRangeViewModel reviewSelection = Assert.IsType<ReportHexDiffRangeViewModel>(
            fragmented.HexDiff.PinnedSelectedRange);
        Assert.Same(fragmented.HexDiff.SelectedRange, reviewSelection);
        Assert.True(reviewSelection.IsReviewRequired);
        Assert.True(reviewSelection.IsSelected);
        Assert.DoesNotContain(reviewSelection, fragmented.HexDiff.NavigatorPage.Items);
        Assert.Equal(65, fragmented.HexDiff.VisibleNavigatorRowCount);
        Assert.Equal(1, CountSelectedNavigatorRows(fragmented.HexDiff));
        Assert.Equal(65, fragmented.HexDiff.MaterializedRangeCount);

        fragmented.HexDiff.JumpAddress = "0x8CA4";
        fragmented.HexDiff.JumpAddressCommand.Execute(null);
        ReportHexDiffRangeViewModel acceptedSelection = Assert.IsType<ReportHexDiffRangeViewModel>(
            fragmented.HexDiff.PinnedSelectedRange);
        Assert.Same(fragmented.HexDiff.SelectedRange, acceptedSelection);
        Assert.False(acceptedSelection.IsReviewRequired);
        Assert.True(acceptedSelection.IsSelected);
        Assert.False(reviewSelection.IsSelected);
        Assert.DoesNotContain(acceptedSelection, fragmented.HexDiff.NavigatorPage.Items);
        Assert.Equal(65, fragmented.HexDiff.VisibleNavigatorRowCount);
        Assert.Equal(1, CountSelectedNavigatorRows(fragmented.HexDiff));
        Assert.Equal(65, fragmented.HexDiff.MaterializedRangeCount);

        fragmented.HexDiff.NavigatorPage.NextPageCommand.Execute(null);
        Assert.Equal(1, fragmented.HexDiff.NavigatorPage.PageIndex);
        Assert.Equal(64, fragmented.HexDiff.NavigatorPage.VisibleCount);
        Assert.Same(acceptedSelection, fragmented.HexDiff.PinnedSelectedRange);
        Assert.Equal(65, fragmented.HexDiff.VisibleNavigatorRowCount);
        Assert.Equal(65, fragmented.HexDiff.MaterializedRangeCount);
        Assert.Equal(1, CountSelectedNavigatorRows(fragmented.HexDiff));

        ReportHexDiffRangeViewModel visiblePageSelection = Assert.IsType<ReportHexDiffRangeViewModel>(
            fragmented.HexDiff.NavigatorPage.Items[0]);
        fragmented.HexDiff.SelectRangeCommand.Execute(visiblePageSelection);
        Assert.Same(visiblePageSelection, fragmented.HexDiff.SelectedRange);
        Assert.Null(fragmented.HexDiff.PinnedSelectedRange);
        Assert.False(acceptedSelection.IsSelected);
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
