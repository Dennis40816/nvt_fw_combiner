using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Large reports retain complete evidence while deferring unexpanded difference row models.</summary>
    [Fact]
    public async Task LargeChangeReportUsesBoundedPagesAndKeepsCompleteJson()
    {
        const int differenceCount = 1_000;
        const int sectionCount = 40;
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(differenceCount, sectionCount);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        await viewModel.LoadReportJsonAsync(
            json,
            "large-report.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(json, viewModel.LoadedReportJson);
        Assert.Equal(differenceCount, viewModel.LoadedReport.OutputDifferenceCount);
        Assert.Equal(differenceCount, viewModel.LoadedReport.OutputDifferences.Count);
        Assert.Equal(0, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.Equal(sectionCount, viewModel.LoadedReport.OutputDifferenceGroups.Count);
        Assert.Equal(8, viewModel.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(8, viewModel.LoadedReport.OutputDifferenceSummaryPage.VisibleCount);
        Assert.True(viewModel.LoadedReport.OutputDifferenceGroupPage.HasMoreItems);

        ReportDifferenceGroupViewModel firstGroup = Assert.IsType<ReportDifferenceGroupViewModel>(
            viewModel.LoadedReport.OutputDifferenceGroupPage.Items[0]);
        Assert.True(firstGroup.IsReviewRequired);
        Assert.False(firstGroup.IsExpanded);
        Assert.Equal(25, firstGroup.RowsPage.TotalCount);
        Assert.Equal(0, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(0, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        firstGroup.IsExpanded = true;
        Assert.Equal(24, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(24, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        ReportLineViewModel firstReviewRow = Assert.IsType<ReportLineViewModel>(firstGroup.RowsPage.Items[0]);
        Assert.Equal("diff-00999", firstReviewRow.Title);
        Assert.Same(firstReviewRow, viewModel.LoadedReport.OutputDifferences[999]);
        Assert.Equal(24, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.True(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        firstGroup.RowsPage.LoadMoreCommand.Execute(null);
        Assert.Equal(25, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(25, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.False(firstGroup.RowsPage.HasMoreItems);

        viewModel.LoadedReport.OutputDifferenceGroupPage.LoadMoreCommand.Execute(null);
        Assert.Equal(16, viewModel.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(differenceCount, viewModel.LoadedReport.OutputDifferenceCount);
        Assert.Equal(25, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
    }

    /// <summary>Legacy full-hex fields render only a bounded preview while raw report JSON stays complete.</summary>
    [Fact]
    public void LargeChangeReportBoundsLegacyFullHexDisplay()
    {
        string json = ReportJsonSamples.ReplaceWithFullHexOutputDifference(byteCount: 4_096);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "legacy-full-hex.json");

        ReportLineViewModel difference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Equal("Before preview, first 64 bytes", difference.BeforeLabel);
        Assert.Equal("After preview, first 64 bytes", difference.AfterLabel);
        Assert.Equal(64, difference.BeforeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(64, difference.AfterValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(json, viewModel.LoadedReportJson);
        Assert.Contains(new string('A', 512), viewModel.LoadedReportJson, StringComparison.Ordinal);
    }

    /// <summary>Cancelled background projection does not publish a partial report or history entry.</summary>
    [Fact]
    public async Task CancelledChangeReportProjectionPublishesNoPartialState()
    {
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 1_000, sectionCount: 40);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            viewModel.LoadReportJsonAsync(json, "cancelled-report.json", cancellationSource.Token));

        Assert.False(viewModel.HasLoadedReport);
        Assert.False(viewModel.HasReportHistory);
        Assert.Equal(string.Empty, viewModel.LoadedReportJson);
    }
}
