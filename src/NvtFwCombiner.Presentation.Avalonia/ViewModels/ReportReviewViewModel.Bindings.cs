using NvtFwCombiner.Application.Composition;

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

    /// <summary>Output size in bytes.</summary>
    public long OutputSize { get; }

    /// <summary>True when the report output was written to disk.</summary>
    public bool IsOutputCommitted => outputCommitted == true;

    /// <summary>True when the report describes a preview-only output.</summary>
    public bool IsOutputPreview => outputCommitted == false && !IsOutputNotGenerated;

    /// <summary>True when a blocked run committed no output artifact; zero is not presented as a file size.</summary>
    public bool IsOutputNotGenerated { get; }

    /// <summary>True when the report does not state whether output was committed.</summary>
    public bool IsOutputStateUnknown => outputCommitted is null;

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

    /// <summary>Non-serialized bytes attached only to the current verified UI session.</summary>
    internal CompositionRunInspectionSnapshot? InspectionSnapshot { get; }

    /// <summary>True when the current UI session knows the committed output artifact path.</summary>
    public bool HasOutputArtifactPath => !string.IsNullOrWhiteSpace(OutputArtifactPath);

    /// <summary>Input artifact rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Inputs { get; }

    /// <summary>Number of input rows.</summary>
    public int InputCount => Inputs.Count;

    /// <summary>Human-readable grouped input rows.</summary>
    public IReadOnlyList<ReportInputGroupViewModel> InputGroups { get; }

    /// <summary>True when grouped input rows are available.</summary>
    public bool HasInputGroups => InputGroups.Count > 0;

    /// <summary>True when input details are available.</summary>
    public bool HasInputs => Inputs.Count > 0;

    /// <summary>True when no input details are available.</summary>
    public bool HasNoInputs => !HasInputs;

    /// <summary>Operation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Operations { get; }

    /// <summary>Number of operation rows.</summary>
    public int OperationCount => Operations.Count;

    /// <summary>Human-readable run flow nodes.</summary>
    public IReadOnlyList<ReportOperationFlowNodeViewModel> OperationFlow { get; }

    /// <summary>Bounded operation-flow nodes rendered by the Operations tab.</summary>
    public ReportPagedListViewModel OperationFlowPage { get; }

    /// <summary>True when operation flow nodes are available.</summary>
    public bool HasOperationFlow => OperationFlow.Count > 0;

    /// <summary>True when operation details are available.</summary>
    public bool HasOperations => Operations.Count > 0;

    /// <summary>True when no operation flow or detail rows are available.</summary>
    public bool HasNoOperations => !HasOperationFlow && !HasOperations;

    /// <summary>Actual postbuild process invocations flattened into independently numbered review rows.</summary>
    public IReadOnlyList<ReportPostbuildInvocationViewModel> PostbuildInvocations { get; }

    /// <summary>Bounded postbuild invocation rows rendered by the Postbuild tab.</summary>
    public ReportPagedListViewModel PostbuildInvocationPage { get; }

    /// <summary>Number of independently numbered postbuild invocation rows.</summary>
    public int PostbuildInvocationCount => PostbuildInvocations.Count;

    /// <summary>True when postbuild invocation or declared-plan rows are available.</summary>
    public bool HasPostbuildInvocations => PostbuildInvocations.Count > 0;

    /// <summary>True when no postbuild invocation or declared-plan rows are available.</summary>
    public bool HasNoPostbuildInvocations => !HasPostbuildInvocations;

    /// <summary>Operations that do not contain an external command block.</summary>
    public IReadOnlyList<ReportLineViewModel> StepOperations { get; }

    /// <summary>Bounded operation-detail rows rendered by the Operations tab.</summary>
    public ReportPagedListViewModel StepOperationPage { get; }

    /// <summary>Number of non-command operation rows.</summary>
    public int StepOperationCount => StepOperations.Count;

    /// <summary>True when non-command operation details are available.</summary>
    public bool HasStepOperations => StepOperations.Count > 0;

    /// <summary>Mutation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Mutations { get; }

    /// <summary>Bounded mutation rows rendered by the Changes tab.</summary>
    public ReportPagedListViewModel MutationPage { get; }

    /// <summary>Number of mutation rows.</summary>
    public int MutationCount => Mutations.Count;

    /// <summary>True when mutation details are available.</summary>
    public bool HasMutations => Mutations.Count > 0;

    /// <summary>Final output-vs-reference difference rows.</summary>
    public IReadOnlyList<ReportLineViewModel> OutputDifferences { get; }

    /// <summary>Number of final output-vs-reference difference rows.</summary>
    public int OutputDifferenceCount => _outputDifferenceProjection.Count;

    /// <summary>Number of detailed difference row models created for currently requested UI pages.</summary>
    internal int MaterializedOutputDifferenceCount => _outputDifferenceProjection.MaterializedCount;

    /// <summary>Final output differences grouped by physical report section.</summary>
    public IReadOnlyList<ReportDifferenceGroupViewModel> OutputDifferenceGroups { get; }

    /// <summary>Bounded output-difference section groups rendered by the Changes tab.</summary>
    public ReportPagedListViewModel OutputDifferenceGroupPage { get; }

    /// <summary>True when output difference details are available.</summary>
    public bool HasOutputDifferences => _outputDifferenceProjection.Count > 0;

    /// <summary>Bounded complete or fallback Hex Diff review state.</summary>
    public ReportHexDiffViewModel HexDiff { get; }

    /// <summary>True when no output differences or changed ranges are available.</summary>
    public bool HasNoByteChanges => !HasOutputDifferences && !HasMutations;

    /// <summary>Simplified output-difference rows for the primary report view.</summary>
    public IReadOnlyList<ReportDifferenceSummaryRowViewModel> OutputDifferenceSummaryRows { get; }

    /// <summary>Bounded primary difference-summary rows rendered by the modal.</summary>
    public ReportPagedListViewModel OutputDifferenceSummaryPage { get; }

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

    /// <summary>Bounded issue rows rendered by the Issues tab.</summary>
    public ReportPagedListViewModel IssuePage { get; }

    /// <summary>Number of diagnostic rows, including warnings.</summary>
    public int IssueCount => Issues.Count;

    /// <summary>True when issue or warning diagnostics are available.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>True when no issue or warning diagnostics are available.</summary>
    public bool HasNoIssues => !HasIssues;

    /// <summary>Number of warning diagnostics.</summary>
    public int WarningCount => CountWarnings(Issues);

    /// <summary>True when warning diagnostics are available.</summary>
    public bool HasWarnings => WarningCount > 0;

    /// <summary>Number of blocking issue diagnostics.</summary>
    public int BlockingIssueCount => CountBlockingIssues(Issues);

    /// <summary>True when warnings exist but no blocking issue exists.</summary>
    public bool HasWarningsWithoutBlockingIssues => HasWarnings && !HasPrimaryIssue;

    /// <summary>The first issue to show as the report's primary reason.</summary>
    public ReportLineViewModel PrimaryIssue { get; }

    /// <summary>True when the report should show a primary blocking reason.</summary>
    public bool HasPrimaryIssue => BlockingIssueCount > 0;

    /// <summary>True when the compact success/warning summary may replace the blocking-issue focus panel.</summary>
    public bool HasNoPrimaryIssue => !HasPrimaryIssue;

    /// <summary>True when the report has neither blocking issues nor warnings.</summary>
    public bool IsClean => !HasPrimaryIssue && !HasWarnings;

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

}
