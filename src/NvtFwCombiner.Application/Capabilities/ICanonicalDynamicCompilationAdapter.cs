using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Compiles one profile-owned dynamic route selected by the current canonical
/// publication. Application retains publication and binding authority.
/// </summary>
public interface ICanonicalDynamicCompilationAdapter
{
    IReadOnlyList<long> GetMapCapacities(
        string icId,
        string workflowId,
        out IReadOnlyList<CompositionIssue> issues);

    void Compile(
        string icId,
        string workflowId,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out MetadataPlanDefinition? metadataPlan,
        out IReadOnlyList<CompositionIssue> issues);
}
