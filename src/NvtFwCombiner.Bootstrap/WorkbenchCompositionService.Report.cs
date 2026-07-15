using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static WorkbenchRunResult ToWorkbenchRunResult(CompositionRunResult result)
    {
        CompositionRunReport report = result.Report;
        string reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
        return new WorkbenchRunResult(
            result.Status == CompositionExecutionStatus.Succeeded,
            result.Status.ToString(),
            report.ProfileId,
            report.Output.Size,
            report.Output.Sha256,
            report.Output.FileName,
            result.CommittedOutputId,
            reportJson);
    }

    private static WorkbenchRunResult CreateReplaceReportRunResult(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName)
    {
        string profileId = $"{icId.ToLowerInvariant()}-{replaceMode.ToLowerInvariant()}-replace-workbench";
        return CreateBlockedReportRunResult(
            GetReplaceRunIdPrefix(replaceMode),
            profileId,
            "0.5.0",
            icId,
            $"{replaceMode.ToLowerInvariant()}-replace",
            $"{replaceMode.ToLowerInvariant()}-replace",
            CompositionKind.Replace,
            slotPaths,
            build,
            operations,
            issues,
            outputFileName);
    }

    private static IReadOnlyList<OperationRunSummary> CreateExplicitMappingPlanningOperations(
        IReadOnlyList<ExplicitMapping> explicitMappings,
        CompositionOperationKind operationKind)
    {
        return
        [
            .. explicitMappings.Select(mapping => new OperationRunSummary(
                mapping.MappingId,
                mapping.Sequence,
                operationKind,
                OperationRunStatus.Skipped,
                mapping.SourceBindingId,
                mapping.SourceRange,
                mapping.TargetSpaceId,
                mapping.TargetRange,
                mapping.OverlapPolicy,
                null,
                null,
                [],
                [],
                mapping.Reason,
                mapping.Provenance)),
        ];
    }

    private static WorkbenchRunResult CreateBlockedReportRunResult(
        string runIdPrefix,
        string profileId,
        string profileVersion,
        string icId,
        string modeId,
        string experienceId,
        CompositionKind compositionKind,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var report = new CompositionRunReport(
            CreateWorkbenchReportRunId(runIdPrefix, build, timestamp),
            profileId,
            profileVersion,
            icId,
            modeId,
            experienceId,
            compositionKind,
            timestamp,
            timestamp,
            CreateInputSummaries(slotPaths),
            operations,
            [],
            issues,
            new OutputArtifactSummary(outputFileName, 0, EmptySha256, committed: false));
        string reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
        return new WorkbenchRunResult(
            false,
            "Blocked",
            profileId,
            0,
            EmptySha256,
            outputFileName,
            null,
            reportJson);
    }
}
