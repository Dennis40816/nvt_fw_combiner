namespace NvtFwCombiner.Application.Configuration;

internal enum EventBufferFormatConfigurationStatus
{
    NotLoaded,
    Ready,
    Missing,
    Invalid,
}

internal enum EventBufferFormatConfigurationFailure
{
    Missing,
    InvalidDocument,
    ScopeMismatch,
    InvalidValues,
    ReadFailed,
    SaveFailed,
}

/// <summary>One immutable publication. LastSaved is recovery material, never effective fallback.</summary>
internal sealed record EventBufferFormatConfigurationState(
    long Generation,
    EventBufferFormatConfigurationStatus Status,
    EventBufferFormatConfiguration? Configuration,
    string? SourceSha256,
    EventBufferFormatConfiguration? LastSaved,
    string? LastSavedSha256);

internal sealed record EventBufferFormatConfigurationOperationResult(
    EventBufferFormatConfigurationState State,
    EventBufferFormatConfigurationFailure? Failure,
    IReadOnlyList<EventBufferFormatConfigurationIssue> Issues)
{
    internal bool Succeeded => Failure is null;
}
