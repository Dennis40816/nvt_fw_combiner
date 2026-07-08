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

}
