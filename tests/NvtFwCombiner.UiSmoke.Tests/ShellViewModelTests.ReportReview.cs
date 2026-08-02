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

        Assert.False(viewModel.Reports.CanOpenReport);
        Assert.False(viewModel.Reports.ShowReportCommand.CanExecute(null));
        Assert.Equal("No report", viewModel.Reports.ReportActionLabel);
        Assert.Equal("Build creates one", viewModel.Reports.ReportActionStatus);

        viewModel.Reports.LoadReportJson(json, "preview-report.json");

        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.CanOpenReport);
        Assert.Equal("Open report", viewModel.Reports.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.Reports.ReportActionStatus);
        Assert.True(viewModel.Reports.HasReportToast);
        Assert.Equal(1, viewModel.Reports.ReportToastOpacity);
        Assert.Equal(json, viewModel.Reports.LoadedReportJson);
        Assert.True(viewModel.Reports.HasReportHistory);
        Assert.Equal(1, viewModel.Reports.ReportHistoryCount);
        Assert.Equal("1 report in history", viewModel.Reports.ReportHistorySummary);
        Assert.True(viewModel.Reports.CanOpenReportHistory);
        Assert.True(viewModel.Reports.ShowReportHistoryCommand.CanExecute(null));
        Assert.True(viewModel.Reports.ClearReportHistoryCommand.CanExecute(null));
        Assert.False(viewModel.Reports.IsReportHistoryViewOpen);
        Assert.True(viewModel.Reports.IsReportReviewViewOpen);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("#1", historyEntry.SequenceLabel);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", historyEntry.Title);
        Assert.Equal("Merge / standard-merge / NT51927", historyEntry.Context);
        Assert.Equal("abcdef", historyEntry.OutputHash);
        Assert.Equal("No external command", historyEntry.CommandSummary);
        Assert.Equal("No issue", historyEntry.IssueSummary);
        Assert.False(viewModel.Reports.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReport.OutputArtifactPath);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927).json", viewModel.Reports.ReportSaveFileName);
        Assert.True(viewModel.Reports.ShowReportCommand.CanExecute(null));
        Assert.False(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Succeeded", viewModel.Reports.LoadedReport.OutcomeTitle);
        Assert.Contains("no reference diff check", viewModel.Reports.LoadedReport.OutcomeDetail, StringComparison.Ordinal);
        Assert.Equal("Review operation trace", viewModel.Reports.LoadedReport.NextStepTitle);
        Assert.Contains("Operations", viewModel.Reports.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Equal("No size", viewModel.Reports.LoadedReport.OutputSizeLabel);
        Assert.Equal("Preview only", viewModel.Reports.LoadedReport.OutputCommitmentLabel);
        Assert.False(viewModel.Reports.LoadedReport.IsOutputCommitted);
        Assert.True(viewModel.Reports.LoadedReport.IsOutputPreview);
        Assert.False(viewModel.Reports.LoadedReport.IsOutputStateUnknown);
        Assert.Equal(0, viewModel.Reports.LoadedReport.OperationCount);
        Assert.Empty(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.False(viewModel.Reports.LoadedReport.HasStepOperations);

        var reportWithSessionPath = ReportReviewViewModel.FromJson(
            json,
            "preview-report.json",
            "C:/nfc/output/preview.bin");
        Assert.True(reportWithSessionPath.HasOutputArtifactPath);
        Assert.Equal("C:/nfc/output/preview.bin", reportWithSessionPath.OutputArtifactPath);

        viewModel.Reports.ShowReportCommand.Execute(null);

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.False(viewModel.Reports.IsReportHistoryViewOpen);
        Assert.True(viewModel.Reports.IsReportReviewViewOpen);
        Assert.False(viewModel.Reports.HasReportToast);
        Assert.Equal(0, viewModel.Reports.ReportToastOpacity);

        viewModel.Reports.CloseReportCommand.Execute(null);

        Assert.False(viewModel.Reports.IsReportModalOpen);
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
        var blocked = ReportReviewViewModel.FromJson(
            ReportJsonSamples.CtrlRamCommandIssue(),
            "blocked-report.json");

        Assert.Equal("Committed output", committed.OutputCommitmentLabel);
        Assert.True(committed.IsOutputCommitted);
        Assert.False(committed.IsOutputPreview);
        Assert.False(committed.IsOutputNotGenerated);
        Assert.False(committed.IsOutputStateUnknown);

        Assert.Equal("Preview only", preview.OutputCommitmentLabel);
        Assert.False(preview.IsOutputCommitted);
        Assert.True(preview.IsOutputPreview);
        Assert.False(preview.IsOutputNotGenerated);
        Assert.False(preview.IsOutputStateUnknown);

        Assert.Equal("Output state unknown", unknown.OutputCommitmentLabel);
        Assert.False(unknown.IsOutputCommitted);
        Assert.False(unknown.IsOutputPreview);
        Assert.False(unknown.IsOutputNotGenerated);
        Assert.True(unknown.IsOutputStateUnknown);

        Assert.Equal("No output generated", blocked.OutputCommitmentLabel);
        Assert.False(blocked.IsOutputPreview);
        Assert.True(blocked.IsOutputNotGenerated);
        Assert.False(blocked.IsOutputStateUnknown);
    }

    /// <summary>Verifies report loading errors still produce a reopenable report modal.</summary>
    [Fact]
    public void ReportReviewErrorsUseModalState()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.Reports.LoadReportError("Startup report", "missing preview-report.json");

        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.CanOpenReport);
        Assert.Equal(string.Empty, viewModel.Reports.LoadedReportJson);
        Assert.Equal("Load failed", viewModel.Reports.ReportActionStatus);
        Assert.True(viewModel.Reports.HasReportHistory);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.Reports.ReportHistoryEntries);
        Assert.Equal("Report could not be loaded", historyEntry.Title);
        Assert.Equal("No output hash", historyEntry.OutputHash);
        Assert.True(viewModel.Reports.HasReportToast);
        Assert.Equal("Report issue: Startup report", viewModel.Reports.ReportToastText);
        Assert.True(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Report load failed", viewModel.Reports.LoadedReport.OutcomeTitle);
        Assert.Equal("Start with this issue", viewModel.Reports.LoadedReport.NextStepTitle);
        Assert.Equal("Load error", viewModel.Reports.LoadedReport.PrimaryIssue.Title);
        Assert.Contains("missing preview-report.json", viewModel.Reports.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
        viewModel.Reports.ShowReportCommand.Execute(null);

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.False(viewModel.Reports.HasReportToast);
    }
}
