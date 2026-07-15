using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies successful runs with warning diagnostics do not render as blocking issues.</summary>
    [Fact]
    public void ReportReviewSeparatesWarningsFromBlockingIssues()
    {
        string json = ReportJsonSamples.CtrlRamWarning();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "warning-report.json");

        Assert.Equal("Succeeded with 1 warning(s)", viewModel.ReportActionStatus);
        Assert.False(viewModel.LoadedReport.IsClean);
        Assert.True(viewModel.LoadedReport.HasWarnings);
        Assert.True(viewModel.LoadedReport.HasWarningsWithoutBlockingIssues);
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("warning", Assert.Single(viewModel.LoadedReport.Issues).Severity);
        Assert.Equal(0, viewModel.LoadedReport.BlockingIssueCount);
        Assert.Equal(1, viewModel.LoadedReport.WarningCount);
        Assert.Equal("Succeeded with 1 warning(s)", viewModel.LoadedReport.OutcomeTitle);
        Assert.Equal("Review warning", viewModel.LoadedReport.NextStepTitle);
        Assert.Contains("truncated", viewModel.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains(viewModel.LoadedReport.TriageRows, row =>
            row.Title == "2. Warning" &&
            row.Detail == CompositionIssueCodes.InputAddressSpaceTruncated &&
            row.Meta == "replace-ctrlram");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "0" &&
            row.Meta == "No blocking issue");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Warnings" &&
            row.Detail == "1" &&
            row.Meta == CompositionIssueCodes.InputAddressSpaceTruncated);
        Assert.Equal("1 warning", Assert.Single(viewModel.ReportHistoryEntries).IssueSummary);
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
