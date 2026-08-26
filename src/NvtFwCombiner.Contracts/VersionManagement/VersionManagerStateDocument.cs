using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.VersionManagement;

/// <summary>Launcher state v1 transport stored separately from shell preferences.</summary>
public sealed record VersionManagerStateDocument(
    int SchemaVersion,
    string? UpdateSource,
    string? ActiveVersion,
    string? LastKnownGoodVersion,
    IReadOnlyList<ManagedVersionAdmissionDocument?>? Admissions,
    PendingVersionActivationDocument? PendingActivation,
    string? FailedActivationVersion,
    bool RetentionReviewDue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PendingManagedVersionMutationDocument? PendingMutation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ManagedRootIdentity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    VersionSourceRegistryStateDocument? SourceRegistryState = null);

/// <summary>Durable fixed-registry authority stored atomically with the effective source.</summary>
public sealed record VersionSourceRegistryStateDocument(
    long AcceptedRevision,
    string? AcceptedDigest,
    bool IsManualPin);

/// <summary>One installed managed-version admission transport.</summary>
public sealed record ManagedVersionAdmissionDocument(
    string? Version,
    string? AdmissionIdentity,
    string? ReleaseManifestSha256);

/// <summary>One recoverable pending activation transport.</summary>
public sealed record PendingVersionActivationDocument(
    string? CandidateVersion,
    string? CandidateAdmissionIdentity,
    string? PreviousActiveVersion,
    string? PreviousLastKnownGoodVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Phase = null);

/// <summary>One recoverable filesystem/state mutation transport.</summary>
public sealed record PendingManagedVersionMutationDocument(
    string? Kind,
    ManagedVersionAdmissionDocument? Admission);
