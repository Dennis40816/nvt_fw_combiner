using System.Text;
using System.Text.Json;
using NvtFwCombiner.Bootstrap;
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
        string[] expectedSectionOrder =
        [
            .. Enumerable.Range(0, sectionCount).Select(index => $"Section {index:D2}"),
        ];
        Assert.Equal(expectedSectionOrder, viewModel.LoadedReport.OutputDifferenceGroups.Select(group => group.Title));
        Assert.Equal(expectedSectionOrder, viewModel.LoadedReport.OutputDifferenceSummaryRows.Select(row => row.Label));
        Assert.False(viewModel.LoadedReport.OutputDifferenceGroups[0].IsReviewRequired);
        Assert.True(viewModel.LoadedReport.OutputDifferenceGroups[^1].IsReviewRequired);
        Assert.Equal("expected", viewModel.LoadedReport.OutputDifferenceSummaryRows[0].Status);
        Assert.Equal("review", viewModel.LoadedReport.OutputDifferenceSummaryRows[^1].Status);
        Assert.Equal(8, viewModel.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(8, viewModel.LoadedReport.OutputDifferenceSummaryPage.VisibleCount);
        Assert.True(viewModel.LoadedReport.OutputDifferenceGroupPage.HasMoreItems);

        ReportDifferenceGroupViewModel firstGroup = Assert.IsType<ReportDifferenceGroupViewModel>(
            viewModel.LoadedReport.OutputDifferenceGroupPage.Items[0]);
        Assert.Equal("Section 00", firstGroup.Title);
        Assert.False(firstGroup.IsReviewRequired);
        Assert.False(firstGroup.IsExpanded);
        Assert.Equal(25, firstGroup.RowsPage.TotalCount);
        Assert.Equal(0, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(0, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        firstGroup.IsExpanded = true;
        Assert.Equal(24, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(24, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        ReportLineViewModel firstDifference = Assert.IsType<ReportLineViewModel>(firstGroup.RowsPage.Items[0]);
        Assert.Equal("diff-00000", firstDifference.Title);
        Assert.Same(firstDifference, firstGroup.Rows[0]);
        Assert.Same(firstDifference, viewModel.LoadedReport.OutputDifferences[0]);
        Assert.Equal(24, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        firstGroup.IsExpanded = false;
        firstGroup.IsExpanded = true;
        Assert.Equal(24, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(24, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.True(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        firstGroup.RowsPage.LoadMoreCommand.Execute(null);
        Assert.Equal(25, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(25, viewModel.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.False(firstGroup.RowsPage.HasMoreItems);
        Assert.False(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        Assert.Equal("Showing 25/25", firstGroup.RowsPage.PageStatus);
        Assert.Equal("All items loaded", firstGroup.RowsPage.LoadMoreLabel);

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

    /// <summary>Lazy byte slices preserve escaped labels after the summary parser releases its JSON document.</summary>
    [Fact]
    public void LazyDifferenceSlicePreservesEscapedSectionText()
    {
        const string sectionPrefix = "觸控 / \"CRC\" 欄位";
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(
            count: 8,
            sectionCount: 4,
            sectionPrefix);

        ReportReviewViewModel report = ReportReviewViewModel.FromJson(json, "escaped-section.json");

        Assert.Equal(0, report.MaterializedOutputDifferenceCount);
        ReportDifferenceGroupViewModel firstGroup = report.OutputDifferenceGroups[0];
        Assert.Equal($"{sectionPrefix} 00", firstGroup.Title);
        firstGroup.IsExpanded = true;
        ReportLineViewModel firstRow = Assert.IsType<ReportLineViewModel>(firstGroup.RowsPage.Items[0]);
        Assert.Equal($"{sectionPrefix} 00", firstRow.SectionLabel);
        Assert.Same(firstRow, report.OutputDifferences[0]);
    }

    /// <summary>The wire index follows root-property semantics and keeps complete nested array entries.</summary>
    [Fact]
    public void DifferenceSliceIndexUsesLastTopLevelPropertyAndExactEntryBounds()
    {
        const string firstEntry = "{\"DifferenceId\":\"first\",\"Classification\":\"DeclaredReplacement\",\"IsAccepted\":true,\"SectionLabel\":\"First\"}";
        const string lastEntryOne = "{\"DifferenceId\":\"last-1\",\"Classification\":\"DeclaredReplacement\",\"IsAccepted\":true,\"SectionLabel\":\"Last\",\"Nested\":{\"Values\":[1,{\"Value\":2}]}}";
        const string lastEntryTwo = "{\"DifferenceId\":\"last-2\",\"Classification\":\"Unexpected\",\"IsAccepted\":false,\"SectionLabel\":\"Review\",\"Nested\":[{\"Values\":[3,4]},5]}";
        string json = $$"""
            {
              "Container": { "OutputDifferences": [{{firstEntry}}] },
              "OutputDifferences": [{{firstEntry}}],
              "OutputDifferences": [{{lastEntryOne}},{{lastEntryTwo}}]
            }
            """;
        byte[] utf8 = Encoding.UTF8.GetBytes(json);

        ReportReviewViewModel.JsonValueSlice[] slices =
            ReportReviewViewModel.IndexOutputDifferences(utf8, CancellationToken.None);
        ReportReviewViewModel report = ReportReviewViewModel.FromJson(json, "wire-contract.json");

        Assert.Equal(2, slices.Length);
        Assert.Equal(lastEntryOne, Encoding.UTF8.GetString(utf8, slices[0].Start, slices[0].Length));
        Assert.Equal(lastEntryTwo, Encoding.UTF8.GetString(utf8, slices[1].Start, slices[1].Length));
        Assert.Equal(["last-1", "last-2"], report.OutputDifferences.Select(row => row.Title));
        Assert.Equal(["Last", "Review"], report.OutputDifferenceGroups.Select(group => group.Title));
    }

    /// <summary>A compound value cannot bypass cancellation after its opening token was consumed.</summary>
    [Fact]
    public void DifferenceSliceCompoundValueSkipObservesCancellationAfterEntry()
    {
        _ = Assert.ThrowsAny<OperationCanceledException>(SkipEnteredCompoundValueWithCancellation);
    }

    private static void SkipEnteredCompoundValueWithCancellation()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("{\"Nested\":[{\"Values\":[0,1,2]}]}");
        var reader = new Utf8JsonReader(utf8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        ReportReviewViewModel.SkipJsonValue(ref reader, cancellationSource.Token);
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

    /// <summary>A cancelled run projection cannot publish its verified in-session Hex Diff snapshot.</summary>
    [Fact]
    public async Task CancelledRunHexDiffProjectionPublishesNoPartialState()
    {
        WorkbenchRunResult result = await CreateGeneralReplaceInspectionResultAsync();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            viewModel.ProjectAndApplyRunResultAsync(result, build: false, cancellationSource.Token));

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

    /// <summary>A stale run projection cannot publish a verified Hex Diff or append report history.</summary>
    [Fact]
    public async Task RunHexDiffProjectionUsesLatestReportGeneration()
    {
        WorkbenchRunResult result = await CreateGeneralReplaceInspectionResultAsync();
        using var source = JsonDocument.Parse(result.ReportJson);
        string runId = source.RootElement.GetProperty("RunId").GetString()!;
        WorkbenchRunResult largeResult = result with
        {
            ReportJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(
                count: 10_000,
                sectionCount: 40,
                runId: runId,
                outputSize: result.OutputSize,
                outputSha256: result.OutputSha256),
        };
        string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-report");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Task olderProjection = viewModel.ProjectAndApplyRunResultAsync(
            largeResult,
            build: false,
            TestContext.Current.CancellationToken);
        viewModel.LoadReportJson(newerJson, "newer-report.json");
        await olderProjection;

        Assert.Equal("newer-report.json", viewModel.LoadedReport.SourceName);
        Assert.Equal(newerJson, viewModel.LoadedReportJson);
        Assert.False(viewModel.LoadedReport.HexDiff.HasCompleteDifferenceWorkspace);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.ReportHistoryEntries);
        Assert.Equal("newer-report.json", historyEntry.SourceName);
    }

    /// <summary>A large history reopen cannot overwrite a newer report and never records another run.</summary>
    [Fact]
    public async Task ChangeReportHistoryReopenUsesLatestProjectionGeneration()
    {
        string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 10_000, sectionCount: 40);
        string currentJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "current-report");
        string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-after-history");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.LoadReportJson(olderJson, "older-history-report.json");
        ReportHistoryEntryViewModel olderEntry = viewModel.ReportHistoryEntries[0];
        viewModel.LoadReportJson(currentJson, "current-report.json");

        Task reopening = viewModel.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(olderEntry);
        viewModel.LoadReportJson(newerJson, "newer-after-history.json");
        await reopening;

        Assert.Equal("newer-after-history.json", viewModel.LoadedReport.SourceName);
        Assert.Equal(newerJson, viewModel.LoadedReportJson);
        Assert.Equal(3, viewModel.ReportHistoryCount);
        Assert.Equal("newer-after-history.json", viewModel.ReportHistoryEntries[0].SourceName);
    }

    /// <summary>Explicit history navigation and cleanup choices cancel an in-flight reopen.</summary>
    [Theory]
    [InlineData("cancel")]
    [InlineData("close")]
    [InlineData("back")]
    [InlineData("clear")]
    [InlineData("remove")]
    public async Task ChangeReportHistoryReopenHonorsInFlightUserAction(string action)
    {
        using var uiThread = new UiThreadTestContext();
        await uiThread.InvokeAsync(async () =>
        {
            string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 5_000, sectionCount: 40);
            string currentJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "current-before-action");
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.LoadReportJson(olderJson, "older-action-report.json");
            ReportHistoryEntryViewModel olderEntry = viewModel.ReportHistoryEntries[0];
            viewModel.LoadReportJson(currentJson, "current-before-action.json");
            viewModel.ShowReportHistoryCommand.Execute(null);

            int legacyCanExecuteNotifications = 0;
            viewModel.OpenReportHistoryEntryCommand.CanExecuteChanged += (_, _) => legacyCanExecuteNotifications++;
            viewModel.OpenReportHistoryEntryAsyncCommand.Execute(olderEntry);
            Task reopening = Assert.IsType<Task>(
                viewModel.OpenReportHistoryEntryAsyncCommand.ExecutionTask,
                exactMatch: false);
            Assert.True(viewModel.OpenReportHistoryEntryAsyncCommand.IsRunning);
            Assert.False(viewModel.OpenReportHistoryEntryCommand.CanExecute(olderEntry));
            switch (action)
            {
                case "cancel":
                    viewModel.OpenReportHistoryEntryAsyncCommand.Cancel();
                    break;
                case "close":
                    viewModel.CloseReportCommand.Execute(null);
                    break;
                case "back":
                    viewModel.CloseReportHistoryCommand.Execute(null);
                    break;
                case "clear":
                    viewModel.ClearReportHistoryCommand.Execute(null);
                    break;
                case "remove":
                    viewModel.RemoveReportHistoryEntryCommand.Execute(olderEntry);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown test action '{action}'.");
            }

            await reopening;

            Assert.False(viewModel.OpenReportHistoryEntryAsyncCommand.IsRunning);
            Assert.True(viewModel.OpenReportHistoryEntryCommand.CanExecute(olderEntry));
            Assert.True(legacyCanExecuteNotifications >= 2);
            Assert.Equal("current-before-action.json", viewModel.LoadedReport.SourceName);
            Assert.Equal(currentJson, viewModel.LoadedReportJson);
            Assert.False(viewModel.HasReportToast);
            if (action == "close")
            {
                Assert.False(viewModel.IsReportModalOpen);
            }
            else if (action == "back")
            {
                Assert.True(viewModel.IsReportModalOpen);
                Assert.False(viewModel.IsReportHistoryViewOpen);
            }
            else if (action == "clear")
            {
                Assert.Empty(viewModel.ReportHistoryEntries);
            }
            else if (action == "remove")
            {
                Assert.DoesNotContain(olderEntry, viewModel.ReportHistoryEntries);
            }
        });
    }

    /// <summary>A language change during history projection publishes only the stable language.</summary>
    [Fact]
    public async Task ChangeReportHistoryReopenReplaysLanguageDrift()
    {
        using var uiThread = new UiThreadTestContext();
        await uiThread.InvokeAsync(async () =>
        {
            string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 5_000, sectionCount: 40);
            string currentJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "current-language");
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.LoadReportJson(olderJson, "older-language-report.json");
            ReportHistoryEntryViewModel olderEntry = viewModel.ReportHistoryEntries[0];
            viewModel.LoadReportJson(currentJson, "current-language-report.json");

            Task reopening = viewModel.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(olderEntry);
            Assert.True(viewModel.OpenReportHistoryEntryAsyncCommand.IsRunning);
            viewModel.SelectedLanguage = "Traditional Chinese";
            await reopening;

            Assert.Equal("older-language-report.json", viewModel.LoadedReport.SourceName);
            Assert.Contains(viewModel.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "預期");
            Assert.Equal("已顯示 8/40 筆", viewModel.LoadedReport.OutputDifferenceGroupPage.PageStatus);
            Assert.Equal(2, viewModel.ReportHistoryCount);
        });
    }

    /// <summary>Language selection returns before a large report is atomically relocalized.</summary>
    [Fact]
    public async Task ChangeReportRelocalizationRunsOffDispatcherAndPublishesAtomically()
    {
        using var uiThread = new UiThreadTestContext();
        await uiThread.InvokeAsync(async () =>
        {
            string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 5_000, sectionCount: 40);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.LoadReportJson(json, "large-language-report.json");

            viewModel.SelectedLanguage = "Traditional Chinese";
            Task relocalization = Assert.IsType<Task>(viewModel.ReportRelocalizationTask, exactMatch: false);

            Assert.True(viewModel.IsReportRelocalizationRunning);
            Assert.Contains(viewModel.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "expected");
            await relocalization;

            Assert.False(viewModel.IsReportRelocalizationRunning);
            Assert.Contains(viewModel.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "預期");
            Assert.Equal("已顯示 8/40 筆", viewModel.LoadedReport.OutputDifferenceGroupPage.PageStatus);
            Assert.Equal(json, viewModel.LoadedReportJson);
            Assert.Equal(1, viewModel.ReportHistoryCount);
        });
    }

    /// <summary>A newer report cancels relocalization and remains the only published review.</summary>
    [Fact]
    public async Task ChangeReportRelocalizationCannotOverwriteNewerReport()
    {
        using var uiThread = new UiThreadTestContext();
        await uiThread.InvokeAsync(async () =>
        {
            string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 5_000, sectionCount: 40);
            string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-language-report");
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.LoadReportJson(olderJson, "older-language-report.json");

            viewModel.SelectedLanguage = "Traditional Chinese";
            Task relocalization = Assert.IsType<Task>(viewModel.ReportRelocalizationTask, exactMatch: false);
            Assert.True(viewModel.IsReportRelocalizationRunning);
            viewModel.LoadReportJson(newerJson, "newer-language-report.json");
            Assert.True(viewModel.IsReportRelocalizationRunning);
            viewModel.SelectedLanguage = "English";
            await relocalization;

            Assert.Equal("newer-language-report.json", viewModel.LoadedReport.SourceName);
            Assert.Equal(newerJson, viewModel.LoadedReportJson);
            Assert.Contains(viewModel.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "expected");
            Assert.Equal(2, viewModel.ReportHistoryCount);
        });
    }

    /// <summary>Rapid language requests coalesce into one cancellable command execution.</summary>
    [Fact]
    public async Task ChangeReportRelocalizationCoalescesRapidLanguageRequests()
    {
        using var uiThread = new UiThreadTestContext();
        await uiThread.InvokeAsync(async () =>
        {
            string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 5_000, sectionCount: 40);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.LoadReportJson(json, "rapid-language-report.json");

            viewModel.SelectedLanguage = "Traditional Chinese";
            Task firstRequest = Assert.IsType<Task>(viewModel.ReportRelocalizationTask, exactMatch: false);
            Assert.True(viewModel.IsReportRelocalizationRunning);
            viewModel.SelectedLanguage = "English";
            Task latestRequest = Assert.IsType<Task>(viewModel.ReportRelocalizationTask, exactMatch: false);

            Assert.Same(firstRequest, latestRequest);
            await latestRequest;

            Assert.False(viewModel.IsReportRelocalizationRunning);
            Assert.Contains(viewModel.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "expected");
            Assert.Equal("Showing 8/40", viewModel.LoadedReport.OutputDifferenceGroupPage.PageStatus);
            Assert.Equal(json, viewModel.LoadedReportJson);
            Assert.Equal(1, viewModel.ReportHistoryCount);
        });
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
