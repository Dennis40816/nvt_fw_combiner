using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Observes existing managed lifetime authority without creating a lease or job.</summary>
public sealed class FileSystemManagedProcessLifetimeProbe : IManagedProcessLifetimeProbe
{
    /// <inheritdoc />
    public ValueTask<ManagedProcessLifetimeStatus> ObserveAsync(
        string statePath,
        ManagedProcessLifetimeKind kind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ManagedProcessLifetimeLease.GetStatus(statePath, kind));
    }
}
