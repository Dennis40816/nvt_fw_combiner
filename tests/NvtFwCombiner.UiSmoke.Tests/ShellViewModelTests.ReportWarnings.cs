using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
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
