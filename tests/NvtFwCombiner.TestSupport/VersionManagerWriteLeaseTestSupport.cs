using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.TestSupport;

/// <summary>Creates explicit same-process lease outcomes for Application unit-test adapters.</summary>
public static class VersionManagerWriteLeaseTestSupport
{
    /// <summary>Returns one disposable uncontended writer lease.</summary>
    public static VersionManagerWriteLeaseResult Acquired()
    {
#pragma warning disable CA2000 // Ownership transfers to VersionManagerWriteLeaseResult.
        return new(VersionManagerWriteLeaseIssue.None, new LeaseHandle());
#pragma warning restore CA2000
    }

    /// <summary>Returns one typed writer-contention result without an owned handle.</summary>
    public static VersionManagerWriteLeaseResult Busy()
    {
#pragma warning disable CA2000 // The non-acquired result owns no disposable handle.
        return new(VersionManagerWriteLeaseIssue.Busy);
#pragma warning restore CA2000
    }

    private sealed class LeaseHandle : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
