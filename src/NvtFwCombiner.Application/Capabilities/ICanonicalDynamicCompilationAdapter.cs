using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Compiles one profile-owned dynamic route selected by the current canonical
/// publication. Application retains publication and binding authority.
/// </summary>
public interface ICanonicalDynamicCompilationAdapter
{
    /// <summary>Projects one exact AB registration without compiling a map; other workflows are unavailable.</summary>
    bool TryGetAbAuthoringDefinition(
        CapabilityRouteIdentity identity,
        out CanonicalAbAuthoringDefinition? definition,
        out IReadOnlyList<CompositionIssue> issues);

    IReadOnlyList<long> GetMapCapacities(
        string icId,
        string workflowId,
        out IReadOnlyList<CompositionIssue> issues);

    void Compile(
        CapabilityRouteIdentity identity,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out MetadataPlanDefinition? metadataPlan,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology = null);

    /// <summary>Probes the existing DP Replace definition before publication binding; never selects an AB route.</summary>
    void CompileDefinition(
        string icId,
        string workflowId,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues);
}
