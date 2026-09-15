using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Adapts the trusted built-in profile registry to dynamic compilation.</summary>
internal sealed class BuiltInV2DynamicCompilationAdapter :
    ICanonicalDynamicCompilationAdapter
{
    private readonly Func<string, string, BuiltInV2Registration?> _findAbDeclarationRegistration;

    internal BuiltInV2DynamicCompilationAdapter(
        Func<string, string, BuiltInV2Registration?>? findAbDeclarationRegistration = null)
    {
        _findAbDeclarationRegistration = findAbDeclarationRegistration ?? BuiltInV2RegistrationRegistry.FindAbMergeRegistration;
    }

    public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
        out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(identity);
        definition = null;
        try
        {
            BuiltInV2Registration? registration = identity.WorkflowId == ExperienceIds.AbMerge
                ? _findAbDeclarationRegistration(identity.IcId, identity.MapVariant)
                : null;
            if (registration is null)
            {
                issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                    "No exact trusted AB registration provides this authoring declaration.")];
                return false;
            }
            return registration.TryGetAbAuthoringDefinition(out definition, out issues);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            issues = [new CompositionIssue("profile.v2.builtin-bundle-load-failed",
                $"The trusted AB declaration could not be loaded for '{identity.RouteId}': {exception.Message}")];
            return false;
        }
    }

    public IReadOnlyList<long> GetMapCapacities(
        string icId,
        string workflowId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return ResolveRegistration(icId, workflowId).GetMapCapacities(out issues);
    }

    public void Compile(
        CapabilityRouteIdentity identity,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out MetadataPlanDefinition? metadataPlan,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        BuiltInV2Registration? registration = identity.WorkflowId == ExperienceIds.AbMerge
            ? BuiltInV2RegistrationRegistry.FindAbMergeRegistration(identity.IcId, identity.MapVariant)
            : ResolveRegistration(identity.IcId, identity.WorkflowId);
        if (registration is null ||
            (registration.SelectionGroupMapVariantSetId is { } mapSet &&
                !StringComparer.Ordinal.Equals(mapSet, identity.MapVariant)))
        {
            composition = null;
            metadataPlan = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                $"No trusted dynamic registration matches route '{identity.RouteId}'.")];
            return;
        }

        CompileRegistration(registration, requestedMapCapacity, selectedInputSlotIds,
            out composition, out metadataPlan, out issues, requestedTopology);
        if (composition is not null && issues.Count == 0 && registration.SelectionGroupMapVariantSetId is null &&
            !StringComparer.Ordinal.Equals(composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId, identity.MapVariant))
        {
            composition = null;
            metadataPlan = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                $"The compiled map does not match route '{identity.RouteId}'.")];
        }
    }

    public void CompileDefinition(
        string icId,
        string workflowId,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (workflowId != ExperienceIds.DpReplace)
        {
            composition = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                "Only the existing DP Replace definition probe may compile without an exact published route.")];
            return;
        }

        CompileRegistration(ResolveRegistration(icId, workflowId), requestedMapCapacity,
            selectedInputSlotIds, out composition, out _, out issues, requestedTopology: null);
    }

    private static void CompileRegistration(
        BuiltInV2Registration registration,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out MetadataPlanDefinition? metadataPlan,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology)
    {
        registration.TryCompile(
            requestedMapCapacity,
            requestedTopology,
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
