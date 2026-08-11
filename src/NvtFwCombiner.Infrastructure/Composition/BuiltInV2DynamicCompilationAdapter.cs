using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Adapts the trusted built-in profile registry to dynamic compilation.</summary>
internal sealed class BuiltInV2DynamicCompilationAdapter :
    ICanonicalDynamicCompilationAdapter
{
    public IReadOnlyList<long> GetMapCapacities(
        string icId,
        string workflowId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return ResolveRegistration(icId, workflowId).GetMapCapacities(out issues);
    }

    public void Compile(
        string icId,
        string workflowId,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out MetadataPlanDefinition? metadataPlan,
        out IReadOnlyList<CompositionIssue> issues)
    {
        BuiltInV2Registration registration = ResolveRegistration(icId, workflowId);
        registration.TryCompile(
            requestedMapCapacity,
            requestedTopology: null,
            selectedInputSlotIds,
            out composition,
            out issues);
        metadataPlan = composition is not null && issues.Count == 0
            ? registration.CreateMetadataPlan(composition)
            : null;
    }

    private static BuiltInV2Registration ResolveRegistration(
        string icId,
        string workflowId)
    {
        return workflowId switch
        {
            ExperienceIds.StandardMerge =>
                BuiltInV2RegistrationRegistry.StandardMergeByIc[icId],
            ExperienceIds.DpReplace =>
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[icId],
            _ => throw new InvalidOperationException(
                "Only registered map-bound dynamic routes use this compiler adapter."),
        };
    }
}
