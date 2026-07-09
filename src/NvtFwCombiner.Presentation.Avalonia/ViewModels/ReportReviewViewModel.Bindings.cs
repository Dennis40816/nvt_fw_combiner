namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
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

    /// <summary>True when the report output was committed, false for preview, null when unknown.</summary>
    public bool? OutputCommitted { get; }

    /// <summary>True when the report output was written to disk.</summary>
    public bool IsOutputCommitted { get; }

    /// <summary>True when the report describes a preview-only output.</summary>
    public bool IsOutputPreview { get; }

    /// <summary>True when the report does not state whether output was committed.</summary>
    public bool IsOutputStateUnknown { get; }

    /// <summary>Readable output size label for the primary report summary.</summary>
    public string OutputSizeLabel { get; }

    /// <summary>Readable output commit/preview state for the primary report summary.</summary>
    public string OutputCommitmentLabel { get; }

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

    /// <summary>Final output differences grouped by report section.</summary>
    public IReadOnlyList<ReportDifferenceGroupViewModel> OutputDifferenceGroups { get; }

    /// <summary>True when grouped output difference details are available.</summary>
    public bool HasOutputDifferenceGroups { get; }

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
