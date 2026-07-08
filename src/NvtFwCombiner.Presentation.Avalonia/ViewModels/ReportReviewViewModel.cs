using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Readable UI projection of a CLI/application run report JSON file.</summary>
public sealed partial class ReportReviewViewModel
{
    private ReportReviewViewModel(
        bool isEmpty,
        string sourceName,
        string profileId,
        string icId,
        string modeId,
        string experienceId,
        string compositionKind,
        string runId,
        string startedAtUtc,
        string title,
        string subtitle,
        string status,
        string output,
        string outputFileName,
        long outputSize,
        string outputSha256,
        string outputArtifactPath,
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language = ShellLanguage.English)
    {
        IsEmpty = isEmpty;
        SourceName = sourceName;
        ProfileId = profileId;
        IcId = icId;
        ModeId = modeId;
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
        RunId = runId;
        StartedAtUtc = startedAtUtc;
        Title = title;
        Subtitle = subtitle;
        Status = status;
        Output = output;
        OutputFileName = outputFileName;
        OutputSize = outputSize;
        OutputSha256 = outputSha256;
        OutputHashLabel = string.IsNullOrWhiteSpace(outputSha256)
            ? T(language, "No output hash", "無輸出雜湊")
            : Shorten(outputSha256, 16);
        OutputArtifactPath = string.IsNullOrWhiteSpace(outputArtifactPath) ? string.Empty : outputArtifactPath;
        HasOutputArtifactPath = !string.IsNullOrWhiteSpace(OutputArtifactPath);
        Inputs = inputs;
        Operations = operations;
        CommandOperations = [.. operations.Where(operation => operation.HasCodeBlock)];
        StepOperations = [.. operations.Where(operation => !operation.HasCodeBlock)];
        Mutations = mutations;
        OutputDifferences = outputDifferences;
        Issues = issues;
        Warnings = [.. issues.Where(IsWarning)];
        BlockingIssues = [.. issues.Where(issue => !IsWarning(issue))];
        PrimaryIssue = BlockingIssues.Count == 0 ? ReportLineViewModel.Empty : BlockingIssues[0];
        HasPrimaryIssue = BlockingIssues.Count > 0;
        HasWarnings = Warnings.Count > 0;
        HasWarningsWithoutBlockingIssues = HasWarnings && !HasPrimaryIssue;
        IsClean = !HasPrimaryIssue && !HasWarnings;
        InputGroups = CreateInputGroups(inputs, language);
        OperationFlow = CreateOperationFlow(inputs, operations, outputFileName, status, language);
        HasInputs = inputs.Count > 0;
        HasInputGroups = InputGroups.Count > 0;
        HasOperations = operations.Count > 0;
        HasOperationFlow = OperationFlow.Count > 0;
        HasNoOperations = !HasOperationFlow && !HasOperations;
        HasCommandOperations = CommandOperations.Count > 0;
        HasStepOperations = StepOperations.Count > 0;
        HasMutations = mutations.Count > 0;
        HasOutputDifferences = outputDifferences.Count > 0;
        HasIssues = issues.Count > 0;
        HasNoInputs = !HasInputs;
        HasNoCommandOperations = !HasCommandOperations;
        HasNoStepOperations = !HasStepOperations;
        HasNoMutations = !HasMutations;
        HasNoOutputDifferences = !HasOutputDifferences;
        HasNoIssues = !HasIssues;
        HasNoByteChanges = !HasOutputDifferences && !HasMutations;
        HasOutputFileName = !string.IsNullOrWhiteSpace(outputFileName) &&
            !string.Equals(outputFileName, "No output", StringComparison.OrdinalIgnoreCase);
        SummaryRows = CreateSummaryRows(status, output, inputs, operations, mutations, issues, language);
        OutcomeTitle = CreateOutcomeTitle(status, issues, language);
        OutcomeDetail = CreateOutcomeDetail(output, issues, language);
        OutcomeMeta = CreateOutcomeMeta(issues, language);
        OutcomeIcon = HasPrimaryIssue || HasWarnings ? "!" : "✓";
        OutcomeAccessibilityLabel = HasPrimaryIssue
            ? T(language, "Report has issues", "Report 有問題")
            : HasWarnings
                ? T(language, "Report succeeded with warnings", "Report 成功但有警告")
                : T(language, "Report succeeded", "Report 成功");
        NextStepTitle = HasPrimaryIssue
            ? CreateNextStepTitle(PrimaryIssue, language)
            : HasWarnings
                ? T(language, "Review warning", "查看警告")
                : T(language, "Ready for audit", "可進行審查");
        NextStepDetail = HasPrimaryIssue
            ? CreateIssueAction(PrimaryIssue, language)
            : HasWarnings
            ? Warnings[0].Detail
            : T(language, "Inputs, changes, operation order, and postbuild refresh are available in Audit details.", "輸入、差異、操作順序與 postbuild refresh 已整理在審查明細。");
        ByteDifferenceTitle = CreateByteDifferenceTitle(compositionKind, outputDifferences, language);
        ByteDifferenceDetail = CreateByteDifferenceDetail(compositionKind, outputDifferences, language);
        ByteDifferenceMeta = CreateByteDifferenceMeta(outputDifferences, language);
        OutputDifferenceSummaryRows = CreateOutputDifferenceSummaryRows(outputDifferences, language);
        AuditSummary = CreateAuditSummary(inputs, operations, mutations, outputDifferences, issues, language);
        TriageRows = CreateTriageRows(status, output, operations, issues, language);
        EvidenceRows = CreateEvidenceRows(inputs, operations, mutations, outputDifferences, issues, language);
        ShouldExpandIssues = HasIssues && (HasPrimaryIssue || HasWarnings);
        ShouldExpandCommandOperations = HasCommandOperations;
        ShouldExpandStepOperations = HasStepOperations && !HasCommandOperations;
    }

    /// <summary>Empty report placeholder.</summary>
    public static ReportReviewViewModel Empty { get; } = new(
        true,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        "No report loaded",
        "Load a run report JSON to review it here.",
        "Idle",
        string.Empty,
        string.Empty,
        0,
        string.Empty,
        string.Empty,
        [],
        [],
        [],
        [],
        []);

    /// <summary>True when no report is loaded.</summary>
    public bool IsEmpty { get; }

    /// <summary>File name or parser source label.</summary>
    public string SourceName { get; }

    /// <summary>Profile id recorded by the run report.</summary>
    public string ProfileId { get; }

    /// <summary>IC id recorded by the run report.</summary>
    public string IcId { get; }

    /// <summary>Mode id recorded by the run report.</summary>
    public string ModeId { get; }

    /// <summary>Experience id recorded by the run report.</summary>
    public string ExperienceId { get; }

    /// <summary>Composition kind recorded by the run report.</summary>
    public string CompositionKind { get; }

    /// <summary>Run id recorded by the run report.</summary>
    public string RunId { get; }

    /// <summary>Start timestamp recorded by the run report.</summary>
    public string StartedAtUtc { get; }

    /// <summary>Report title.</summary>
    public string Title { get; }

    /// <summary>Report subtitle.</summary>
    public string Subtitle { get; }

    /// <summary>Run status summary.</summary>
    public string Status { get; }

    /// <summary>Output artifact summary.</summary>
    public string Output { get; }

    /// <summary>Report-safe output file name.</summary>
    public string OutputFileName { get; }

    /// <summary>True when the report contains an output file name to show in the primary result panel.</summary>
    public bool HasOutputFileName { get; }

    /// <summary>Output size in bytes.</summary>
    public long OutputSize { get; }

    /// <summary>Full output SHA-256 recorded by the report.</summary>
    public string OutputSha256 { get; }

    /// <summary>Compact output hash label for dense traceability surfaces.</summary>
    public string OutputHashLabel { get; }

    /// <summary>Host-side output artifact path for the current UI session, not persisted in report JSON.</summary>
    public string OutputArtifactPath { get; }

    /// <summary>True when the current UI session knows the committed output artifact path.</summary>
    public bool HasOutputArtifactPath { get; }

    /// <summary>Input artifact rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Inputs { get; }

    /// <summary>Number of input rows.</summary>
    public int InputCount => Inputs.Count;

    /// <summary>Human-readable grouped input rows.</summary>
    public IReadOnlyList<ReportInputGroupViewModel> InputGroups { get; }

    /// <summary>True when grouped input rows are available.</summary>
    public bool HasInputGroups { get; }

    /// <summary>True when input details are available.</summary>
    public bool HasInputs { get; }

    /// <summary>True when no input details are available.</summary>
    public bool HasNoInputs { get; }

    /// <summary>Operation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Operations { get; }

    /// <summary>Number of operation rows.</summary>
    public int OperationCount => Operations.Count;

    /// <summary>Human-readable run flow nodes.</summary>
    public IReadOnlyList<ReportOperationFlowNodeViewModel> OperationFlow { get; }

    /// <summary>True when operation flow nodes are available.</summary>
    public bool HasOperationFlow { get; }

    /// <summary>True when operation details are available.</summary>
    public bool HasOperations { get; }

    /// <summary>True when no operation flow or detail rows are available.</summary>
    public bool HasNoOperations { get; }

    /// <summary>Operations that contain a fixed-width external command block.</summary>
    public IReadOnlyList<ReportLineViewModel> CommandOperations { get; }

    /// <summary>Number of command operation rows.</summary>
    public int CommandOperationCount => CommandOperations.Count;

    /// <summary>True when external command operations are available.</summary>
    public bool HasCommandOperations { get; }

    /// <summary>True when no external command operations are available.</summary>
    public bool HasNoCommandOperations { get; }

    /// <summary>Operations that do not contain an external command block.</summary>
    public IReadOnlyList<ReportLineViewModel> StepOperations { get; }

    /// <summary>Number of non-command operation rows.</summary>
    public int StepOperationCount => StepOperations.Count;

    /// <summary>True when non-command operation details are available.</summary>
    public bool HasStepOperations { get; }

    /// <summary>True when no non-command operation details are available.</summary>
    public bool HasNoStepOperations { get; }

    /// <summary>Mutation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Mutations { get; }

    /// <summary>Number of mutation rows.</summary>
    public int MutationCount => Mutations.Count;

    /// <summary>True when mutation details are available.</summary>
    public bool HasMutations { get; }

    /// <summary>True when no mutation details are available.</summary>
    public bool HasNoMutations { get; }

    /// <summary>Final output-vs-reference difference rows.</summary>
    public IReadOnlyList<ReportLineViewModel> OutputDifferences { get; }

    /// <summary>Number of final output-vs-reference difference rows.</summary>
    public int OutputDifferenceCount => OutputDifferences.Count;

    /// <summary>True when output difference details are available.</summary>
    public bool HasOutputDifferences { get; }

    /// <summary>True when no output difference details are available.</summary>
    public bool HasNoOutputDifferences { get; }

    /// <summary>True when no output differences or changed ranges are available.</summary>
    public bool HasNoByteChanges { get; }

    /// <summary>Simplified output-difference rows for the primary report view.</summary>
    public IReadOnlyList<ReportDifferenceSummaryRowViewModel> OutputDifferenceSummaryRows { get; }

    /// <summary>Primary byte-difference verdict title.</summary>
    public string ByteDifferenceTitle { get; }

    /// <summary>Primary byte-difference verdict detail.</summary>
    public string ByteDifferenceDetail { get; }

    /// <summary>Small byte-difference verdict metadata.</summary>
    public string ByteDifferenceMeta { get; }

    /// <summary>Compact audit-detail summary for the collapsed traceability section.</summary>
    public string AuditSummary { get; }

    /// <summary>Issue rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Issues { get; }

    /// <summary>Number of diagnostic rows, including warnings.</summary>
    public int IssueCount => Issues.Count;

    /// <summary>True when issue or warning diagnostics are available.</summary>
    public bool HasIssues { get; }

    /// <summary>True when no issue or warning diagnostics are available.</summary>
    public bool HasNoIssues { get; }

    /// <summary>Warning diagnostics that do not block a successful run.</summary>
    public IReadOnlyList<ReportLineViewModel> Warnings { get; }

    /// <summary>Number of warning diagnostics.</summary>
    public int WarningCount => Warnings.Count;

    /// <summary>True when warning diagnostics are available.</summary>
    public bool HasWarnings { get; }

    /// <summary>Blocking issue diagnostics.</summary>
    public IReadOnlyList<ReportLineViewModel> BlockingIssues { get; }

    /// <summary>Number of blocking issue diagnostics.</summary>
    public int BlockingIssueCount => BlockingIssues.Count;

    /// <summary>True when warnings exist but no blocking issue exists.</summary>
    public bool HasWarningsWithoutBlockingIssues { get; }

    /// <summary>The first issue to show as the report's primary reason.</summary>
    public ReportLineViewModel PrimaryIssue { get; }

    /// <summary>True when the report should show a primary blocking reason.</summary>
    public bool HasPrimaryIssue { get; }

    /// <summary>True when the report has no blocking issue and can use the success treatment.</summary>
    public bool IsSuccessful => !HasPrimaryIssue;

    /// <summary>True when the report has neither blocking issues nor warnings.</summary>
    public bool IsClean { get; }

    /// <summary>Compact summary chips shown at the top of the modal.</summary>
    public IReadOnlyList<ReportLineViewModel> SummaryRows { get; }

    /// <summary>Primary report outcome shown before detailed evidence.</summary>
    public string OutcomeTitle { get; }

    /// <summary>Short outcome explanation that tells the user where to start.</summary>
    public string OutcomeDetail { get; }

    /// <summary>Small outcome metadata line.</summary>
    public string OutcomeMeta { get; }

    /// <summary>Short semantic status icon displayed in the report outcome badge.</summary>
    public string OutcomeIcon { get; }

    /// <summary>Readable label for the report outcome icon.</summary>
    public string OutcomeAccessibilityLabel { get; }

    /// <summary>Title for the next recommended review step.</summary>
    public string NextStepTitle { get; }

    /// <summary>Description for the next recommended review step.</summary>
    public string NextStepDetail { get; }

    /// <summary>Ordered rows that tell the user where to look first.</summary>
    public IReadOnlyList<ReportLineViewModel> TriageRows { get; }

    /// <summary>Compact counts for each available evidence category.</summary>
    public IReadOnlyList<ReportLineViewModel> EvidenceRows { get; }

    /// <summary>True when the issue list should open by default.</summary>
    public bool ShouldExpandIssues { get; }

    /// <summary>True when external command evidence should open by default.</summary>
    public bool ShouldExpandCommandOperations { get; }

    /// <summary>True when normal operation evidence should open by default.</summary>
    public bool ShouldExpandStepOperations { get; }

    /// <summary>Loads a readable report model from run report JSON.</summary>
    public static ReportReviewViewModel FromJson(
        string json,
        string sourceName,
        string? outputArtifactPath = null,
        ShellLanguage language = ShellLanguage.English)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string profileId = GetString(root, nameof(ProfileId));
        string icId = GetString(root, nameof(IcId));
        string modeId = GetString(root, nameof(ModeId));
        string experienceId = GetString(root, nameof(ExperienceId));
        string compositionKind = GetString(root, nameof(CompositionKind));
        string runId = GetString(root, nameof(RunId));
        string startedAt = GetString(root, nameof(StartedAtUtc));
        string outputFileName = GetOutputString(root, "FileName");
        long outputSize = GetOutputLong(root, "Size");
        string outputSha256 = GetOutputString(root, "Sha256");
        IReadOnlyList<ReportLineViewModel> inputs = ParseInputs(root);
        IReadOnlyList<ReportLineViewModel> operations = ParseOperations(root);
        IReadOnlyList<ReportLineViewModel> mutations = ParseMutations(root);
        IReadOnlyList<ReportLineViewModel> outputDifferences = ParseOutputDifferences(root, language);
        IReadOnlyList<ReportLineViewModel> issues = ParseIssues(root);
        string status = CreateStatus(issues, language);

        return new ReportReviewViewModel(
            false,
            sourceName,
            profileId,
            icId,
            modeId,
            experienceId,
            compositionKind,
            runId,
            startedAt,
            $"{profileId} ({icId})",
            $"{compositionKind} / {experienceId} / {Shorten(runId, 18)} / {startedAt}",
            status,
            ParseOutput(root),
            outputFileName,
            outputSize,
            outputSha256,
            outputArtifactPath ?? string.Empty,
            inputs,
            operations,
            mutations,
            outputDifferences,
            issues,
            language);
    }

    /// <summary>Creates an error report when JSON parsing or loading fails.</summary>
    public static ReportReviewViewModel Error(
        string sourceName,
        string message,
        string issueTitle = "Parse error",
        string status = "Invalid JSON",
        ShellLanguage language = ShellLanguage.English)
    {
        return new ReportReviewViewModel(
            false,
            sourceName,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "Report could not be loaded",
            sourceName,
            status,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            [],
            [],
            [],
            [],
            [new ReportLineViewModel(issueTitle, message, "report-json")],
            language);
    }

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

    private static int CountBlockingIssues(IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count(issue => !IsWarning(issue));
    }

    private static int CountWarnings(IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count(IsWarning);
    }

    private static string T(ShellLanguage language, string english, string chineseTraditional)
    {
        return language == ShellLanguage.ChineseTraditional ? chineseTraditional : english;
    }

    private static bool IsWarning(ReportLineViewModel issue)
    {
        return string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Severity, "info", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrWhiteSpace(issue.Severity) &&
                string.Equals(issue.Title, "input.address-space.truncated", StringComparison.Ordinal));
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

    private static string FormatEndpoint(string? addressSpaceId, string? range)
    {
        return string.IsNullOrWhiteSpace(addressSpaceId)
            ? "(none)"
            : $"{addressSpaceId} {range ?? string.Empty}".Trim();
    }

    private static (string ReasonSummary, string CommandBlock) ExtractCombinerCommand(string reason)
    {
        const string marker = "Combiner command: ";
        int markerIndex = reason.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return (reason, string.Empty);
        }

        string summary = reason[..(markerIndex + "Combiner command".Length)].Trim();
        string command = reason[(markerIndex + marker.Length)..].Trim();
        if (command.EndsWith('.'))
        {
            command = command[..^1];
        }

        return (summary, command);
    }

    private static string? GetRangeOrNull(JsonElement element, string propertyName)
    {
        return TryGetRange(element, propertyName) is { } range
            ? FormatRange(range)
            : null;
    }

    private static JsonElement? TryGetRange(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement range) && range.ValueKind == JsonValueKind.Object
            ? range
            : null;
    }

    private static string FormatRange(JsonElement range)
    {
        long start = GetLong(range, "Start");
        long end = GetLong(range, "EndExclusive");
        long length = GetLong(range, "Length");
        return string.Create(CultureInfo.InvariantCulture, $"0x{start:X}-0x{end - 1:X} (len 0x{length:X})");
    }

    private static string? GetStringOrNull(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return GetStringOrNull(element, propertyName) ?? string.Empty;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long number)
            ? number
            : 0;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }

    private static string Shorten(string text, int keep)
    {
        return text.Length <= keep ? text : $"{text[..keep]}...";
    }
}
