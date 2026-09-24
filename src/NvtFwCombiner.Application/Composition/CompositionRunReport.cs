using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.Authoring;

using System.Collections.ObjectModel;

using System.Text.Json.Serialization;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application run summary for one preview or build; not the canonical composition-report-v1 wire contract.</summary>
public sealed class CompositionRunReport(
    string runId,
    string profileId,
    string profileVersion,
    string icId,
    string modeId,
    string experienceId,
    CompositionKind compositionKind,
    DateTimeOffset startedAtUtc,
    DateTimeOffset completedAtUtc,
    IReadOnlyList<InputArtifactSummary> inputs,
    IReadOnlyList<OperationRunSummary> operations,
    IReadOnlyList<MutationRunSummary> mutations,
    IReadOnlyList<CompositionIssue> issues,
    OutputArtifactSummary output,
    IReadOnlyList<OutputDifferenceSummary>? outputDifferences = null,
    string? compilationFingerprint = null,
    IReadOnlyList<ValidationRunSummary>? validations = null,
    OutputNamingSummary? outputNaming = null,
    IReadOnlyList<DeliveryArtifactSummary>? deliveryArtifacts = null,
    GeneralAuthoringAdmissionSummary? generalAdmission = null,
    ImageInitializationSummary? imageInitialization = null,
    GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null,
    CompositionOutputBundleDeliverySummary? bundleDelivery = null,
    string? resolvedMapId = null,
    IReadOnlyList<InputDiagnosticSummary>? inputDiagnostics = null,
    AbMergeFormatRunSummary? abMergeFormat = null,
    SourceEnvelopeRunSummary? sourceEnvelope = null)
{
    /// <summary>Stable run id.</summary>
    public string RunId { get; } = CompositionSummaryValue.NotBlank(runId, nameof(runId));

    /// <summary>Profile id used for the run.</summary>
    public string ProfileId { get; } = CompositionSummaryValue.NotBlank(profileId, nameof(profileId));

    /// <summary>Profile version used for the run.</summary>
    public string ProfileVersion { get; } = CompositionSummaryValue.NotBlank(
        profileVersion,
        nameof(profileVersion));

    /// <summary>IC id declared by the profile.</summary>
    public string IcId { get; } = CompositionSummaryValue.NotBlank(icId, nameof(icId));

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId { get; } = CompositionSummaryValue.NotBlank(modeId, nameof(modeId));

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId { get; } = CompositionSummaryValue.NotBlank(
        experienceId,
        nameof(experienceId));

    /// <summary>Merge or replace composition kind.</summary>
    public CompositionKind CompositionKind { get; } = compositionKind;

    /// <summary>UTC timestamp when the run started.</summary>
    public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;

    /// <summary>UTC timestamp when the run completed.</summary>
    public DateTimeOffset CompletedAtUtc { get; } = completedAtUtc;

    /// <summary>Input artifact summaries without portable paths.</summary>
    public IReadOnlyList<InputArtifactSummary> Inputs { get; } = CompositionSummaryValue.Snapshot(
        inputs,
        nameof(inputs));

    /// <summary>Operation statuses in plan order.</summary>
    public IReadOnlyList<OperationRunSummary> Operations { get; } = CompositionSummaryValue.Snapshot(
        operations,
        nameof(operations));

    /// <summary>Application mutation summaries mapped from the shared engine.</summary>
    public IReadOnlyList<MutationRunSummary> Mutations { get; } = CompositionSummaryValue.Snapshot(
        mutations,
        nameof(mutations));

    /// <summary>Structured issues emitted during the run.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; } = CompositionSummaryValue.Snapshot(
        issues,
        nameof(issues));

    /// <summary>Output artifact summary.</summary>
    public OutputArtifactSummary Output { get; } = CompositionSummaryValue.NotNull(output, nameof(output));

    /// <summary>Replace final-output differences compared with the reference base.</summary>
    public IReadOnlyList<OutputDifferenceSummary> OutputDifferences { get; } = outputDifferences is null
        ? []
        : [.. outputDifferences];

    /// <summary>Compiled artifact fingerprint that binds V2 bundle, profile, map, and execution facts when available.</summary>
    public string? CompilationFingerprint { get; } = RequireCompilationFingerprint(compilationFingerprint);

    /// <summary>Exact firmware map selected by the compiled typed route.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MapId { get; } = resolvedMapId is null
        ? null
        : CompositionSummaryValue.NotBlank(resolvedMapId, nameof(resolvedMapId));

    /// <summary>Actual DP/output extent and canonical layout template, omitted for exact map runs.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceEnvelopeRunSummary? SourceEnvelope { get; } = sourceEnvelope;

    /// <summary>Compiled validation outcomes retained independently from operation execution.</summary>
    public IReadOnlyList<ValidationRunSummary> Validations { get; } = Array.AsReadOnly(
        validations is null ? Array.Empty<ValidationRunSummary>() : [.. validations]);

    /// <summary>Output-name rendering provenance when a typed renderer resolved the automatic name.</summary>
    public OutputNamingSummary? OutputNaming { get; } = outputNaming;

    /// <summary>Additional artifacts delivered from the completed primary composition output.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DeliveryArtifactSummary>? DeliveryArtifacts { get; } = deliveryArtifacts is { Count: > 0 }
        ? Array.AsReadOnly([.. deliveryArtifacts])
        : null;

    /// <summary>General authoring admission provenance when this run uses a General route.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeneralAuthoringAdmissionSummary? GeneralAdmission { get; } = generalAdmission;

    /// <summary>Exact compiled output capacity/fill or reference-clone provenance.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageInitializationSummary? ImageInitialization { get; } = imageInitialization;

    /// <summary>Plan-only General Replace marker when POSTBUILD cannot execute.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeneralReplaceDiagnosticPreviewSummary? DiagnosticPreview { get; } = diagnosticPreview;

    /// <summary>Actual atomic output bundle delivery, omitted for Preview and loose output.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompositionOutputBundleDeliverySummary? BundleDelivery { get; } = bundleDelivery;

    /// <summary>Captured AB format evidence; absence means not recorded, never an inferred Common format.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AbMergeFormatRunSummary? AbMergeFormat { get; } = abMergeFormat;

    /// <summary>Optional immutable input evidence bound to zero-based indexes in <see cref="Issues"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<InputDiagnosticSummary>? InputDiagnostics { get; } = SnapshotInputDiagnostics(
        inputDiagnostics,
        issues);

    private static string? RequireCompilationFingerprint(string? compilationFingerprint)
    {
        return compilationFingerprint is null ||
            (compilationFingerprint.Length == 64 && compilationFingerprint.All(static character =>
                character is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
                ? compilationFingerprint
                : throw new ArgumentException(
                    "Compilation fingerprint must be a lowercase SHA-256 value.",
                    nameof(compilationFingerprint));
    }

    private static ReadOnlyCollection<InputDiagnosticSummary>? SnapshotInputDiagnostics(
        IReadOnlyList<InputDiagnosticSummary>? inputDiagnostics,
        IReadOnlyList<CompositionIssue> issues)
    {
        if (inputDiagnostics is not { Count: > 0 })
        {
            return null;
        }

        InputDiagnosticSummary[] snapshot = [.. inputDiagnostics];
        foreach (InputDiagnosticSummary diagnostic in snapshot)
        {
            if (diagnostic is null || diagnostic.IssueIndex >= issues.Count)
            {
                throw new ArgumentException(
                    "Input diagnostics must contain unique in-bounds issue indexes.",
                    nameof(inputDiagnostics));
            }
        }

        return snapshot.Select(static diagnostic => diagnostic.IssueIndex).Distinct().Count() == snapshot.Length
            ? Array.AsReadOnly(snapshot)
            : throw new ArgumentException(
                "Input diagnostics must contain unique in-bounds issue indexes.",
                nameof(inputDiagnostics));
    }
}

/// <summary>Path-free provenance for one captured DP extent compiled from a canonical layout template.</summary>
public sealed class SourceEnvelopeRunSummary
{
    internal SourceEnvelopeRunSummary(SourceEnvelopeExtent envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        SourceSlotId = envelope.SourceSlotId;
        RootRegionId = envelope.RootRegionId;
        LayoutTemplateMapId = envelope.LayoutTemplateMapId;
        LayoutTemplateCapacity = envelope.LayoutTemplateCapacity;
        ActualOutputLength = envelope.ActualOutputLength;
        ExpectedOuterLengths = Array.AsReadOnly([.. envelope.ExpectedOuterLengths]);
        UnexpectedLengthIssueCode = envelope.UnexpectedLengthIssueCode;
    }

    /// <summary>Captured DP slot that determined actual output length.</summary>
    public string SourceSlotId { get; }
    /// <summary>Canonical full-container region used for template anchors.</summary>
    public string RootRegionId { get; }
    /// <summary>Canonical map used for layout facts, not the actual output extent.</summary>
    public string LayoutTemplateMapId { get; }
    /// <summary>Canonical layout template capacity in bytes.</summary>
    public long LayoutTemplateCapacity { get; }
    /// <summary>Actual captured DP and compiled output length in bytes.</summary>
    public long ActualOutputLength { get; }
    /// <summary>Declared standard lengths used only for the advisory.</summary>
    public IReadOnlyList<long> ExpectedOuterLengths { get; }
    /// <summary>Typed nonblocking warning for this unexpected outer length.</summary>
    public string UnexpectedLengthIssueCode { get; }
}
