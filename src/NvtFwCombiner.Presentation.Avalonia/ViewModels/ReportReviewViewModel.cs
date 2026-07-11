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
        bool? outputCommitted,
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
        OutputCommitted = outputCommitted;
        IsOutputCommitted = outputCommitted == true;
        IsOutputPreview = outputCommitted == false;
        IsOutputStateUnknown = outputCommitted is null;
        OutputSizeLabel = CreateOutputSizeLabel(outputSize, language);
        OutputCommitmentLabel = CreateOutputCommitmentLabel(outputCommitted, language);
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
        PostbuildInvocations = CreatePostbuildInvocations(operations, language);
        Mutations = mutations;
        OutputDifferences = outputDifferences;
        OutputDifferenceGroups = CreateOutputDifferenceGroups(outputDifferences, language);
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
        HasPostbuildInvocations = PostbuildInvocations.Count > 0;
        HasStepOperations = StepOperations.Count > 0;
        HasMutations = mutations.Count > 0;
        HasOutputDifferences = outputDifferences.Count > 0;
        HasOutputDifferenceGroups = OutputDifferenceGroups.Count > 0;
        HasIssues = issues.Count > 0;
        HasNoInputs = !HasInputs;
        HasNoCommandOperations = !HasCommandOperations;
        HasNoPostbuildInvocations = !HasPostbuildInvocations;
        HasNoStepOperations = !HasStepOperations;
        HasNoMutations = !HasMutations;
        HasNoOutputDifferences = !HasOutputDifferences;
        HasNoIssues = !HasIssues;
        HasNoByteChanges = !HasOutputDifferences && !HasMutations;
        HasOutputFileName = !string.IsNullOrWhiteSpace(outputFileName) &&
            !string.Equals(outputFileName, "No output", StringComparison.OrdinalIgnoreCase);
        SummaryRows = CreateSummaryRows(status, output, inputs, operations, mutations, issues, language);
        OutcomeTitle = CreateOutcomeTitle(status, issues, language);
        OutcomeDetail = CreateOutcomeDetail(output, issues, compositionKind, outputDifferences, language);
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
                : CreateCleanNextStepTitle(outputDifferences, language);
        NextStepDetail = HasPrimaryIssue
            ? CreateIssueAction(PrimaryIssue, language)
            : HasWarnings
            ? Warnings[0].Detail
            : CreateCleanNextStepDetail(outputDifferences, operations, language);
        ByteDifferenceTitle = CreateByteDifferenceTitle(compositionKind, outputDifferences, language);
        ByteDifferenceDetail = CreateByteDifferenceDetail(compositionKind, outputDifferences, language);
        ByteDifferenceMeta = CreateByteDifferenceMeta(outputDifferences, language);
        OutputDifferenceSummaryRows = CreateOutputDifferenceSummaryRows(outputDifferences, language);
        AuditSummary = CreateAuditSummary(inputs, operations, mutations, outputDifferences, issues, language);
        TriageRows = CreateTriageRows(status, output, operations, issues, language);
        EvidenceRows = CreateEvidenceRows(inputs, operations, mutations, outputDifferences, issues, language);
    }

    /// <summary>Empty report sentinel.</summary>
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
        null,
        string.Empty,
        string.Empty,
        [],
        [],
        [],
        [],
        []);
}
