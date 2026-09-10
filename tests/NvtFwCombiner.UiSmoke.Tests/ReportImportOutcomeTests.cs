using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Imported data stays readable without inventing a successful run.</summary>
public sealed class ReportImportOutcomeTests
{
    /// <summary>Unrecognized/incomplete objects retain exact raw data and a neutral outcome.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"unrelated\":\"not a report\"}")]
    [InlineData("{\"Issues\":null}")]
    [InlineData("{\"Issues\":\"invalid\"}")]
    [InlineData("{\"Operations\":[0]}")]
    public void IncompleteReportRemainsReadableWithoutSuccess(string json)
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.Reports.LoadReportJson(json, "incomplete.json");

        Assert.True(shell.Reports.CanOpenReport);
        Assert.Equal(json, shell.Reports.LoadedReportJson);
        Assert.Equal("Unknown", shell.Reports.LoadedReport.Status);
        Assert.Equal("Unknown", shell.Reports.LoadedReport.OutcomeTitle);
        Assert.False(shell.Reports.LoadedReport.IsClean);
        Assert.False(shell.Reports.LoadedReport.HasPrimaryIssue);
        Assert.NotEqual("✓", shell.Reports.LoadedReport.OutcomeIcon);
        Assert.DoesNotContain("succeeded", shell.Reports.LoadedReport.OutcomeAccessibilityLabel, StringComparison.OrdinalIgnoreCase);
        ReportHistoryEntryViewModel entry = Assert.Single(shell.Reports.ReportHistoryEntries);
        Assert.Equal("Unknown", entry.Status);
        Assert.False(entry.IsSuccess);
        Assert.False(entry.IsWarning);
        Assert.False(entry.IsError);
        Assert.Equal(json, entry.ReportJson);
    }

    /// <summary>The manual/startup file path publishes the same read-only unknown result.</summary>
    [Fact]
    public async Task AsyncImportPreservesUnknownDataWithoutClaimingSuccess()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        ReportPublicationResult result = await shell.Reports.LoadReportFileAsync(
            _ => ValueTask.FromResult("{}"), "incomplete.json", TestContext.Current.CancellationToken);

        Assert.Equal(ReportPublicationOutcome.Published, result.Outcome);
        Assert.Equal("{}", shell.Reports.LoadedReportJson);
        Assert.Equal("Unknown", shell.Reports.LoadedReport.Status);
        Assert.False(Assert.Single(shell.Reports.ReportHistoryEntries).IsSuccess);
    }

    /// <summary>Valid pre-extension run reports keep their existing successful projection.</summary>
    [Fact]
    public void ExistingRunReportKeepsItsSuccessfulOutcome()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        string json = ReportJsonSamples.Succeeded();
        shell.Reports.LoadReportJson(json, "valid.json");

        Assert.Equal("Succeeded", shell.Reports.LoadedReport.Status);
        Assert.True(shell.Reports.LoadedReport.IsClean);
        Assert.True(Assert.Single(shell.Reports.ReportHistoryEntries).IsSuccess);
        Assert.Equal(json, shell.Reports.LoadedReportJson);
    }

    /// <summary>Actual serialized reports preserve info/warning/error and legacy severity fallback.</summary>
    [Theory]
    [InlineData("info", "Succeeded")]
    [InlineData("warning", "Succeeded with 1 warning(s)")]
    [InlineData("error", "1 issue(s)")]
    [InlineData("fatal", "1 issue(s)")]
    [InlineData(null, "1 issue(s)")]
    public void SerializedReportsKeepIssueOutcomes(string? severity, string expected)
    {
        string json = ReportInputFeedbackTests.Json(ReportInputFeedbackTests.Report(
            [new CompositionIssue("ASSESSMENT", "Recorded issue", "run", severity is "fatal" or null ? "error" : severity)]));
        if (severity is null or "fatal")
        {
            JsonNode root = JsonNode.Parse(json)!;
            if (severity is null)
            {
                _ = root["Issues"]![0]!.AsObject().Remove("Severity");
            }
            else
            {
                root["Issues"]![0]!["Severity"] = severity;
            }
            json = root.ToJsonString();
        }
        var report = ReportReviewViewModel.FromJson(json, "serialized.json");
        Assert.Equal(expected, report.Status);
        Assert.False(report.IsOutcomeUnknown);
        Assert.Equal("Recorded issue", Assert.Single(report.Issues).Detail);
    }

    /// <summary>Older cached success labels cannot override incomplete raw evidence on any row.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoredSuccessMetadataIsReassessedForUnknownRaw(bool chinese)
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(), "control.json");
        ReportHistoryMetadataSnapshot stale = Assert.Single(shell.Reports.ExportReportHistory()).Metadata;
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        IReadOnlyList<ReportHistorySnapshot> snapshots =
        [new("first.json", "{}", string.Empty, stale), new("second.json", "{\"other\":1}", string.Empty, stale)];
        _ = await shell.Reports.LoadReportHistoryAsync(
            _ => Task.FromResult(snapshots), TestContext.Current.CancellationToken);
        string expected = chinese ? "未知" : "Unknown";
        Assert.Equal(2, shell.Reports.ReportHistoryCount);
        Assert.All(shell.Reports.ReportHistoryEntries, entry =>
        {
            Assert.Equal(expected, entry.Status);
            Assert.False(entry.IsSuccess);
            Assert.False(entry.IsWarning);
            Assert.False(entry.IsError);
        });
        await shell.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(shell.Reports.ReportHistoryEntries[1]);
        Assert.Equal(expected, shell.Reports.LoadedReport.OutcomeTitle);
        Assert.Equal("{\"other\":1}", shell.Reports.LoadedReportJson);
        Assert.Equal(snapshots.Select(item => item.ReportJson), shell.Reports.ExportReportHistory().Select(item => item.ReportJson));
        shell.SelectedLanguage = chinese ? "English" : "Traditional Chinese";
        if (shell.Reports.RelocalizationTask is { } relocalization)
        {
            await relocalization;
        }
        Assert.Equal(chinese ? "Unknown" : "未知", shell.Reports.LoadedReport.OutcomeTitle);
    }
}
