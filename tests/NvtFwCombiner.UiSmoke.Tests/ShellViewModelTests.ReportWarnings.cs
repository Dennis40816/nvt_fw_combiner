using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ReportReviewHistoryTests
{
    /// <summary>Durable envelope facts survive report projection while older reports keep a specific fallback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReportDpWarningUsesRecordedEnvelopeAndHistorySummary(bool chinese)
    {
        JsonNode root = JsonNode.Parse(ReportJsonSamples.CtrlRamWarning(
            issueCode: "DP_NONSTANDARD_SIZE_WARNING", message: "DP length advisory."))!;
        root["SourceEnvelope"] = new JsonObject
        {
            ["SourceSlotId"] = "dp-ab-input",
            ["ActualOutputLength"] = 0xC0000,
            ["ExpectedOuterLengths"] = new JsonArray(0x80000, 0x100000),
            ["UnexpectedLengthIssueCode"] = "DP_NONSTANDARD_SIZE_WARNING",
        };
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ReportReviewViewModel report = ReportReviewViewModel.FromJson(root.ToJsonString(), "dp-report.json", language: language);
        ReportLineViewModel issue = Assert.Single(report.Issues);
        Assert.Contains("786,432 bytes", issue.IssueSummary, StringComparison.Ordinal);
        Assert.Contains("524,288 / 1,048,576 bytes", issue.Detail, StringComparison.Ordinal);
        Assert.Contains("786,432 bytes", report.SummaryIssueDescriptions, StringComparison.Ordinal);

        Assert.True(root.AsObject().Remove("SourceEnvelope"));
        ReportReviewViewModel legacy = ReportReviewViewModel.FromJson(root.ToJsonString(), "old-dp-report.json", language: language);
        ReportLineViewModel legacyIssue = Assert.Single(legacy.Issues);
        Assert.Contains(chinese ? "此 IC 的預期大小" : "expected size for this IC", legacyIssue.IssueSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("standard capacity", legacyIssue.IssueSummary, StringComparison.Ordinal);
        Assert.Contains(chinese ? "客製的 OSD" : "customized OSD", legacyIssue.Detail, StringComparison.Ordinal);

        root["InputDiagnostics"] = new JsonArray(new JsonObject
        {
            ["IssueIndex"] = 0,
            ["SlotId"] = "dp-ab-input",
            ["Evidence"] = new JsonObject { ["AddressSpaceId"] = "source", ["ActualLength"] = 0xC0000 },
        });
        ReportReviewViewModel withActualOnly = ReportReviewViewModel.FromJson(
            root.ToJsonString(), "dp-actual-only.json", language: language);
        Assert.Contains("786,432 bytes", Assert.Single(withActualOnly.Issues).IssueSummary, StringComparison.Ordinal);
    }
    /// <summary>Verifies successful runs with warning diagnostics do not render as blocking issues.</summary>
    [Fact]
    public void ReportReviewSeparatesWarningsFromBlockingIssues()
    {
        string json = ReportJsonSamples.CtrlRamWarning();
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.Reports.LoadReportJson(json, "warning-report.json");

        Assert.Equal("Succeeded with 1 warning(s)", viewModel.Reports.ReportActionStatus);
        Assert.False(viewModel.Reports.LoadedReport.IsClean);
        Assert.True(viewModel.Reports.LoadedReport.HasWarnings);
        Assert.True(viewModel.Reports.LoadedReport.HasWarningsWithoutBlockingIssues);
        Assert.False(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.Equal("warning", Assert.Single(viewModel.Reports.LoadedReport.Issues).Severity);
        Assert.Equal(0, viewModel.Reports.LoadedReport.BlockingIssueCount);
        Assert.Equal(1, viewModel.Reports.LoadedReport.WarningCount);
        Assert.Equal("Succeeded with 1 warning(s)", viewModel.Reports.LoadedReport.OutcomeTitle);
        Assert.Equal("Review warning", viewModel.Reports.LoadedReport.NextStepTitle);
        Assert.Contains("truncated", viewModel.Reports.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Equal("1 warning", Assert.Single(viewModel.Reports.ReportHistoryEntries).IssueSummary);
    }

    /// <summary>Verifies report review uses schema severity before legacy code-based warning fallback.</summary>
    [Fact]
    public void ReportReviewUsesIssueSeverityForWarnings()
    {
        string json = ReportJsonSamples.CtrlRamWarning(
            runId: "ui-smoke-severity-warning",
            issueCode: "processor.review-note",
            message: "Processor completed with a review note.",
            operationId: "run-postbuild");

        var report = ReportReviewViewModel.FromJson(json, "severity-warning.json");

        Assert.True(report.HasWarningsWithoutBlockingIssues);
        Assert.False(report.HasPrimaryIssue);
        Assert.Equal(0, report.BlockingIssueCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal("Succeeded with 1 warning(s)", report.Status);
        ReportLineViewModel issue = Assert.Single(report.Issues);
        Assert.Equal("processor.review-note", issue.Title);
        Assert.Equal("warning", issue.Severity);
    }

    /// <summary>Verifies older reports without issue severity keep the documented truncation warning behavior.</summary>
    [Fact]
    public void ReportReviewKeepsLegacyTruncationWarningFallback()
    {
        string json = ReportJsonSamples.CtrlRamWarning(
            runId: "ui-smoke-legacy-warning",
            severity: null,
            message: "Input ctrlram-input was truncated.");

        var report = ReportReviewViewModel.FromJson(json, "legacy-warning.json");

        Assert.True(report.HasWarningsWithoutBlockingIssues);
        Assert.False(report.HasPrimaryIssue);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal("warning", Assert.Single(report.Issues).Severity);
    }
}
