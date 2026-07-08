using System.Globalization;

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

    private static IReadOnlyList<ReportLineViewModel> CreateSummaryRows(
        string status,
        string output,
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        int warningCount = CountWarnings(issues);
        return
        [
            new ReportLineViewModel(T(language, "Status", "狀態"), status, firstBlockingIssue?.Title ?? FormatWarningMeta(warningCount, language)),
            new ReportLineViewModel(T(language, "Inputs", "輸入"), inputs.Count.ToString(CultureInfo.InvariantCulture), T(language, "files", "檔案")),
            new ReportLineViewModel(
                T(language, "Steps", "步驟"),
                operations.Count.ToString(CultureInfo.InvariantCulture),
                commandCount == 0
                    ? T(language, "operations", "操作")
                    : T(
                        language,
                        $"{commandCount.ToString(CultureInfo.InvariantCulture)} command(s)",
                        $"{commandCount.ToString(CultureInfo.InvariantCulture)} 個 command")),
            new ReportLineViewModel(T(language, "Mutations", "變更"), mutations.Count.ToString(CultureInfo.InvariantCulture), output),
        ];
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
        ShellLanguage language)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? warningCount == 0
                ? T(language, "No issues reported. Audit details are organized below.", "沒有回報問題；審查明細已整理在下方。")
                : T(language, "The run completed, but review the warning before treating the output as final evidence.", "流程已完成，但請先確認警告再把輸出視為最終證據。")
            : string.IsNullOrWhiteSpace(output)
            ? T(language, "The run did not produce an output artifact. Start with the first issue below.", "此次執行沒有產生輸出檔；請先查看第一個問題。")
            : T(language, "Start with the first issue below, then verify the related inputs, operations, and output evidence.", "請先查看第一個問題，再確認相關輸入、操作與輸出證據。");
    }

    private static string CreateOutcomeMeta(IReadOnlyList<ReportLineViewModel> issues, ShellLanguage language)
    {
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        return firstBlockingIssue?.Meta ?? FormatWarningMeta(CountWarnings(issues), language);
    }

    private static string CreateNextStepTitle(ReportLineViewModel issue, ShellLanguage language)
    {
        return string.Equals(issue.Title, "input.address-space.length-mismatch", StringComparison.Ordinal)
            ? T(language, "Fix input size", "修正輸入大小")
            : T(language, "Start with this issue", "先查看此問題");
    }

    private static string CreateIssueAction(ReportLineViewModel issue, ShellLanguage language)
    {
        return issue.Title switch
        {
            "input.address-space.length-mismatch" =>
                T(
                    language,
                    "Use a BIN whose byte length matches the selected IC/profile range, or switch to a workflow that explicitly allows padding/truncation. This run stays blocked because no relaxation policy applies.",
                    "請使用 byte 長度符合所選 IC/profile 範圍的 BIN，或改用明確允許 padding/truncation 的流程。此執行沒有放寬 policy，因此會阻擋。"),
            "input.address-space.truncated" =>
                T(
                    language,
                    "The selected profile allowed truncation for this CtrlRAM input. Review the warning and output differences before using the artifact as evidence.",
                    "所選 profile 允許此 CtrlRAM input truncation；使用輸出作為證據前，請確認警告與輸出差異。"),
            _ => T(language, "Fix the reported issue, then run Build again.", "修正回報問題後再重新建立。"),
        };
    }

    private static IReadOnlyList<ReportLineViewModel> CreateTriageRows(
        string status,
        string output,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        ReportLineViewModel? primaryIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        ReportLineViewModel? firstWarning = issues.FirstOrDefault(IsWarning);
        return primaryIssue is not null
            ?
            [
                new ReportLineViewModel(T(language, "1. First issue", "1. 第一個問題"), primaryIssue.Title, primaryIssue.Meta),
                new ReportLineViewModel(T(language, "2. Recommended action", "2. 建議處理"), CreateIssueAction(primaryIssue, language), T(language, "action", "處理方式")),
                new ReportLineViewModel(T(language, "3. Message", "3. 訊息"), primaryIssue.Detail, T(language, "reason", "原因")),
                new ReportLineViewModel(
                    T(language, "4. Evidence", "4. 證據"),
                    commandCount > 0 ? T(language, "Refresh commands", "Refresh commands") : T(language, "Operation steps", "操作步驟"),
                    commandCount > 0
                        ? T(language, $"{commandCount.ToString(CultureInfo.InvariantCulture)} command(s)", $"{commandCount.ToString(CultureInfo.InvariantCulture)} 個 command")
                        : T(language, $"{operations.Count.ToString(CultureInfo.InvariantCulture)} operation(s)", $"{operations.Count.ToString(CultureInfo.InvariantCulture)} 個操作")),
            ]
            : firstWarning is not null
            ?
            [
                new ReportLineViewModel(T(language, "1. Result", "1. 結果"), status, T(language, "No blocking issue", "沒有阻擋問題")),
                new ReportLineViewModel(T(language, "2. Warning", "2. 警告"), firstWarning.Title, firstWarning.Meta),
                new ReportLineViewModel(
                    T(language, "3. Evidence", "3. 證據"),
                    commandCount > 0 ? T(language, "Refresh commands available", "有 refresh command 證據") : T(language, "Operation trace available", "有操作 trace"),
                    commandCount > 0
                        ? T(language, $"{commandCount.ToString(CultureInfo.InvariantCulture)} command(s)", $"{commandCount.ToString(CultureInfo.InvariantCulture)} 個 command")
                        : T(language, $"{operations.Count.ToString(CultureInfo.InvariantCulture)} operation(s)", $"{operations.Count.ToString(CultureInfo.InvariantCulture)} 個操作")),
            ]
            :
            [
                new ReportLineViewModel(T(language, "1. Result", "1. 結果"), status, T(language, "No issue", "沒有問題")),
                new ReportLineViewModel("2. Output", string.IsNullOrWhiteSpace(output) ? T(language, "No output", "無輸出") : output, "artifact"),
                new ReportLineViewModel(
                    T(language, "3. Evidence", "3. 證據"),
                    commandCount > 0 ? T(language, "Refresh commands available", "有 refresh command 證據") : T(language, "Operation trace available", "有操作 trace"),
                    commandCount > 0
                        ? T(language, $"{commandCount.ToString(CultureInfo.InvariantCulture)} command(s)", $"{commandCount.ToString(CultureInfo.InvariantCulture)} 個 command")
                        : T(language, $"{operations.Count.ToString(CultureInfo.InvariantCulture)} operation(s)", $"{operations.Count.ToString(CultureInfo.InvariantCulture)} 個操作")),
            ];
    }

    private static IReadOnlyList<ReportLineViewModel> CreateEvidenceRows(
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        int stepCount = operations.Count - commandCount;
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        return
        [
            new ReportLineViewModel(
                T(language, "Issues", "問題"),
                blockingIssueCount.ToString(CultureInfo.InvariantCulture),
                firstBlockingIssue?.Title ?? T(language, "No blocking issue", "沒有阻擋問題")),
            new ReportLineViewModel(
                T(language, "Warnings", "警告"),
                warningCount.ToString(CultureInfo.InvariantCulture),
                warningCount == 0 ? T(language, "No warning", "沒有警告") : issues.First(IsWarning).Title),
            new ReportLineViewModel(
                T(language, "Inputs", "輸入"),
                inputs.Count.ToString(CultureInfo.InvariantCulture),
                T(language, "file hashes", "檔案雜湊")),
            new ReportLineViewModel(
                "Commands",
                commandCount.ToString(CultureInfo.InvariantCulture),
                T(language, "external processors", "外部處理器")),
            new ReportLineViewModel(
                T(language, "Steps", "步驟"),
                stepCount.ToString(CultureInfo.InvariantCulture),
                T(language, "copy/process order", "copy/process 順序")),
            new ReportLineViewModel(
                T(language, "Mutations", "變更"),
                mutations.Count.ToString(CultureInfo.InvariantCulture),
                T(language, "changed ranges", "變更範圍")),
            new ReportLineViewModel(
                "Output diff",
                outputDifferences.Count.ToString(CultureInfo.InvariantCulture),
                CreateOutputDifferenceMeta(outputDifferences, language)),
        ];
    }
}
