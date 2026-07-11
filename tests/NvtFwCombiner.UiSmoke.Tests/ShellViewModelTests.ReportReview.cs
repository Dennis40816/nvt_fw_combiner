using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies reports stay behind an explicit action until opened.</summary>
    [Fact]
    public void ReportReviewUsesToastAndModalState()
    {
        string json = ReportJsonSamples.Succeeded(runId: "ui-smoke");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.False(viewModel.CanOpenReport);
        Assert.False(viewModel.ShowReportCommand.CanExecute(null));
        Assert.Equal("No report", viewModel.ReportActionLabel);
        Assert.Equal("Build creates one", viewModel.ReportActionStatus);

        viewModel.LoadReportJson(json, "preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.Equal("Open report", viewModel.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.ReportActionStatus);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal(1, viewModel.ReportToastOpacity);
        Assert.Equal(json, viewModel.LoadedReportJson);
        Assert.True(viewModel.HasReportHistory);
        Assert.Equal(1, viewModel.ReportHistoryCount);
        Assert.Equal("1 report in history", viewModel.ReportHistorySummary);
        Assert.True(viewModel.CanOpenReportHistory);
        Assert.True(viewModel.ShowReportHistoryCommand.CanExecute(null));
        Assert.True(viewModel.ClearReportHistoryCommand.CanExecute(null));
        Assert.False(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.IsReportReviewViewOpen);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.ReportHistoryEntries);
        Assert.Equal("#1", historyEntry.SequenceLabel);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", historyEntry.Title);
        Assert.Equal("Merge / standard-merge / NT51927", historyEntry.Context);
        Assert.Equal("abcdef", historyEntry.OutputHash);
        Assert.Equal("No external command", historyEntry.CommandSummary);
        Assert.Equal("No issue", historyEntry.IssueSummary);
        Assert.False(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(string.Empty, viewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927).json", viewModel.ReportSaveFileName);
        Assert.True(viewModel.ShowReportCommand.CanExecute(null));
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Succeeded", viewModel.LoadedReport.OutcomeTitle);
        Assert.Contains("no reference diff check", viewModel.LoadedReport.OutcomeDetail, StringComparison.Ordinal);
        Assert.Equal("Review operation trace", viewModel.LoadedReport.NextStepTitle);
        Assert.Contains("Operations", viewModel.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Equal("No size", viewModel.LoadedReport.OutputSizeLabel);
        Assert.Equal("Preview only", viewModel.LoadedReport.OutputCommitmentLabel);
        Assert.False(viewModel.LoadedReport.IsOutputCommitted);
        Assert.True(viewModel.LoadedReport.IsOutputPreview);
        Assert.False(viewModel.LoadedReport.IsOutputStateUnknown);
        Assert.Contains(viewModel.LoadedReport.TriageRows, row =>
            row.Title == "1. Result" &&
            row.Detail == "Succeeded" &&
            row.Meta == "No issue");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "0" &&
            row.Meta == "No blocking issue");
        Assert.Equal(4, viewModel.LoadedReport.SummaryRows.Count);
        Assert.Contains(viewModel.LoadedReport.SummaryRows, row =>
            row.Title == "Status" &&
            row.Detail == "Succeeded" &&
            row.Meta == "No blocking issue");
        Assert.Equal(0, viewModel.LoadedReport.OperationCount);
        Assert.False(viewModel.LoadedReport.HasCommandOperations);
        Assert.False(viewModel.LoadedReport.HasStepOperations);

        var reportWithSessionPath = ReportReviewViewModel.FromJson(
            json,
            "preview-report.json",
            "C:/nfc/output/preview.bin");
        Assert.True(reportWithSessionPath.HasOutputArtifactPath);
        Assert.Equal("C:/nfc/output/preview.bin", reportWithSessionPath.OutputArtifactPath);

        viewModel.ShowReportCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.IsReportReviewViewOpen);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal(0, viewModel.ReportToastOpacity);

        viewModel.CloseReportCommand.Execute(null);

        Assert.False(viewModel.IsReportModalOpen);
    }

    /// <summary>Verifies report output state is parsed from the JSON contract, not from formatted display text.</summary>
    [Fact]
    public void ReportReviewUsesTypedOutputCommitmentState()
    {
        var committed = ReportReviewViewModel.FromJson(
            ReportJsonSamples.Succeeded(committed: true),
            "committed-report.json");
        var preview = ReportReviewViewModel.FromJson(
            ReportJsonSamples.Succeeded(committed: false),
            "preview-report.json");
        var unknown = ReportReviewViewModel.FromJson(
            ReportJsonSamples.Succeeded(committed: null),
            "unknown-report.json");

        Assert.Equal("Committed output", committed.OutputCommitmentLabel);
        Assert.True(committed.IsOutputCommitted);
        Assert.False(committed.IsOutputPreview);
        Assert.False(committed.IsOutputStateUnknown);

        Assert.Equal("Preview only", preview.OutputCommitmentLabel);
        Assert.False(preview.IsOutputCommitted);
        Assert.True(preview.IsOutputPreview);
        Assert.False(preview.IsOutputStateUnknown);

        Assert.Equal("Output state unknown", unknown.OutputCommitmentLabel);
        Assert.False(unknown.IsOutputCommitted);
        Assert.False(unknown.IsOutputPreview);
        Assert.True(unknown.IsOutputStateUnknown);
    }

    /// <summary>Verifies report loading errors still produce a reopenable report modal.</summary>
    [Fact]
    public void ReportReviewErrorsUseModalState()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportError("Startup report", "missing preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.Equal(string.Empty, viewModel.LoadedReportJson);
        Assert.Equal("Load failed", viewModel.ReportActionStatus);
        Assert.True(viewModel.HasReportHistory);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.ReportHistoryEntries);
        Assert.Equal("Report could not be loaded", historyEntry.Title);
        Assert.Equal("No output hash", historyEntry.OutputHash);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal("Report issue: Startup report", viewModel.ReportToastText);
        Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Report load failed", viewModel.LoadedReport.OutcomeTitle);
        Assert.Equal("Start with this issue", viewModel.LoadedReport.NextStepTitle);
        Assert.Equal("Load error", viewModel.LoadedReport.PrimaryIssue.Title);
        Assert.Contains("missing preview-report.json", viewModel.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "1" &&
            row.Meta == "Load error");

        viewModel.ShowReportCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.HasReportToast);
    }
}
