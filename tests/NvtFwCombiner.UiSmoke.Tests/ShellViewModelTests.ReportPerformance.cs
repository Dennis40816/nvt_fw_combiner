using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Large reports retain complete evidence while initially exposing bounded UI pages.</summary>
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
        Assert.Equal(sectionCount, viewModel.LoadedReport.OutputDifferenceGroups.Count);
        Assert.Equal(8, viewModel.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(8, viewModel.LoadedReport.OutputDifferenceSummaryPage.VisibleCount);
        Assert.True(viewModel.LoadedReport.OutputDifferenceGroupPage.HasMoreItems);

        ReportDifferenceGroupViewModel firstGroup = Assert.IsType<ReportDifferenceGroupViewModel>(
            viewModel.LoadedReport.OutputDifferenceGroupPage.Items[0]);
        Assert.Equal("Section 00", firstGroup.Title);
        Assert.False(firstGroup.IsReviewRequired);
        Assert.Equal(25, firstGroup.RowsPage.TotalCount);
        Assert.Equal(24, firstGroup.RowsPage.VisibleCount);
        ReportLineViewModel firstDifference = Assert.IsType<ReportLineViewModel>(firstGroup.RowsPage.Items[0]);
        Assert.Equal("diff-00000", firstDifference.Title);
        Assert.True(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        firstGroup.RowsPage.LoadMoreCommand.Execute(null);
        Assert.Equal(25, firstGroup.RowsPage.VisibleCount);
        Assert.False(firstGroup.RowsPage.HasMoreItems);
        Assert.False(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        Assert.Equal("Showing 25/25", firstGroup.RowsPage.PageStatus);
        Assert.Equal("All items loaded", firstGroup.RowsPage.LoadMoreLabel);

        viewModel.LoadedReport.OutputDifferenceGroupPage.LoadMoreCommand.Execute(null);
        Assert.Equal(16, viewModel.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(differenceCount, viewModel.LoadedReport.OutputDifferenceCount);
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

    /// <summary>A language change during background projection cannot publish a stale-language report.</summary>
    [Fact]
    public async Task ChangeReportProjectionReplaysWhenLanguageChangesInFlight()
    {
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 2_000, sectionCount: 40);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Task loading = viewModel.LoadReportJsonAsync(
            json,
            "language-race-report.json",
            TestContext.Current.CancellationToken);
        viewModel.SelectedLanguage = "Traditional Chinese";
        await loading;

        Assert.Equal(ShellLanguage.ChineseTraditional, viewModel.Text.Language);
        ReportLineViewModel firstDifference = viewModel.LoadedReport.OutputDifferences[0];
        Assert.Contains(firstDifference.Badges, badge => badge.Text == "預期");
        Assert.Equal("已顯示 8/40 筆", viewModel.LoadedReport.OutputDifferenceGroupPage.PageStatus);
        Assert.Equal("再載入 8 筆（尚餘 32 筆）", viewModel.LoadedReport.OutputDifferenceGroupPage.LoadMoreLabel);
        for (int index = 0; index < 4; index++)
        {
            viewModel.LoadedReport.OutputDifferenceGroupPage.LoadMoreCommand.Execute(null);
        }

        Assert.Equal("已顯示 40/40 筆", viewModel.LoadedReport.OutputDifferenceGroupPage.PageStatus);
        Assert.Equal("已載入全部項目", viewModel.LoadedReport.OutputDifferenceGroupPage.LoadMoreLabel);
        Assert.Equal(json, viewModel.LoadedReportJson);
    }

    /// <summary>A slower earlier load cannot overwrite a newer report or append stale history.</summary>
    [Fact]
    public async Task ChangeReportProjectionUsesLatestLoadGeneration()
    {
        string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 10_000, sectionCount: 40);
        string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-report");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Task olderLoad = viewModel.LoadReportJsonAsync(
            olderJson,
            "older-large-report.json",
            TestContext.Current.CancellationToken);
        viewModel.LoadReportJson(newerJson, "newer-report.json");
        await olderLoad;

        Assert.Equal("newer-report.json", viewModel.LoadedReport.SourceName);
        Assert.Equal(newerJson, viewModel.LoadedReportJson);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.ReportHistoryEntries);
        Assert.Equal("newer-report.json", historyEntry.SourceName);
    }

    /// <summary>Relocalizing an existing report does not cancel a newer in-flight load.</summary>
    [Fact]
    public async Task LanguageChangeKeepsNewerInFlightReportGeneration()
    {
        string oldJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "old-report");
        string newJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 10_000, sectionCount: 40);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.LoadReportJson(oldJson, "old-report.json");

        Task newLoad = viewModel.LoadReportJsonAsync(
            newJson,
            "new-large-report.json",
            TestContext.Current.CancellationToken);
        viewModel.SelectedLanguage = "Traditional Chinese";
        await newLoad;

        Assert.Equal("new-large-report.json", viewModel.LoadedReport.SourceName);
        Assert.Equal(newJson, viewModel.LoadedReportJson);
        Assert.Contains(viewModel.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "預期");
        Assert.Equal("new-large-report.json", viewModel.ReportHistoryEntries[0].SourceName);
    }
}
