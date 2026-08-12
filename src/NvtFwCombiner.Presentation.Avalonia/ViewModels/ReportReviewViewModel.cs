namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Readable UI projection of a CLI/application run report JSON file.</summary>
public sealed partial class ReportReviewViewModel
{
    private readonly bool? outputCommitted;
    private readonly OutputDifferenceProjection _outputDifferenceProjection;

    internal ReportReviewViewModel(
        bool isEmpty,
        string sourceName,
        long? reportJsonUtf8ByteCount,
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
        CompositionRunInspectionSnapshot? inspectionSnapshot,
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        OutputDifferenceProjection outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language = ShellLanguage.English)
    {
        IsEmpty = isEmpty;
        SourceName = sourceName;
        ReportJsonUtf8ByteCount = reportJsonUtf8ByteCount;
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
        this.outputCommitted = outputCommitted;
        IsOutputNotGenerated = outputCommitted == false &&
            outputSize <= 0 &&
            issues.Any(issue => !IsWarning(issue));
        OutputSizeLabel = CreateOutputSizeLabel(outputSize, IsOutputNotGenerated, language);
        OutputCommitmentLabel = CreateOutputCommitmentLabel(outputCommitted, IsOutputNotGenerated, language);
        OutputSha256 = outputSha256;
        OutputHashLabel = IsOutputNotGenerated || string.IsNullOrWhiteSpace(outputSha256)
            ? T(language, "No output hash", "無輸出雜湊")
            : Shorten(outputSha256, 16);
        OutputArtifactPath = string.IsNullOrWhiteSpace(outputArtifactPath) ? string.Empty : outputArtifactPath;
        InspectionSnapshot = inspectionSnapshot;
        Inputs = inputs;
        Operations = operations;
        StepOperations = [.. operations.Where(operation => !operation.HasCodeBlock && !operation.HasRuntimeCommands)];
        PostbuildInvocations = CreatePostbuildInvocations(operations, language);
        Mutations = mutations;
        _outputDifferenceProjection = outputDifferences;
        HexDiff = ReportHexDiffViewModel.Create(
            inspectionSnapshot,
            outputDifferences.HexDiffSource,
            runId,
            outputSize,
            outputSha256,
            language);
        OutputDifferences = outputDifferences.Rows;
        OutputDifferenceGroups = outputDifferences.Groups;
        Issues = issues;
        PrimaryIssue = issues.FirstOrDefault(issue => !IsWarning(issue)) ?? ReportLineViewModel.Empty;
        InputGroups = CreateInputGroups(inputs, language);
        OperationFlow = CreateOperationFlow(inputs, operations, outputFileName, status, language);
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
            ? Issues.First(IsWarning).Detail
            : CreateCleanNextStepDetail(outputDifferences, operations, language);
        ByteDifferenceTitle = CreateByteDifferenceTitle(compositionKind, outputDifferences, language);
        ByteDifferenceDetail = CreateByteDifferenceDetail(compositionKind, outputDifferences, language);
        ByteDifferenceMeta = CreateByteDifferenceMeta(outputDifferences, language);
        OutputDifferenceSummaryRows = outputDifferences.SummaryRows;
        AuditSummary = CreateAuditSummary(inputs, operations, mutations, outputDifferences.Count, issues, language);
        OutputDifferenceSummaryPage = ReportPagedListViewModel.Create(OutputDifferenceSummaryRows, 8, language);
        OutputDifferenceGroupPage = ReportPagedListViewModel.Create(OutputDifferenceGroups, 8, language);
        MutationPage = ReportPagedListViewModel.Create(Mutations, 40, language);
        OperationFlowPage = ReportPagedListViewModel.Create(OperationFlow, 24, language);
        StepOperationPage = ReportPagedListViewModel.Create(StepOperations, 24, language);
        PostbuildInvocationPage = ReportPagedListViewModel.Create(PostbuildInvocations, 24, language);
        IssuePage = ReportPagedListViewModel.Create(Issues, 40, language);
    }

    /// <summary>Empty report sentinel.</summary>
    public static ReportReviewViewModel Empty { get; } = new(
        true,
        string.Empty,
        null,
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
        null,
        [],
        [],
        [],
        OutputDifferenceProjection.Empty,
        []);

    /// <summary>UTF-8 report size already observed by the background JSON projection.</summary>
    internal long? ReportJsonUtf8ByteCount { get; }
}
