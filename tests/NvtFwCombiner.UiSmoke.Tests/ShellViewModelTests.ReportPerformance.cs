using System.Text;
using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Live typed reports and reopened JSON reports project the same review evidence.</summary>
    [Fact]
    public async Task LiveTypedReportProjectionMatchesPersistedJsonProjection()
    {
        CompositionRunResult result = await CreateDpReplaceInspectionResultAsync();
        string json = CompositionRunReportJson.Serialize(result);
        var persisted = ReportReviewViewModel.FromJsonCancellable(
            json,
            "persisted report",
            result.CommittedOutputId,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);
        var live = ReportReviewViewModel.FromReportCancellable(
            result.Report,
            suppressOutput: false,
            "live report",
            result.CommittedOutputId,
            result.InspectionSnapshot,
            ShellLanguage.English,
            TestContext.Current.CancellationToken);

        Assert.Equal(persisted.ProfileId, live.ProfileId);
        Assert.Equal(persisted.IcId, live.IcId);
        Assert.Equal(persisted.ModeId, live.ModeId);
        Assert.Equal(persisted.ExperienceId, live.ExperienceId);
        Assert.Equal(persisted.CompositionKind, live.CompositionKind);
        Assert.Equal(persisted.RunId, live.RunId);
        Assert.Equal(persisted.StartedAtUtc, live.StartedAtUtc);
        Assert.Equal(persisted.Status, live.Status);
        Assert.Equal(persisted.Output, live.Output);
        Assert.Equal(persisted.OutputFileName, live.OutputFileName);
        Assert.Equal(persisted.OutputSize, live.OutputSize);
        Assert.Equal(persisted.OutputCommitmentLabel, live.OutputCommitmentLabel);
        Assert.Equal(persisted.OutputSha256, live.OutputSha256);
        Assert.Equal(persisted.Inputs.Select(ToReportLineSignature), live.Inputs.Select(ToReportLineSignature));
        Assert.Equal(persisted.Operations.Select(ToReportLineSignature), live.Operations.Select(ToReportLineSignature));
        Assert.Equal(persisted.Mutations.Select(ToReportLineSignature), live.Mutations.Select(ToReportLineSignature));
        Assert.Equal(persisted.Issues.Select(ToReportLineSignature), live.Issues.Select(ToReportLineSignature));
        Assert.Equal(
            persisted.OutputDifferenceGroups.Select(static group => (group.Title, group.Detail, group.Status)),
            live.OutputDifferenceGroups.Select(static group => (group.Title, group.Detail, group.Status)));
        Assert.Equal(
            persisted.OutputDifferenceSummaryRows.Select(static row => (row.Label, row.Count, row.Status, row.Detail)),
            live.OutputDifferenceSummaryRows.Select(static row => (row.Label, row.Count, row.Status, row.Detail)));
        Assert.Equal(0, live.MaterializedOutputDifferenceCount);
        Assert.Equal(
            persisted.OutputDifferences.Select(ToReportLineSignature),
            live.OutputDifferences.Select(ToReportLineSignature));
        Assert.Equal(persisted.HexDiff.HasDifferenceWorkspace, live.HexDiff.HasDifferenceWorkspace);
        Assert.Equal(persisted.HexDiff.TotalRowCount, live.HexDiff.TotalRowCount);
        Assert.Equal(persisted.HexDiff.TotalByteCount, live.HexDiff.TotalByteCount);
    }

    private static string ToReportLineSignature(ReportLineViewModel line)
    {
        return JsonSerializer.Serialize(new
        {
            line.Title,
            line.Detail,
            line.Meta,
            line.CodeBlock,
            line.CodeBlockLabel,
            line.OperationKind,
            line.OperationSource,
            line.OperationTarget,
            line.OperationProcessor,
            line.OperationStatus,
            line.Severity,
            line.Classification,
            line.IsAccepted,
            line.Range,
            line.ChangedSummary,
            line.Reason,
            line.SectionLabel,
            line.BeforeLabel,
            line.BeforeValue,
            line.AfterLabel,
            line.AfterValue,
            line.InputRole,
            line.InputSizeLabel,
            line.InputAddressSpace,
            Badges = line.Badges.Select(static badge => badge.Text),
            Facts = line.Facts.Select(static fact => new { fact.Label, fact.Value, fact.IsTechnical }),
            Ranges = line.RangeRows.Select(static row => new { row.Kind, row.AddressSpace, row.Range, row.Source }),
            Commands = line.RuntimeCommands.Select(static command => new
            {
                command.Title,
                command.ArgumentListEvidence,
                command.WorkingDirectoryDetail,
            }),
        });
    }

    /// <summary>Large reports retain complete evidence while deferring unexpanded difference row models.</summary>
    [Fact]
    public async Task LargeChangeReportUsesBoundedPagesAndKeepsCompleteJson()
    {
        const int differenceCount = 1_000;
        const int sectionCount = 40;
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(differenceCount, sectionCount);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        await viewModel.Reports.LoadReportJsonAsync(
            json,
            "large-report.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(json, viewModel.Reports.LoadedReportJson);
        Assert.Equal(differenceCount, viewModel.Reports.LoadedReport.OutputDifferenceCount);
        Assert.Equal(differenceCount, viewModel.Reports.LoadedReport.OutputDifferences.Count);
        Assert.Equal(0, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.Equal(sectionCount, viewModel.Reports.LoadedReport.OutputDifferenceGroups.Count);
        string[] expectedSectionOrder =
        [
            .. Enumerable.Range(0, sectionCount).Select(index => $"Section {index:D2}"),
        ];
        Assert.Equal(expectedSectionOrder, viewModel.Reports.LoadedReport.OutputDifferenceGroups.Select(group => group.Title));
        Assert.Equal(expectedSectionOrder, viewModel.Reports.LoadedReport.OutputDifferenceSummaryRows.Select(row => row.Label));
        Assert.False(viewModel.Reports.LoadedReport.OutputDifferenceGroups[0].IsReviewRequired);
        Assert.True(viewModel.Reports.LoadedReport.OutputDifferenceGroups[^1].IsReviewRequired);
        Assert.Equal("expected", viewModel.Reports.LoadedReport.OutputDifferenceSummaryRows[0].Status);
        Assert.Equal("review", viewModel.Reports.LoadedReport.OutputDifferenceSummaryRows[^1].Status);
        Assert.Equal(8, viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(8, viewModel.Reports.LoadedReport.OutputDifferenceSummaryPage.VisibleCount);
        Assert.True(viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.HasMoreItems);

        ReportDifferenceGroupViewModel firstGroup = Assert.IsType<ReportDifferenceGroupViewModel>(
            viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.Items[0]);
        Assert.Equal("Section 00", firstGroup.Title);
        Assert.False(firstGroup.IsReviewRequired);
        Assert.False(firstGroup.IsExpanded);
        Assert.Equal(25, firstGroup.RowsPage.TotalCount);
        Assert.Equal(0, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(0, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
        firstGroup.IsExpanded = true;
        Assert.Equal(24, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(24, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
        ReportLineViewModel firstDifference = Assert.IsType<ReportLineViewModel>(firstGroup.RowsPage.Items[0]);
        Assert.Equal("diff-00000", firstDifference.Title);
        Assert.Same(firstDifference, firstGroup.Rows[0]);
        Assert.Same(firstDifference, viewModel.Reports.LoadedReport.OutputDifferences[0]);
        Assert.Equal(24, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
        firstGroup.IsExpanded = false;
        firstGroup.IsExpanded = true;
        Assert.Equal(24, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(24, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.True(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        firstGroup.RowsPage.LoadMoreCommand.Execute(null);
        Assert.Equal(25, firstGroup.RowsPage.VisibleCount);
        Assert.Equal(25, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
        Assert.False(firstGroup.RowsPage.HasMoreItems);
        Assert.False(firstGroup.RowsPage.LoadMoreCommand.CanExecute(null));
        Assert.Equal("Showing 25/25", firstGroup.RowsPage.PageStatus);
        Assert.Equal("All items loaded", firstGroup.RowsPage.LoadMoreLabel);

        viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.LoadMoreCommand.Execute(null);
        Assert.Equal(16, viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.VisibleCount);
        Assert.Equal(differenceCount, viewModel.Reports.LoadedReport.OutputDifferenceCount);
        Assert.Equal(25, viewModel.Reports.LoadedReport.MaterializedOutputDifferenceCount);
    }

    /// <summary>Legacy full-hex fields render only a bounded preview while raw report JSON stays complete.</summary>
    [Fact]
    public void LargeChangeReportBoundsLegacyFullHexDisplay()
    {
        string json = ReportJsonSamples.ReplaceWithFullHexOutputDifference(byteCount: 4_096);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.Reports.LoadReportJson(json, "legacy-full-hex.json");

        ReportLineViewModel difference = Assert.Single(viewModel.Reports.LoadedReport.OutputDifferences);
        Assert.Equal("Before preview, first 64 bytes", difference.BeforeLabel);
        Assert.Equal("After preview, first 64 bytes", difference.AfterLabel);
        Assert.Equal(64, difference.BeforeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(64, difference.AfterValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(json, viewModel.Reports.LoadedReportJson);
        Assert.Contains(new string('A', 512), viewModel.Reports.LoadedReportJson, StringComparison.Ordinal);
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

        var report = ReportReviewViewModel.FromJson(json, "escaped-section.json");

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
        const string firstEntry = /*lang=json,strict*/ "{\"DifferenceId\":\"first\",\"Classification\":\"DeclaredReplacement\",\"IsAccepted\":true,\"SectionLabel\":\"First\"}";
        const string lastEntryOne = /*lang=json,strict*/ "{\"DifferenceId\":\"last-1\",\"Classification\":\"DeclaredReplacement\",\"IsAccepted\":true,\"SectionLabel\":\"最後🚀\",\"Nested\":{\"Values\":[1,{\"Value\":2}]}}";
        const string lastEntryTwo = /*lang=json,strict*/ "{\"DifferenceId\":\"last-2\",\"Classification\":\"Unexpected\",\"IsAccepted\":false,\"SectionLabel\":\"審查😀\",\"Nested\":[{\"Values\":[3,4]},5]}";
        string json = $$"""
            {
              "Title": "前置🧭",
              "Container": { "OutputDifferences": [{{firstEntry}}] },
              "OutputDifferences": [{{firstEntry}}],
              "OutputDifferences": [{{lastEntryOne}},{{lastEntryTwo}}]
            }
            """;
        byte[] utf8 = Encoding.UTF8.GetBytes(json);

        ReportReviewViewModel.JsonValueSlice[] slices =
            ReportReviewViewModel.IndexOutputDifferences(utf8, CancellationToken.None);
        var report = ReportReviewViewModel.FromJson(json, "wire-contract.json");

        Assert.Equal(2, slices.Length);
        Assert.Equal(lastEntryOne, Encoding.UTF8.GetString(utf8, slices[0].Start, slices[0].Length));
        Assert.Equal(lastEntryTwo, Encoding.UTF8.GetString(utf8, slices[1].Start, slices[1].Length));
        Assert.True(json.AsSpan(slices[0].CharStart, slices[0].CharLength).SequenceEqual(lastEntryOne));
        Assert.True(json.AsSpan(slices[1].CharStart, slices[1].CharLength).SequenceEqual(lastEntryTwo));
        Assert.Equal(["last-1", "last-2"], report.OutputDifferences.Select(row => row.Title));
        Assert.Equal(["最後🚀", "審查😀"], report.OutputDifferenceGroups.Select(group => group.Title));
    }

    /// <summary>Malformed UTF-16 is rejected before a lazy difference model can be published.</summary>
    [Fact]
    public void DifferenceProjectionRejectsUnpairedUtf16BeforeLazyPublication()
    {
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 1, sectionCount: 1);
        string malformed = json.Replace("Section", "Section\uD800", StringComparison.Ordinal);

        _ = Assert.Throws<EncoderFallbackException>(() =>
            ReportReviewViewModel.FromJson(malformed, "unpaired-surrogate.json"));
    }

    /// <summary>A compound value cannot bypass cancellation after its opening token was consumed.</summary>
    [Fact]
    public void DifferenceSliceCompoundValueSkipObservesCancellationAfterEntry()
    {
        _ = Assert.ThrowsAny<OperationCanceledException>(SkipEnteredCompoundValueWithCancellation);
    }

    private static void SkipEnteredCompoundValueWithCancellation()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"Nested\":[{\"Values\":[0,1,2]}]}");
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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            viewModel.Reports.LoadReportJsonAsync(json, "cancelled-report.json", cancellationSource.Token));

        Assert.False(viewModel.Reports.HasLoadedReport);
        Assert.False(viewModel.Reports.HasReportHistory);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReportJson);
    }

    /// <summary>A cancelled run projection cannot publish its verified in-session Hex Diff snapshot.</summary>
    [Fact]
    public async Task CancelledRunHexDiffProjectionPublishesNoPartialState()
    {
        CompositionRunResult result = await CreateDpReplaceInspectionResultAsync();
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            viewModel.RunSession.ProjectAndApplyRunResultAsync(result, build: false, cancellationSource.Token));

        Assert.False(viewModel.Reports.HasLoadedReport);
        Assert.False(viewModel.Reports.HasReportHistory);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReportJson);
    }

    /// <summary>A language change during background projection cannot publish a stale-language report.</summary>
    [Fact]
    public async Task ChangeReportProjectionReplaysWhenLanguageChangesInFlight()
    {
        string json = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 2_000, sectionCount: 40);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Task loading = viewModel.Reports.LoadReportJsonAsync(
            json,
            "language-race-report.json",
            TestContext.Current.CancellationToken);
        viewModel.SelectedLanguage = "Traditional Chinese";
        await loading;

        Assert.Equal(ShellLanguage.ChineseTraditional, viewModel.Text.Language);
        ReportLineViewModel firstDifference = viewModel.Reports.LoadedReport.OutputDifferences[0];
        Assert.Contains(firstDifference.Badges, badge => badge.Text == "預期");
        Assert.Equal("已顯示 8/40 筆", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.PageStatus);
        Assert.Equal("再載入 8 筆（尚餘 32 筆）", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.LoadMoreLabel);
        for (int index = 0; index < 4; index++)
        {
            viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.LoadMoreCommand.Execute(null);
        }

        Assert.Equal("已顯示 40/40 筆", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.PageStatus);
        Assert.Equal("已載入全部項目", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.LoadMoreLabel);
        Assert.Equal(json, viewModel.Reports.LoadedReportJson);
    }

    /// <summary>A slower earlier load cannot overwrite a newer report or append stale history.</summary>
    [Fact]
    public async Task ChangeReportProjectionUsesLatestLoadGeneration()
    {
        string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 10_000, sectionCount: 40);
        string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-report");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Task olderLoad = viewModel.Reports.LoadReportJsonAsync(
            olderJson,
            "older-large-report.json",
            TestContext.Current.CancellationToken);
        viewModel.Reports.LoadReportJson(newerJson, "newer-report.json");
        await olderLoad;

        Assert.Equal("newer-report.json", viewModel.Reports.LoadedReport.SourceName);
        Assert.Equal(newerJson, viewModel.Reports.LoadedReportJson);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("newer-report.json", historyEntry.SourceName);
    }

    /// <summary>A stale run projection cannot publish a verified Hex Diff or append report history.</summary>
    [Fact]
    public async Task RunHexDiffProjectionUsesLatestReportGeneration()
    {
        CompositionRunResult result = await CreateDpReplaceInspectionResultAsync();
        using var source = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        string runId = source.RootElement.GetProperty("RunId").GetString()!;
        CompositionRunResult largeResult = WithReport(
            result,
            CreateLargeDifferenceReport(
                result.Report,
                count: 10_000,
                sectionCount: 40,
                runId: runId));
        string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-report");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Task olderProjection = viewModel.RunSession.ProjectAndApplyRunResultAsync(
            largeResult,
            build: false,
            TestContext.Current.CancellationToken);
        viewModel.Reports.LoadReportJson(newerJson, "newer-report.json");
        await olderProjection;

        Assert.Equal("newer-report.json", viewModel.Reports.LoadedReport.SourceName);
        Assert.Equal(newerJson, viewModel.Reports.LoadedReportJson);
        Assert.True(viewModel.Reports.LoadedReport.HexDiff.HasDifferenceWorkspace);
        Assert.False(viewModel.Reports.LoadedReport.HexDiff.IsReportedRangeMode);
        Assert.True(viewModel.Reports.LoadedReport.HexDiff.HasNoViewportBytes);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("newer-report.json", historyEntry.SourceName);
    }

    /// <summary>A large history reopen cannot overwrite a newer report and never records another run.</summary>
    [Fact]
    public async Task ChangeReportHistoryReopenUsesLatestProjectionGeneration()
    {
        string olderJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 10_000, sectionCount: 40);
        string currentJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "current-report");
        string newerJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "newer-after-history");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.Reports.LoadReportJson(olderJson, "older-history-report.json");
        ReportHistoryEntryViewModel olderEntry = viewModel.Reports.ReportHistoryEntries[0];
        viewModel.Reports.LoadReportJson(currentJson, "current-report.json");

        Task reopening = viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(olderEntry);
        viewModel.Reports.LoadReportJson(newerJson, "newer-after-history.json");
        await reopening;

        Assert.Equal("newer-after-history.json", viewModel.Reports.LoadedReport.SourceName);
        Assert.Equal(newerJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal(3, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("newer-after-history.json", viewModel.Reports.ReportHistoryEntries[0].SourceName);
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
            MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
            viewModel.Reports.LoadReportJson(olderJson, "older-action-report.json");
            ReportHistoryEntryViewModel olderEntry = viewModel.Reports.ReportHistoryEntries[0];
            viewModel.Reports.LoadReportJson(currentJson, "current-before-action.json");
            viewModel.Reports.ShowReportHistoryCommand.Execute(null);

            int legacyCanExecuteNotifications = 0;
            viewModel.Reports.OpenReportHistoryEntryCommand.CanExecuteChanged += (_, _) => legacyCanExecuteNotifications++;
            viewModel.Reports.OpenReportHistoryEntryAsyncCommand.Execute(olderEntry);
            Task reopening = Assert.IsType<Task>(
                viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecutionTask,
                exactMatch: false);
            Assert.True(viewModel.Reports.OpenReportHistoryEntryAsyncCommand.IsRunning);
            Assert.False(viewModel.Reports.OpenReportHistoryEntryCommand.CanExecute(olderEntry));
            switch (action)
            {
                case "cancel":
                    viewModel.Reports.OpenReportHistoryEntryAsyncCommand.Cancel();
                    break;
                case "close":
                    viewModel.Reports.CloseReportCommand.Execute(null);
                    break;
                case "back":
                    viewModel.Reports.CloseReportHistoryCommand.Execute(null);
                    break;
                case "clear":
                    viewModel.Reports.ClearReportHistoryCommand.Execute(null);
                    break;
                case "remove":
                    viewModel.Reports.RemoveReportHistoryEntryCommand.Execute(olderEntry);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown test action '{action}'.");
            }

            await reopening;

            Assert.False(viewModel.Reports.OpenReportHistoryEntryAsyncCommand.IsRunning);
            Assert.True(viewModel.Reports.OpenReportHistoryEntryCommand.CanExecute(olderEntry));
            Assert.True(legacyCanExecuteNotifications >= 2);
            Assert.Equal("current-before-action.json", viewModel.Reports.LoadedReport.SourceName);
            Assert.Equal(currentJson, viewModel.Reports.LoadedReportJson);
            Assert.False(viewModel.Reports.HasReportToast);
            if (action == "close")
            {
                Assert.False(viewModel.Reports.IsReportModalOpen);
            }
            else if (action == "back")
            {
                Assert.True(viewModel.Reports.IsReportModalOpen);
                Assert.False(viewModel.Reports.IsReportHistoryViewOpen);
            }
            else if (action == "clear")
            {
                Assert.Empty(viewModel.Reports.ReportHistoryEntries);
            }
            else if (action == "remove")
            {
                Assert.DoesNotContain(olderEntry, viewModel.Reports.ReportHistoryEntries);
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
            MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
            viewModel.Reports.LoadReportJson(olderJson, "older-language-report.json");
            ReportHistoryEntryViewModel olderEntry = viewModel.Reports.ReportHistoryEntries[0];
            viewModel.Reports.LoadReportJson(currentJson, "current-language-report.json");

            Task reopening = viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(olderEntry);
            Assert.True(viewModel.Reports.OpenReportHistoryEntryAsyncCommand.IsRunning);
            viewModel.SelectedLanguage = "Traditional Chinese";
            await reopening;

            Assert.Equal("older-language-report.json", viewModel.Reports.LoadedReport.SourceName);
            Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "預期");
            Assert.Equal("已顯示 8/40 筆", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.PageStatus);
            Assert.Equal(2, viewModel.Reports.ReportHistoryCount);
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
            MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
            viewModel.Reports.LoadReportJson(json, "large-language-report.json");

            viewModel.SelectedLanguage = "Traditional Chinese";
            Task relocalization = Assert.IsType<Task>(viewModel.Reports.RelocalizationTask, exactMatch: false);

            Assert.True(viewModel.Reports.IsRelocalizationRunning);
            Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "expected");
            await relocalization;

            Assert.False(viewModel.Reports.IsRelocalizationRunning);
            Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "預期");
            Assert.Equal("已顯示 8/40 筆", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.PageStatus);
            Assert.Equal(json, viewModel.Reports.LoadedReportJson);
            Assert.Equal(1, viewModel.Reports.ReportHistoryCount);
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
            MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
            viewModel.Reports.LoadReportJson(olderJson, "older-language-report.json");

            viewModel.SelectedLanguage = "Traditional Chinese";
            Task relocalization = Assert.IsType<Task>(viewModel.Reports.RelocalizationTask, exactMatch: false);
            Assert.True(viewModel.Reports.IsRelocalizationRunning);
            viewModel.Reports.LoadReportJson(newerJson, "newer-language-report.json");
            Assert.True(viewModel.Reports.IsRelocalizationRunning);
            viewModel.SelectedLanguage = "English";
            await relocalization;

            Assert.Equal("newer-language-report.json", viewModel.Reports.LoadedReport.SourceName);
            Assert.Equal(newerJson, viewModel.Reports.LoadedReportJson);
            Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "expected");
            Assert.Equal(2, viewModel.Reports.ReportHistoryCount);
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
            MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
            viewModel.Reports.LoadReportJson(json, "rapid-language-report.json");

            viewModel.SelectedLanguage = "Traditional Chinese";
            Task firstRequest = Assert.IsType<Task>(viewModel.Reports.RelocalizationTask, exactMatch: false);
            Assert.True(viewModel.Reports.IsRelocalizationRunning);
            viewModel.SelectedLanguage = "English";
            Task latestRequest = Assert.IsType<Task>(viewModel.Reports.RelocalizationTask, exactMatch: false);

            Assert.Same(firstRequest, latestRequest);
            await latestRequest;

            Assert.False(viewModel.Reports.IsRelocalizationRunning);
            Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "expected");
            Assert.Equal("Showing 8/40", viewModel.Reports.LoadedReport.OutputDifferenceGroupPage.PageStatus);
            Assert.Equal(json, viewModel.Reports.LoadedReportJson);
            Assert.Equal(1, viewModel.Reports.ReportHistoryCount);
        });
    }

    /// <summary>Relocalizing an existing report does not cancel a newer in-flight load.</summary>
    [Fact]
    public async Task LanguageChangeKeepsNewerInFlightReportGeneration()
    {
        string oldJson = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(runId: "old-report");
        string newJson = ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 10_000, sectionCount: 40);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.Reports.LoadReportJson(oldJson, "old-report.json");

        Task newLoad = viewModel.Reports.LoadReportJsonAsync(
            newJson,
            "new-large-report.json",
            TestContext.Current.CancellationToken);
        viewModel.SelectedLanguage = "Traditional Chinese";
        await newLoad;

        Assert.Equal("new-large-report.json", viewModel.Reports.LoadedReport.SourceName);
        Assert.Equal(newJson, viewModel.Reports.LoadedReportJson);
        Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferences[0].Badges, badge => badge.Text == "預期");
        Assert.Equal("new-large-report.json", viewModel.Reports.ReportHistoryEntries[0].SourceName);
    }

    private static CompositionRunReport CreateLargeDifferenceReport(
        CompositionRunReport source,
        int count,
        int sectionCount,
        string runId)
    {
        OutputDifferenceSummary[] differences =
        [
            .. Enumerable.Range(0, count).Select(index => new OutputDifferenceSummary(
                $"diff-{index:D5}",
                new Domain.Composition.ByteRange(index * 4L, 4),
                changedByteCount: 4,
                index == count - 1
                    ? Contracts.Reports.OutputDifferenceClassifications.Unexpected
                    : Contracts.Reports.OutputDifferenceClassifications.DeclaredReplacement,
                isAccepted: index != count - 1,
                $"evidence-{index:D5}",
                $"difference {index}",
                $"Section {index % sectionCount:D2}",
                "11111111111111111111",
                "22222222222222222222",
                beforeHexPreview: "AABBCCDD",
                afterHexPreview: "11223344",
                hexPreviewByteCount: 4,
                isHexPreviewComplete: true)),
        ];
        return new CompositionRunReport(
            runId,
            source.ProfileId,
            source.ProfileVersion,
            source.IcId,
            source.ModeId,
            source.ExperienceId,
            source.CompositionKind,
            source.StartedAtUtc,
            source.CompletedAtUtc,
            source.Inputs,
            source.Operations,
            source.Mutations,
            source.Issues,
            source.Output,
            differences,
            source.CompilationFingerprint,
            source.Validations,
            source.OutputNaming,
            source.DeliveryArtifacts,
            source.GeneralAdmission,
            source.ImageInitialization,
            source.DiagnosticPreview);
    }
}
