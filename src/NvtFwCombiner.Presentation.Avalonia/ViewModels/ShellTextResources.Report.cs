// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string RunDateColumn => SelectLanguage("Run date ↓", "執行日期 ↓");
    public string RunTypeColumn => SelectLanguage("Type", "執行類型");
    public string RunResultColumn => SelectLanguage("Result", "結果");
    public string RunIssuesColumn => SelectLanguage("Issues", "問題數");
    public string RunReportsNewestFirst => SelectLanguage("Newest runs first", "最新執行的報告在最上方");
    public string RunReportsSelectHint => SelectLanguage("Select a report to view details", "選取報告以檢視詳細內容");
    public string LoadRunReportLabel => SelectLanguage("Load Report", "載入報告");
    public string BackToRunReportsLabel => SelectLanguage("Run reports", "執行報告");
    public string GetRunReportCount(int count)
    {
        return SelectLanguage(
            count == 1 ? "1 report" : FormattableString.Invariant($"{count} reports"),
            FormattableString.Invariant($"{count} 筆報告"));
    }
    public string DeleteHistoryTitle => SelectLanguage("Delete this report?", "刪除這筆報告？");
    public string DeleteHistoryDetail => SelectLanguage(
        "This removes only this history entry. Report files and output files will not be deleted.",
        "只移除這筆歷史紀錄，不會刪除報告檔或輸出檔案。");
    public string DeleteHistoryConfirmLabel => SelectLanguage("Delete", "刪除");

    public string ReportToastTitle { get; private init; } = string.Empty;

    public string ReplaceSelectionTitle { get; private init; } = string.Empty;

    public string CloseSelectionTooltip { get; private init; } = string.Empty;

    public string SelectedReplacementsTitle { get; private init; } = string.Empty;

    public string RequiredBeforeBuildTitle { get; private init; } = string.Empty;

    public string CloseLabel { get; private init; } = string.Empty;

    public string SaveReportLabel { get; private init; } = string.Empty;

    public string BuildCompletedTitle { get; private init; } = string.Empty;

    public string BuildCompletedDetail { get; private init; } = string.Empty;

    public string BuildCompletedOutputLabel { get; private init; } = string.Empty;

    public string BuildCompletedOpenFolderLabel { get; private init; } = string.Empty;

    public string LatestOutputOpenFolderLabel { get; private init; } = string.Empty;

    public string LatestOutputActionLabel { get; private init; } = string.Empty;

    public string BuildCompletedOkLabel { get; private init; } = string.Empty;

    public string BuildCompletedOpenFolderError { get; private init; } = string.Empty;

    public string BuildCompletedAdditionalOutputLabel { get; private init; } = string.Empty;

    public string FileRevealFailedTitle { get; private init; } = string.Empty;

    public string FileRevealFailedDetail { get; private init; } = string.Empty;

    public string CloseReportTooltip { get; private init; } = string.Empty;

    public string ReportHistoryTitle { get; private init; } = string.Empty;

    public string BackToReportLabel { get; private init; } = string.Empty;

    public string ClearAllLabel { get; private init; } = string.Empty;

    public string ClearHistoryLabel { get; private init; } = string.Empty;

    public string ClearHistoryTooltip { get; private init; } = string.Empty;

    public string NoReportHistoryLabel { get; private init; } = string.Empty;

    public string RunLabel { get; private init; } = string.Empty;

    public string OutputLabel { get; private init; } = string.Empty;

    public string RunResultReportReadyLabel { get; private init; } = string.Empty;

    public string PrimaryReasonLabel { get; private init; } = string.Empty;

    public string FailedStepLabel { get; private init; } = string.Empty;

    public string OutputImpactLabel { get; private init; } = string.Empty;

    public string NextActionLabel { get; private init; } = string.Empty;

    public string ChangeReviewTitle { get; private init; } = string.Empty;

    public string EvidenceTitle { get; private init; } = string.Empty;

    public string TraceLabel { get; private init; } = string.Empty;

    public string ReportTabInputs { get; private init; } = string.Empty;

    public string ReportTabChanges { get; private init; } = string.Empty;

    public string ReportTabOperations { get; private init; } = string.Empty;

    public string ReportTabPostbuild { get; private init; } = string.Empty;

    public string ReportTabIssues { get; private init; } = string.Empty;

    public string ReportTabRaw { get; private init; } = string.Empty;

    public string RawReportTitle { get; private init; } = string.Empty;

    public string RawReportDetail { get; private init; } = string.Empty;

    public string RunMetadataTitle { get; private init; } = string.Empty;

    public string ReportFileLabel { get; private init; } = string.Empty;

    public string FileLabel { get; private init; } = string.Empty;

    public string SizeLabel { get; private init; } = string.Empty;

    public string StateLabel { get; private init; } = string.Empty;

    public string StatusLabel { get; private init; } = string.Empty;

    public string ArtifactPathLabel { get; private init; } = string.Empty;

    public string InputsAndHashesTitle { get; private init; } = string.Empty;

    public string EmptyInputsMessage { get; private init; } = string.Empty;

    public string EmptyByteChangesMessage { get; private init; } = string.Empty;

    public string HexDiffViewportTitle { get; private init; } = string.Empty;

    public string HexDiffShowOriginalRowsLabel { get; private init; } = string.Empty;

    public string HexDiffOriginalRowLabel { get; private init; } = string.Empty;

    public string HexDiffRangeScrollAutomationName { get; private init; } = string.Empty;

    public string HexDiffRangeNavigatorTitle { get; private init; } = string.Empty;

    public string HexDiffRangeNavigatorDetail { get; private init; } = string.Empty;

    public string HexDiffResizeAutomationName { get; private init; } = string.Empty;

    public string HexDiffWhyLabel { get; private init; } = string.Empty;

    public string RangeLabel { get; private init; } = string.Empty;

    public string ResultLabel { get; private init; } = string.Empty;

    public string DetailLabel { get; private init; } = string.Empty;

    public string ChangedRangesTitle { get; private init; } = string.Empty;

    public string EmptyOperationsMessage { get; private init; } = string.Empty;

    public string OperationStepsTitle { get; private init; } = string.Empty;

    public string KindLabel { get; private init; } = string.Empty;

    public string SourceLabel { get; private init; } = string.Empty;

    public string EmptyPostbuildMessage { get; private init; } = string.Empty;

    public string HeaderRefreshTraceTitle { get; private init; } = string.Empty;

    public string EmptyIssuesMessage { get; private init; } = string.Empty;

    public string NoActionRequiredTitle { get; private init; } = string.Empty;

    public string IssuesAndWarningsTitle { get; private init; } = string.Empty;

    public string RangeTableTitle { get; private init; } = string.Empty;

    public string AddressSpaceLabel { get; private init; } = string.Empty;

    public string CopyCommandTooltip { get; private init; } = string.Empty;

    public string CopyRawReportTooltip { get; private init; } = string.Empty;

    public string ReportCopyFailedTitle { get; private init; } = string.Empty;

    public string ReportCopyFailedDetail { get; private init; } = string.Empty;

    public string DeleteReportTooltip { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
