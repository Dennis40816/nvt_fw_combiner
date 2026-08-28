using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Projected reference disposition without executing any bytes.</summary>
public enum PlanOnlyCoverageDisposition
{
    /// <summary>Reference bytes remain untouched by an accepted mapping.</summary>
    Kept,

    /// <summary>An accepted mapping plans to replace this reference range.</summary>
    Changed,
}

/// <summary>One coherent range in a plan-only Replace coverage projection.</summary>
public sealed record PlanOnlyCoverageSegment(
    ByteRange Range,
    PlanOnlyCoverageDisposition Disposition,
    string? MappingId);

/// <summary>Explicit non-executing report marker for POSTBUILD-unavailable Preview.</summary>
/// <param name="RequiredStageId">Exact compiled stage id, or null when Parent stage authority is absent.</param>
/// <param name="Blocker">The same highest-priority typed blocker used by Build readiness.</param>
/// <param name="Coverage">Complete projected Kept/Changed reference coverage.</param>
public sealed record GeneralReplaceDiagnosticPreviewSummary(
    string? RequiredStageId,
    CapabilityActionBlocker Blocker,
    IReadOnlyList<PlanOnlyCoverageSegment> Coverage)
{
    /// <summary>Stable report mode that cannot be confused with executable Preview.</summary>
    public string Mode { get; } = "diagnostic-plan-only";

    /// <summary>Required operator-facing statement from the accepted contract.</summary>
    public string Message { get; } =
        "POSTBUILD required but unavailable — plan only; no output was produced.";

    /// <summary>True because this diagnostic exists only for POSTBUILD-dependent targets.</summary>
    public bool PostbuildRequired { get; } = true;

    /// <summary>No output artifact exists for this result.</summary>
    public bool OutputProduced { get; }

    /// <summary>No Header, CRC, hash, or final-output validity is claimed.</summary>
    public bool ClaimsFinalIntegrity { get; }
}

/// <summary>Projects one coherent General Replace diagnostic without reading or mutating bytes.</summary>
public static class GeneralReplaceDiagnosticPreviewProjector
{
    /// <summary>Creates the report projection from admitted mappings and shared action readiness.</summary>
    public static GeneralReplaceDiagnosticPreviewSummary Project(
        long referenceCapacity,
        GeneralAuthoringAdmissionResult admission,
        CapabilityActionReadinessSnapshot readiness,
        string? requiredStageId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(referenceCapacity, 1);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(readiness);
        CapabilityActionBlocker blocker = readiness.Build.PrimaryBlocker ??
            throw new ArgumentException(
                "Diagnostic Preview requires one Build blocker.",
                nameof(readiness));
        bool missingStage = StringComparer.Ordinal.Equals(
            blocker.Code,
            CapabilityActionReadinessIssueCodes.PostbuildStageAuthorityMissing);
        bool runtimeUnavailable =
            blocker.Dimension == CapabilityReadinessDimension.RuntimeDependency;
        if (!admission.IsAdmitted ||
            !readiness.Preview.IsAvailable ||
            readiness.Build.IsAvailable ||
            (!missingStage && !runtimeUnavailable) ||
            (missingStage != string.IsNullOrWhiteSpace(requiredStageId)))
        {
            throw new ArgumentException(
                "Diagnostic Preview requires coherent admitted mappings and one exact POSTBUILD blocker.");
        }

        List<PlanOnlyCoverageSegment> coverage = [];
        long cursor = 0;
        foreach (GeneralOccupancySegment segment in admission.OccupancySegments)
        {
            if (!StringComparer.Ordinal.Equals(
                    segment.TargetAddressSpaceId,
                    CompositionAddressSpaceIds.OutputImage) ||
                segment.TargetRange.Start < cursor ||
                segment.TargetRange.EndExclusive > referenceCapacity)
            {
                throw new ArgumentException(
                    "Diagnostic Preview occupancy must be ordered, non-overlapping output-image coverage.",
                    nameof(admission));
            }

            if (cursor < segment.TargetRange.Start)
            {
                coverage.Add(new PlanOnlyCoverageSegment(
                    new ByteRange(cursor, segment.TargetRange.Start - cursor),
                    PlanOnlyCoverageDisposition.Kept,
                    null));
            }

            coverage.Add(new PlanOnlyCoverageSegment(
                segment.TargetRange,
                PlanOnlyCoverageDisposition.Changed,
                segment.MappingId));
            cursor = segment.TargetRange.EndExclusive;
        }

        if (cursor < referenceCapacity)
        {
            coverage.Add(new PlanOnlyCoverageSegment(
                new ByteRange(cursor, referenceCapacity - cursor),
                PlanOnlyCoverageDisposition.Kept,
                null));
        }

        return new GeneralReplaceDiagnosticPreviewSummary(
            requiredStageId,
            blocker,
            coverage.AsReadOnly());
    }
}

/// <summary>Creates the canonical typed report for one non-executing General Replace Preview.</summary>
public static class GeneralReplaceDiagnosticPreviewReportProjector
{
    private const string EmptySha256 =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>Projects an accepted plan-only outcome without reopening inputs or executing bytes.</summary>
    public static CompositionRunReport Project(
        ActiveSessionSnapshot acceptedSession,
        GeneralAuthoringAdmissionResult admission,
        GeneralReplaceDiagnosticPreviewSummary diagnostic,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(diagnostic);
        ResolvedCapability capability = acceptedSession.GetAcceptedCapability(
                AuthoringDerivedResultKind.Validation) ??
            throw new ArgumentException(
                "Diagnostic Preview requires one exact accepted General Replace capability.",
                nameof(acceptedSession));
        CompiledComposition composition = capability.CompiledComposition;
        InputArtifactSummary[] inputs =
        [
            .. acceptedSession.Slots
                .Where(static slot => slot.FileStamp is not null)
                .OrderBy(static slot => slot.DefinitionId, StringComparer.Ordinal)
                .Select(static slot => new InputArtifactSummary(
                    slot.DefinitionId,
                    slot.DefinitionId,
                    slot.FileStamp!.Value.AcceptedLength,
                    slot.FileStamp.Value.Sha256,
                    slot.SelectedPath is null ? null : Path.GetFileName(slot.SelectedPath))),
        ];
        OperationRunSummary[] operations =
        [
            .. composition.Plan.OrderedOperations
                .Where(static operation =>
                    operation.Kind == CompositionOperationKind.ReplaceRange)
                .Select(CompositionRunService.ToPlanningOperationSummary),
        ];
        CompositionIssue issue = new(
            diagnostic.Blocker.Code,
            diagnostic.Blocker.Message,
            diagnostic.Blocker.SubjectId);
        return new CompositionRunReport(
            $"ui-replace-general-preview-{timestamp.ToUnixTimeMilliseconds()}",
            composition.V2Details.ProfileId,
            composition.V2Details.ProfileVersion,
            capability.Identity.IcId,
            capability.Identity.WorkflowId,
            capability.Identity.WorkflowId,
            composition.V2Details.CompositionKind,
            timestamp,
            timestamp,
            inputs,
            operations,
            mutations: [],
            issues: [issue],
            new OutputArtifactSummary(
                composition.V2Details.OutputNamingRequirement.FileNameTemplate,
                size: 0,
                EmptySha256,
                committed: false),
            compilationFingerprint: composition.CompilationFingerprint,
            generalAdmission: admission.ToSummary(),
            imageInitialization: ImageInitializationSummary.FromCompiled(
                composition.Plan.OutputInitialization),
            diagnosticPreview: diagnostic,
            resolvedMapId: composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
    }
}
