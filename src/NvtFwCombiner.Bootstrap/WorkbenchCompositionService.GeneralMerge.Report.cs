using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchRunResult CreateGeneralMergeReportRunResult(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        bool succeeded,
        string profileId,
        string profileVersion)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var report = new CompositionRunReport(
            CreateWorkbenchReportRunId(GeneralMergeRunIdPrefix, build, timestamp),
            profileId,
            profileVersion,
            icId,
            IcWorkflowIds.GeneralMerge,
            IcWorkflowIds.GeneralMerge,
            CompositionKind.Merge,
            timestamp,
            timestamp,
            CreateInputSummaries(slotPaths),
            operations,
            [],
            issues,
            new OutputArtifactSummary(outputFileName, 0, EmptySha256, committed: false));
        string reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
        return new WorkbenchRunResult(
            succeeded,
            succeeded ? "Succeeded" : "Blocked",
            profileId,
            0,
            EmptySha256,
            outputFileName,
            null,
            reportJson);
    }
}
