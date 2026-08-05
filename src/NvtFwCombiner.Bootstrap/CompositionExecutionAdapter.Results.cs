using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static WorkbenchRunResult ToWorkbenchRunResult(
        CompositionRunResult result,
        IReadOnlyList<DeliveryArtifactSummary>? deliveryArtifacts = null,
        IReadOnlyList<CompositionIssue>? additionalIssues = null,
        IReadOnlyList<WorkbenchDeliveryArtifact>? deliveredArtifacts = null,
        bool isDeliveryComplete = true,
        string? deliveryFailureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        CompositionRunReport report = deliveryArtifacts is null && additionalIssues is null
            ? result.Report
            : result.Report.WithDeliveryArtifacts(deliveryArtifacts ?? [], additionalIssues);
        string reportJson = SerializeReport(report, suppressOutput: false);
        return new WorkbenchRunResult(
            result.Status == CompositionExecutionStatus.Succeeded,
            result.Status.ToString(),
            report.ProfileId,
            report.Output.Size,
            report.Output.Sha256,
            report.Output.FileName,
            result.CommittedOutputId,
            reportJson)
        {
            InspectionSnapshot = result.InspectionSnapshot,
            OutputBytes = result.Status == CompositionExecutionStatus.Succeeded
                ? result.OutputBytes
                : ReadOnlyMemory<byte>.Empty,
            OutputNaming = report.OutputNaming,
            DeliveryArtifacts = deliveredArtifacts is null ? [] : [.. deliveredArtifacts],
            IsDeliveryComplete = isDeliveryComplete,
            DeliveryFailureMessage = deliveryFailureMessage,
            PreviewToken = result.PreviewToken,
        };
    }

    internal static WorkbenchRunResult CreateReplaceReportRunResult(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        GeneralAuthoringAdmissionResult? generalAdmission = null,
        GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null)
    {
        return CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            FormatWorkbenchRunAction(build),
            operations,
            issues,
            outputFileName,
            generalAdmission,
            diagnosticPreview);
    }

    internal static WorkbenchRunResult CreateReplaceReportRunResult(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        string runAction,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        GeneralAuthoringAdmissionResult? generalAdmission = null,
        GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null)
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
            runAction,
            operations,
            issues,
            outputFileName,
            generalAdmission: generalAdmission,
            diagnosticPreview: diagnosticPreview,
            status: diagnosticPreview is null
                ? "Blocked"
                : "DiagnosticPlanOnly");
    }

    internal static WorkbenchRunResult CreateReplaceReadinessOnlyResult(
        string icId,
        string replaceMode,
        CapabilityActionReadinessSnapshot readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        return new WorkbenchRunResult(
            Succeeded: false,
            Status: "BuildUnavailable",
            ProfileId:
                $"{icId.ToLowerInvariant()}-{replaceMode.ToLowerInvariant()}-replace-workbench",
            OutputSize: 0,
            OutputSha256: string.Empty,
            OutputFileName: string.Empty,
            CommittedOutputId: null,
            ReportJson: string.Empty)
        {
            ActionReadiness = readiness,
        };
    }

    internal static IReadOnlyList<OperationRunSummary> CreateExplicitMappingPlanningOperations(
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

    internal static WorkbenchRunResult CreateBlockedReportRunResult(
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
        string outputFileName,
        GeneralAuthoringAdmissionResult? generalAdmission = null,
        ImageInitializationSummary? imageInitialization = null,
        GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null,
        string status = "Blocked")
    {
        return CreateBlockedReportRunResult(
            runIdPrefix,
            profileId,
            profileVersion,
            icId,
            modeId,
            experienceId,
            compositionKind,
            slotPaths,
            FormatWorkbenchRunAction(build),
            operations,
            issues,
            outputFileName,
            generalAdmission,
            imageInitialization,
            diagnosticPreview,
            status);
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
        string runAction,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        GeneralAuthoringAdmissionResult? generalAdmission = null,
        ImageInitializationSummary? imageInitialization = null,
        GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null,
        string status = "Blocked")
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var report = new CompositionRunReport(
            $"{runIdPrefix}-{runAction}-{FormatWorkbenchRunTimestamp(timestamp)}",
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
            new OutputArtifactSummary(outputFileName, 0, EmptySha256, committed: false),
            generalAdmission: generalAdmission?.ToSummary(),
            imageInitialization: imageInitialization,
            diagnosticPreview: diagnosticPreview);
        bool suppressOutput = diagnosticPreview is not null;
        string reportJson = SerializeReport(report, suppressOutput);
        return new WorkbenchRunResult(
            false,
            status,
            profileId,
            0,
            suppressOutput ? string.Empty : EmptySha256,
            suppressOutput ? string.Empty : outputFileName,
            null,
            reportJson);
    }

    private static List<InputArtifactSummary> CreateInputSummaries(
        IReadOnlyDictionary<string, string> slotPaths)
    {
        List<InputArtifactSummary> summaries = [];
        foreach ((string slotId, string path) in slotPaths.OrderBy(
            static item => item.Key,
            StringComparer.Ordinal))
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                summaries.Add(new InputArtifactSummary(
                    slotId,
                    Path.GetFileName(path),
                    0,
                    EmptySha256));
                continue;
            }

            FileInfo file = new(fullPath);
            summaries.Add(new InputArtifactSummary(
                slotId,
                Path.GetFileName(fullPath),
                file.Length,
                WorkbenchArtifactIdentity.Sha256File(fullPath)));
        }

        return summaries;
    }

    private static string SerializeReport(
        CompositionRunReport report,
        bool suppressOutput)
    {
        if (!suppressOutput)
        {
            return JsonSerializer.Serialize(report, ReportJsonOptions);
        }

        JsonObject projection =
            JsonSerializer.SerializeToNode(report, ReportJsonOptions)!
                .AsObject();
        projection[nameof(CompositionRunReport.Output)] = null;
        return projection.ToJsonString(ReportJsonOptions);
    }
}
