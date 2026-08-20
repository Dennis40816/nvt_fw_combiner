using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.Authoring;

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
    CompositionOutputBundleDeliverySummary? bundleDelivery = null)
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
}
