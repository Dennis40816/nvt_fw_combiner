namespace NvtFwCombiner.Application.Configuration;

/// <summary>Current configuration availability, distinct from firmware support or Build readiness.</summary>
public enum EventBufferFormatConfigurationStatus
{
    /// <summary>No persisted configuration has been read.</summary>
    NotLoaded,
    /// <summary>The current persisted snapshot passed configuration admission.</summary>
    Ready,
    /// <summary>No persisted configuration exists; defaults remain draft material only.</summary>
    Missing,
    /// <summary>Persisted configuration could not be admitted.</summary>
    Invalid,
}

/// <summary>Stable configuration persistence and admission failure categories.</summary>
public enum EventBufferFormatConfigurationFailure
{
    /// <summary>No configuration file exists.</summary>
    Missing,
    /// <summary>Configuration document shape or text is invalid.</summary>
    InvalidDocument,
    /// <summary>The persisted document belongs to another scope.</summary>
    ScopeMismatch,
    /// <summary>Recognition values or identity membership are invalid.</summary>
    InvalidValues,
    /// <summary>Reading persisted configuration failed.</summary>
    ReadFailed,
    /// <summary>Persisting the new configuration failed.</summary>
    SaveFailed,
}

/// <summary>One immutable publication. LastSaved is recovery material, never effective fallback.</summary>
public sealed record EventBufferFormatConfigurationState(
    long Generation,
    EventBufferFormatConfigurationStatus Status,
    EventBufferFormatConfiguration? Configuration,
    string? SourceSha256,
    EventBufferFormatConfiguration? LastSaved,
    string? LastSavedSha256);

/// <summary>Operation result; success describes configuration only, not runtime re-evaluation.</summary>
public sealed record EventBufferFormatConfigurationOperationResult(
    EventBufferFormatConfigurationState State,
    EventBufferFormatConfigurationFailure? Failure,
    IReadOnlyList<EventBufferFormatConfigurationIssue> Issues)
{
    /// <summary>Whether validation and the requested storage operation succeeded.</summary>
    public bool Succeeded => Failure is null;
}
