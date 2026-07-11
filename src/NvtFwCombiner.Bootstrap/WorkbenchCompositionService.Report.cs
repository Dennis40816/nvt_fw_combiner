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
        string outputFileName,
        bool succeeded)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string profileId = $"{icId.ToLowerInvariant()}-{replaceMode.ToLowerInvariant()}-replace-workbench";
        var report = new CompositionRunReport(
            CreateWorkbenchReportRunId(GetReplaceRunIdPrefix(replaceMode), build, timestamp),
            profileId,
            "0.5.0",
            icId,
            $"{replaceMode.ToLowerInvariant()}-replace",
            $"{replaceMode.ToLowerInvariant()}-replace",
            CompositionKind.Replace,
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
