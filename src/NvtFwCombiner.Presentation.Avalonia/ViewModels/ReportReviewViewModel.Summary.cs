using System.Globalization;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static string CreateStatus(IReadOnlyList<ReportLineViewModel> issues, ShellLanguage language)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? warningCount == 0
                ? T(language, "Succeeded", "成功")
                : T(
                    language,
                    string.Create(CultureInfo.InvariantCulture, $"Succeeded with {warningCount} warning(s)"),
                    string.Create(CultureInfo.InvariantCulture, $"成功，含 {warningCount} 個警告"))
            : warningCount == 0
            ? T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"{blockingIssueCount} issue(s)"),
                string.Create(CultureInfo.InvariantCulture, $"{blockingIssueCount} 個問題"))
            : T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"{blockingIssueCount} issue(s), {warningCount} warning(s)"),
                string.Create(CultureInfo.InvariantCulture, $"{blockingIssueCount} 個問題，{warningCount} 個警告"));
    }

    private static string FormatWarningMeta(int warningCount, ShellLanguage language)
    {
        return warningCount == 0
            ? T(language, "No blocking issue", "沒有阻擋問題")
            : T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"{warningCount} warning(s)"),
                string.Create(CultureInfo.InvariantCulture, $"{warningCount} 個警告"));
    }

    private static string CreateOutcomeTitle(
        string status,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? status
            : string.Equals(status, "Load failed", StringComparison.Ordinal)
            ? T(language, "Report load failed", "Report 載入失敗")
            : warningCount == 0
                ? T(language, "Needs attention", "需要處理")
                : T(language, "Needs attention with warnings", "需要處理，且有警告");
    }

    private static string CreateOutcomeDetail(
        string output,
        IReadOnlyList<ReportLineViewModel> issues,
        string compositionKind,
        OutputDifferenceProjection outputDifferences,
        ShellLanguage language)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? warningCount == 0
                ? CreateCleanOutcomeDetail(compositionKind, outputDifferences, language)
                : T(language, "The run completed, but review the warning before treating the output as final evidence.", "流程已完成，但請先確認警告再把輸出視為最終證據。")
            : string.IsNullOrWhiteSpace(output)
            ? T(language, "The run did not produce an output artifact. Start with the first issue below.", "此次執行沒有產生輸出檔；請先查看第一個問題。")
            : T(language, "Start with the first issue below, then verify the related inputs, operations, and output evidence.", "請先查看第一個問題，再確認相關輸入、操作與輸出證據。");
    }

    private static string CreateCleanOutcomeDetail(
        string compositionKind,
        OutputDifferenceProjection outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count > 0 && !outputDifferences.HasReviewRequired
            ? T(
                language,
                "No blocking issues or warnings. All reported byte changes are classified as expected.",
                "沒有阻擋問題或警告；所有回報的 byte 變更都已分類為預期變更。")
            : IsReplaceComposition(compositionKind)
                ? T(
                    language,
                    "No blocking issues or warnings. No final output byte differences were reported.",
                    "沒有阻擋問題或警告；沒有回報 final output byte 差異。")
                : T(
                    language,
                    "No blocking issues or warnings. This workflow has no reference diff check; review the operation trace if evidence is needed.",
                    "沒有阻擋問題或警告；此流程沒有 reference diff check，需要證據時請查看操作紀錄。");
    }

    private static string CreateOutcomeMeta(IReadOnlyList<ReportLineViewModel> issues, ShellLanguage language)
    {
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        return firstBlockingIssue?.Meta ?? FormatWarningMeta(CountWarnings(issues), language);
    }

    private static string CreateNextStepTitle(ReportLineViewModel issue, ShellLanguage language)
    {
        return string.Equals(issue.Title, WorkbenchCompositionIssueCodes.InputAddressSpaceLengthMismatch, StringComparison.Ordinal)
            ? T(language, "Fix input size", "修正輸入大小")
            : T(language, "Start with this issue", "先查看此問題");
    }

    private static string CreateCleanNextStepTitle(
        OutputDifferenceProjection outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count > 0
            ? T(language, "Inspect expected changes", "查看預期變更")
            : T(language, "Review operation trace", "查看操作紀錄");
    }

    private static string CreateCleanNextStepDetail(
        OutputDifferenceProjection outputDifferences,
        IReadOnlyList<ReportLineViewModel> operations,
        ShellLanguage language)
    {
        return outputDifferences.Count > 0
            ? T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"Inspect {outputDifferences.Count} expected change(s) in Changes. Each entry starts with the affected data field; raw bytes stay in its details."),
                string.Create(CultureInfo.InvariantCulture, $"到 Changes 查看 {outputDifferences.Count} 筆預期變更；每筆先顯示受影響資料欄位，raw bytes 收在詳細內容。"))
            : T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"No output comparison changes were reported. Review Operations for the {operations.Count} recorded step(s)."),
                string.Create(CultureInfo.InvariantCulture, $"沒有回報 output comparison 變更；請到 Operations 確認 {operations.Count} 個已記錄步驟。"));
    }

    private static string CreateOutputSizeLabel(long outputSize, ShellLanguage language)
    {
        return outputSize <= 0
            ? T(language, "No size", "無大小")
            : string.Create(CultureInfo.InvariantCulture, $"{outputSize} bytes");
    }

    private static string CreateOutputCommitmentLabel(bool? committed, ShellLanguage language)
    {
        return committed == true
            ? T(language, "Committed output", "已寫出輸出")
            : committed == false
                ? T(language, "Preview only", "僅預覽")
                : T(language, "Output state unknown", "輸出狀態未知");
    }

    private static string CreateIssueAction(ReportLineViewModel issue, ShellLanguage language)
    {
        return issue.Title switch
        {
            WorkbenchCompositionIssueCodes.InputAddressSpaceLengthMismatch =>
                T(
                    language,
                    "Use a BIN whose byte length matches the selected IC/profile range, or switch to a workflow that explicitly allows padding/truncation. This run stays blocked because no relaxation policy applies.",
                    "請使用 byte 長度符合所選 IC/profile 範圍的 BIN，或改用明確允許 padding/truncation 的流程。此執行沒有放寬 policy，因此會阻擋。"),
            WorkbenchCompositionIssueCodes.InputAddressSpaceTruncated =>
                T(
                    language,
                    "The selected profile allowed truncation for this CtrlRAM input. Review the warning and output differences before using the artifact as evidence.",
                    "所選 profile 允許此 CtrlRAM input truncation；使用輸出作為證據前，請確認警告與輸出差異。"),
            _ => T(language, "Fix the reported issue, then run Build again.", "修正回報問題後再重新建立。"),
        };
    }

}
