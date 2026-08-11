using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Reads and resolves the host's single canonical capability publication.
/// Callers receive immutable Application results and never own catalog loading,
/// profile compilation, or publication state.
/// </summary>
public interface ICanonicalCapabilityQuery
{
    /// <summary>Gets the current valid publication, loading it once when needed.</summary>
    CanonicalCapabilityCatalogSnapshot GetCurrentSnapshot();

    /// <summary>Gets the current publication when available; cold failure returns null.</summary>
    CanonicalCapabilityCatalogSnapshot? TryGetCurrentSnapshot();

    /// <summary>Resolves one exact published route.</summary>
    CapabilityResolutionResult Resolve(string routeId);

    /// <summary>Resolves one policy-bound dynamic route definition.</summary>
    CapabilityRouteResolutionResult ResolveDynamicRoute(string routeId);

    /// <summary>Resolves the sole exact route matching the selected axes.</summary>
    CapabilityResolutionResult ResolveUniqueRoute(
        string icId,
        string workflowId,
        string icCountVariant,
        long? outputCapacity = null);

    /// <summary>Resolves the sole exact route matching a topology selection.</summary>
    CapabilityResolutionResult ResolveUniqueTopologyRoute(
        string icId,
        string workflowId,
        TopologySelection? topology);

    /// <summary>Returns whether one authorable route is present in the current publication.</summary>
    bool HasAuthorableCapability(string icId, string workflowId);

    /// <summary>Retains one exact compiled capability only while its publication is current.</summary>
    ResolvedCapability? ResolveCurrentCompilation(
        CompiledComposition composition,
        ResolvedCapability? acceptedCapability = null);
}
