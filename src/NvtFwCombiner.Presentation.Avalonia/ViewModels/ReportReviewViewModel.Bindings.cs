namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportReviewViewModel
{
    public bool IsEmpty { get; }

    public string SourceName { get; }

    public string ProfileId { get; }

    public string IcId { get; }

    public string ModeId { get; }

    public string ExperienceId { get; }

    public string CompositionKind { get; }

    public string RunId { get; }

    public string StartedAtUtc { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Status { get; }

    public string Output { get; }

    public string OutputFileName { get; }

    public long OutputSize { get; }

    public bool IsOutputCommitted => outputCommitted == true;

    /// <summary>True when the report describes a preview-only output.</summary>
    public bool IsOutputPreview => outputCommitted == false && !IsOutputNotGenerated;

    public bool IsOutputNotGenerated { get; }

    public bool IsOutputStateUnknown => outputCommitted is null;

    public string OutputSizeLabel { get; }

    public string OutputCommitmentLabel { get; }

    public string OutputSha256 { get; }

    public string OutputHashLabel { get; }

    /// <summary>Host-side output artifact path for the current UI session, not persisted in report JSON.</summary>
    public string OutputArtifactPath { get; }

    /// <summary>Non-serialized bytes attached only to the current verified UI session.</summary>
    internal CompositionRunInspectionSnapshot? InspectionSnapshot { get; }

    public bool HasOutputArtifactPath => !string.IsNullOrWhiteSpace(OutputArtifactPath);

    public IReadOnlyList<ReportLineViewModel> Inputs { get; }

    public int InputCount => Inputs.Count;

    public IReadOnlyList<ReportInputGroupViewModel> InputGroups { get; }

    public bool HasInputGroups => InputGroups.Count > 0;

    public bool HasInputs => Inputs.Count > 0;

    public bool HasNoInputs => !HasInputs;

    public IReadOnlyList<ReportLineViewModel> Operations { get; }

    public int OperationCount => Operations.Count;

    public IReadOnlyList<ReportOperationFlowNodeViewModel> OperationFlow { get; }

    /// <summary>Bounded operation-flow nodes rendered by the Operations tab.</summary>
    public ReportPagedListViewModel OperationFlowPage { get; }

    public bool HasOperationFlow => OperationFlow.Count > 0;

    public bool HasOperations => Operations.Count > 0;

    public bool HasNoOperations => !HasOperationFlow && !HasOperations;

    public IReadOnlyList<ReportPostbuildInvocationViewModel> PostbuildInvocations { get; }

    /// <summary>Bounded postbuild invocation rows rendered by the Postbuild tab.</summary>
    public ReportPagedListViewModel PostbuildInvocationPage { get; }

    public int PostbuildInvocationCount => PostbuildInvocations.Count;

    public bool HasPostbuildInvocations => PostbuildInvocations.Count > 0;

    public bool HasNoPostbuildInvocations => !HasPostbuildInvocations;

    public IReadOnlyList<ReportLineViewModel> StepOperations { get; }

    /// <summary>Bounded operation-detail rows rendered by the Operations tab.</summary>
    public ReportPagedListViewModel StepOperationPage { get; }

    public int StepOperationCount => StepOperations.Count;

    public bool HasStepOperations => StepOperations.Count > 0;

    /// <summary>Mutation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Mutations { get; }

    /// <summary>Bounded mutation rows rendered by the Changes tab.</summary>
    public ReportPagedListViewModel MutationPage { get; }

    /// <summary>Number of mutation rows.</summary>
    public int MutationCount => Mutations.Count;

    /// <summary>True when mutation details are available.</summary>
    public bool HasMutations => Mutations.Count > 0;

    public IReadOnlyList<ReportLineViewModel> OutputDifferences { get; }

    public int OutputDifferenceCount => _outputDifferenceProjection.Count;

    internal int MaterializedOutputDifferenceCount => _outputDifferenceProjection.MaterializedCount;

    /// <summary>Final output differences grouped by physical report section.</summary>
    public IReadOnlyList<ReportDifferenceGroupViewModel> OutputDifferenceGroups { get; }

    /// <summary>Bounded output-difference section groups rendered by the Changes tab.</summary>
    public ReportPagedListViewModel OutputDifferenceGroupPage { get; }

    public bool HasOutputDifferences => _outputDifferenceProjection.Count > 0;

    /// <summary>Bounded complete or fallback Hex Diff review state.</summary>
    public ReportHexDiffViewModel HexDiff { get; }

    /// <summary>True when no output differences or changed ranges are available.</summary>
    public bool HasNoByteChanges => !HasOutputDifferences && !HasMutations;

    public IReadOnlyList<ReportDifferenceSummaryRowViewModel> OutputDifferenceSummaryRows { get; }

    /// <summary>Bounded primary difference-summary rows rendered by the modal.</summary>
    public ReportPagedListViewModel OutputDifferenceSummaryPage { get; }

    public string ByteDifferenceTitle { get; }

    public string ByteDifferenceDetail { get; }

    public string ByteDifferenceMeta { get; }

    public IReadOnlyList<ReportLineViewModel> Issues { get; }

    /// <summary>Bounded issue rows rendered by the Issues tab.</summary>
    public ReportPagedListViewModel IssuePage { get; }

    public int IssueCount => Issues.Count;

    public bool HasIssues => Issues.Count > 0;

    public bool HasNoIssues => !HasIssues;

    public int WarningCount => CountWarnings(Issues);

    public bool HasWarnings => WarningCount > 0;

    public int BlockingIssueCount => CountBlockingIssues(Issues);

    public bool HasWarningsWithoutBlockingIssues => HasWarnings && !HasPrimaryIssue;

    public ReportLineViewModel PrimaryIssue { get; }

    public bool HasPrimaryIssue => BlockingIssueCount > 0;

    public bool HasNoPrimaryIssue => !HasPrimaryIssue;

    public bool IsClean => !HasPrimaryIssue && !HasWarnings;

    /// <summary>Primary report outcome shown before detailed evidence.</summary>
    public string OutcomeTitle { get; }

    public string OutcomeDetail { get; }

    public string OutcomeMeta { get; }

    public string OutcomeIcon { get; }

    public string OutcomeAccessibilityLabel { get; }

    public string NextStepTitle { get; }

    public string NextStepDetail { get; }

}
