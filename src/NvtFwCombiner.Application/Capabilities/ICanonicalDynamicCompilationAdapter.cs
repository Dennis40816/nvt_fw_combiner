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
    /// <summary>Reads the profile-declared route key for a captured source extent without compiling it.</summary>
    bool TryGetSourceEnvelopeMapVariant(
        string icId,
        string workflowId,
        long? sourceLength,
        out string? mapVariant,
        out IReadOnlyList<CompositionIssue> issues)
    {
        mapVariant = null;
        issues = [];
        return false;
    }

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

    /// <summary>
    /// Compiles from captured immutable source artifacts. Legacy adapters fail closed rather
    /// than silently downgrading the request to the length-only overload.
    /// </summary>
    void Compile(
        CapabilityRouteIdentity identity,
        long? requestedMapCapacity,
        IReadOnlyList<FirmwareArtifactPayload> capturedArtifacts,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out MetadataPlanDefinition? metadataPlan,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(capturedArtifacts);
        composition = null;
        metadataPlan = null;
        issues = [new CompositionIssue(
            "capability.dynamic.captured-compilation-unsupported",
            "This dynamic compiler does not support captured source artifacts.")];
    }

}
