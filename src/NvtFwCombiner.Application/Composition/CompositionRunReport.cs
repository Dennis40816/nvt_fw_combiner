using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.Authoring;

using System.Text.Json.Serialization;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application run summary for one preview or build; not the canonical composition-report-v1 wire contract.</summary>
public sealed class CompositionRunReport
{
    /// <summary>Creates a report summary.</summary>
    public CompositionRunReport(
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
        GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceId);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(mutations);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(output);
        if (compilationFingerprint is not null &&
            (compilationFingerprint.Length != 64 || !compilationFingerprint.All(static character =>
                character is (>= '0' and <= '9') or (>= 'a' and <= 'f'))))
        {
            throw new ArgumentException("Compilation fingerprint must be a lowercase SHA-256 value.", nameof(compilationFingerprint));
        }

        RunId = runId;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        IcId = icId;
        ModeId = modeId;
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Inputs = inputs;
        Operations = operations;
        Mutations = mutations;
        Issues = issues;
        Output = output;
        OutputDifferences = outputDifferences is null ? [] : [.. outputDifferences];
        CompilationFingerprint = compilationFingerprint;
        Validations = Array.AsReadOnly(validations is null ? Array.Empty<ValidationRunSummary>() : [.. validations]);
        OutputNaming = outputNaming;
        DeliveryArtifacts = deliveryArtifacts is { Count: > 0 }
            ? Array.AsReadOnly([.. deliveryArtifacts])
            : null;
        GeneralAdmission = generalAdmission;
        ImageInitialization = imageInitialization;
        DiagnosticPreview = diagnosticPreview;
    }

    /// <summary>Stable run id.</summary>
    public string RunId { get; }

    /// <summary>Profile id used for the run.</summary>
    public string ProfileId { get; }

    /// <summary>Profile version used for the run.</summary>
    public string ProfileVersion { get; }

    /// <summary>IC id declared by the profile.</summary>
    public string IcId { get; }

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId { get; }

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId { get; }

    /// <summary>Merge or replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>UTC timestamp when the run started.</summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>UTC timestamp when the run completed.</summary>
    public DateTimeOffset CompletedAtUtc { get; }

    /// <summary>Input artifact summaries without portable paths.</summary>
    public IReadOnlyList<InputArtifactSummary> Inputs { get; }

    /// <summary>Operation statuses in plan order.</summary>
    public IReadOnlyList<OperationRunSummary> Operations { get; }

    /// <summary>Application mutation summaries mapped from the shared engine.</summary>
    public IReadOnlyList<MutationRunSummary> Mutations { get; }

    /// <summary>Structured issues emitted during the run.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>Output artifact summary.</summary>
    public OutputArtifactSummary Output { get; }

    /// <summary>Replace final-output differences compared with the reference base.</summary>
    public IReadOnlyList<OutputDifferenceSummary> OutputDifferences { get; }

    /// <summary>Compiled artifact fingerprint that binds V2 bundle, profile, map, and execution facts when available.</summary>
    public string? CompilationFingerprint { get; }

    /// <summary>Compiled validation outcomes retained independently from operation execution.</summary>
    public IReadOnlyList<ValidationRunSummary> Validations { get; }

    /// <summary>Output-name rendering provenance when a typed renderer resolved the automatic name.</summary>
    public OutputNamingSummary? OutputNaming { get; }

    /// <summary>Additional artifacts delivered from the completed primary composition output.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DeliveryArtifactSummary>? DeliveryArtifacts { get; }

    /// <summary>General authoring admission provenance when this run uses a General route.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeneralAuthoringAdmissionSummary? GeneralAdmission { get; }

    /// <summary>Exact compiled output capacity/fill or reference-clone provenance.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageInitializationSummary? ImageInitialization { get; }

    /// <summary>Plan-only General Replace marker when POSTBUILD cannot execute.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeneralReplaceDiagnosticPreviewSummary? DiagnosticPreview { get; }

}
