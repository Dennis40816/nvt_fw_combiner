using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Binds Application readiness leases to the current external-processor generation.</summary>
internal sealed class RuntimeDependencyReadinessLeaseProvider :
    IRuntimeDependencyReadinessLeaseProvider
{
    public RuntimeDependencyReadinessLease AcquireCurrent()
    {
        ExternalProcessorGenerationLease lease = ExternalProcessorFactory.AcquireCurrent();
        return new(
            lease.ReadinessProvider,
            lease.Generation,
            ExternalProcessorFactory.IsCurrent);
    }
}
