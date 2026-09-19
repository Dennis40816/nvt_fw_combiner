namespace NvtFwCombiner.Application.Configuration;

/// <summary>Explicit source of the external runtime; no implicit fallback between sources.</summary>
public enum ToolchainRuntimeSource
{
    /// <summary>The host-owned bundled runtime.</summary>
    Bundled,
    /// <summary>An explicitly selected and hash-pinned user runtime.</summary>
    User,
}

/// <summary>Immutable requested selection; the session admits its shape and exact candidate evidence.</summary>
public sealed record ToolchainRuntimeSelection(ToolchainRuntimeSource Source, string? Path = null, string? Sha256 = null);

/// <summary>Exact path and bytes inspected by the platform, with informational version and architecture.</summary>
public sealed record ToolchainRuntimeIdentity(string Path, string Sha256, string FileVersion, string Architecture);

/// <summary>Complete explicit candidate verification outcome; empty issues alone never mean verified.</summary>
public enum ToolchainRuntimeCandidateVerification
{
    /// <summary>Complete verification has not been established.</summary>
    Unknown,
    /// <summary>The inspector completed every required candidate check.</summary>
    Verified,
    /// <summary>The candidate failed one or more checks.</summary>
    Rejected,
}

/// <summary>Stable failure code and presentation-safe explanation.</summary>
public sealed record ToolchainRuntimeConfigurationIssue(string Code, string Message);

/// <summary>Immutable candidate evidence, never authority to change the selected runtime.</summary>
public sealed class ToolchainRuntimeCandidateInspection(
    ToolchainRuntimeIdentity? identity,
    ToolchainRuntimeCandidateVerification verification,
    IReadOnlyList<ToolchainRuntimeConfigurationIssue> issues)
{
    /// <summary>Exact identity observed, if available.</summary>
    public ToolchainRuntimeIdentity? Identity { get; } = identity;
    /// <summary>Explicit complete verification result.</summary>
    public ToolchainRuntimeCandidateVerification Verification { get; } = verification;
    /// <summary>Defensively copied inspection issues.</summary>
    public IReadOnlyList<ToolchainRuntimeConfigurationIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
}

/// <summary>Whether one requested selection is currently admitted.</summary>
public enum ToolchainRuntimeConfigurationStatus
{
    /// <summary>No configuration read has completed.</summary>
    NotLoaded,
    /// <summary>The published selection was admitted.</summary>
    Current,
    /// <summary>The requested selection cannot be admitted; no fallback is active.</summary>
    Blocked,
}

/// <summary>One immutable configuration publication, retaining requested identity on blocked reloads.</summary>
public sealed class ToolchainRuntimeConfigurationSnapshot(
    long generation,
    ToolchainRuntimeConfigurationStatus status,
    ToolchainRuntimeSelection? requestedSelection,
    ToolchainRuntimeSelection? selection,
    IReadOnlyList<ToolchainRuntimeConfigurationIssue> issues)
{
    /// <summary>Monotonically increasing publication generation.</summary>
    public long Generation { get; } = generation;
    /// <summary>Current admission status.</summary>
    public ToolchainRuntimeConfigurationStatus Status { get; } = status;
    /// <summary>Requested persisted identity, retained when it cannot be admitted.</summary>
    public ToolchainRuntimeSelection? RequestedSelection { get; } = requestedSelection;
    /// <summary>Admitted selection, or null while not loaded or blocked.</summary>
    public ToolchainRuntimeSelection? Selection { get; } = selection;
    /// <summary>Defensively copied admission issues.</summary>
    public IReadOnlyList<ToolchainRuntimeConfigurationIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
}

/// <summary>Operation outcome; a failed Save reports issues without replacing the current publication.</summary>
public sealed class ToolchainRuntimeConfigurationOperationResult(
    ToolchainRuntimeConfigurationSnapshot snapshot,
    bool succeeded,
    IReadOnlyList<ToolchainRuntimeConfigurationIssue> issues)
{
    /// <summary>The publication current when this operation completed.</summary>
    public ToolchainRuntimeConfigurationSnapshot Snapshot { get; } = snapshot;
    /// <summary>True only after admission and the requested persistence/read have succeeded.</summary>
    public bool Succeeded { get; } = succeeded;
    /// <summary>Defensively copied operation issues.</summary>
    public IReadOnlyList<ToolchainRuntimeConfigurationIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
}
