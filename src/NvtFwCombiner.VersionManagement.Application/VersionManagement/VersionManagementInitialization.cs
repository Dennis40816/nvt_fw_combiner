namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Initialization dispatch under the experience owner's mutation lock, using its durable reload and lease ports.</summary>
internal static class VersionManagementInitialization
{
    internal static async ValueTask<VersionManagementSnapshot> LoadAsync(
        bool isReadOnly,
        TimeSpan writerLeaseTimeout,
        Func<CancellationToken, bool, ValueTask<VersionManagementSnapshot>> reload,
        Func<TimeSpan, CancellationToken, ValueTask<VersionManagerWriteLeaseResult>> acquireLease,
        Func<VersionManagementSnapshot> publishUnavailable,
        CancellationToken cancellationToken)
    {
        if (isReadOnly)
        {
            return await reload(cancellationToken, false).ConfigureAwait(false);
        }
        using VersionManagerWriteLeaseResult lease = await acquireLease(
            writerLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        return lease.IsAcquired
            ? await reload(cancellationToken, true).ConfigureAwait(false)
            : publishUnavailable();
    }
}
