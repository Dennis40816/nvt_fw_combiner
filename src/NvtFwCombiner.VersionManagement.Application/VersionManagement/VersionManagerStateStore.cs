namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Stable launcher-state read result category.</summary>
public enum VersionManagerStateLoadIssue
{
    /// <summary>State loaded and validated.</summary>
    None,
    /// <summary>No state file exists.</summary>
    Missing,
    /// <summary>State exists but is malformed or inconsistent.</summary>
    Invalid,
    /// <summary>State exists but could not be read.</summary>
    Unavailable,
}

/// <summary>Fail-closed launcher-state load result.</summary>
public sealed record VersionManagerStateLoadResult(
    VersionManagerState? State,
    VersionManagerStateLoadIssue Issue)
{
    /// <summary>Gets whether a validated state snapshot was published.</summary>
    public bool IsSuccess => State is not null && Issue == VersionManagerStateLoadIssue.None;
}

/// <summary>Stable result from one atomic launcher-state write.</summary>
public enum VersionManagerStateSaveIssue
{
    /// <summary>The complete state snapshot was atomically committed.</summary>
    None,
    /// <summary>The destination could not durably accept the snapshot.</summary>
    Unavailable,
}

/// <summary>Typed state persistence result used at cross-resource transaction seams.</summary>
public readonly record struct VersionManagerStateSaveResult(VersionManagerStateSaveIssue Issue)
{
    /// <summary>Gets whether the atomic state write committed.</summary>
    public bool IsSuccess => Issue == VersionManagerStateSaveIssue.None;
}

/// <summary>Atomic persistence port for launcher-owned managed-version state.</summary>
public interface IVersionManagerStateStore
{
    /// <summary>Loads state without guessing missing version identities.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fail-closed state result.</returns>
    ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Atomically saves one validated state snapshot.</summary>
    /// <param name="state">Validated immutable state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completion token.</returns>
    ValueTask SaveAsync(VersionManagerState state, CancellationToken cancellationToken);

    /// <summary>Atomically saves state and converts adapter failure into a stable result.</summary>
    async ValueTask<VersionManagerStateSaveResult> TrySaveAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(state, cancellationToken).ConfigureAwait(false);
            return new(VersionManagerStateSaveIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new(VersionManagerStateSaveIssue.Unavailable);
        }
    }
}
