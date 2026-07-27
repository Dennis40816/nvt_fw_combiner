using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Refreshes current-machine requirements selected by one compiled capability.</summary>
public interface IRuntimeDependencyReadinessProvider
{
    /// <summary>
    /// Re-probes platform, manifest, executable identity, and staging availability
    /// without changing any trusted firmware fact or processor authority.
    /// </summary>
    ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
        RuntimeDependencyReadinessRequest request,
        long generation,
        CancellationToken cancellationToken);
}
