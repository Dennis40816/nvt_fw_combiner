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
    /// <summary>State is unbound or belongs to another normalized managed root.</summary>
    ManagedRootMismatch,
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

/// <summary>Stable outcome from acquiring the one cross-process version-manager writer.</summary>
public enum VersionManagerWriteLeaseIssue
{
    /// <summary>The caller exclusively owns the exact canonical state file.</summary>
    None,
    /// <summary>Another application or launcher process currently owns the state file.</summary>
    Busy,
    /// <summary>The platform could not create or inspect the writer lease.</summary>
    Unavailable,
}

/// <summary>Exclusive cross-process ownership held across one complete state/filesystem/process transaction.</summary>
public sealed class VersionManagerWriteLeaseResult : IDisposable
{
    private readonly IDisposable? _lease;

    /// <summary>Creates one typed lease result.</summary>
    public VersionManagerWriteLeaseResult(
        VersionManagerWriteLeaseIssue issue,
        IDisposable? lease = null)
    {
        bool succeeded = issue == VersionManagerWriteLeaseIssue.None;
        if (succeeded != (lease is not null))
        {
            throw new ArgumentException("A successful writer lease must own exactly one handle.", nameof(lease));
        }
        Issue = issue;
        _lease = lease;
    }

    /// <summary>Gets the acquisition result.</summary>
    public VersionManagerWriteLeaseIssue Issue { get; }

    /// <summary>Gets whether this result owns the exclusive writer lease.</summary>
    public bool IsAcquired => Issue == VersionManagerWriteLeaseIssue.None;

    /// <inheritdoc />
    public void Dispose()
    {
        _lease?.Dispose();
    }
}

/// <summary>Atomic persistence port for launcher-owned managed-version state.</summary>
public interface IVersionManagerStateStore
{
    /// <summary>Tries to own the store's canonical state path across one complete transaction.</summary>
    /// <param name="waitTimeout">Maximum bounded contention wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An exclusive lease or a typed busy/unavailable outcome.</returns>
    ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
        TimeSpan waitTimeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads state without guessing missing version identities. Implementations must return
    /// their ValueTask promptly and honor cancellation; callers may isolate and abandon a
    /// read-only load after their own hard deadline.
    /// </summary>
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
