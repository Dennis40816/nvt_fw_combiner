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

    public bool TryGetSourceEnvelopeMapVariant(
        string icId,
        string workflowId,
        long? sourceLength,
        out string? mapVariant,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (sourceLength is { } length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        }
        BuiltInV2Registration registration = ResolveRegistration(icId, workflowId);
        SourceEnvelopeProfileBinding? envelope = registration.SourceEnvelopeBinding;
        if (envelope is null)
        {
            mapVariant = null;
            issues = [];
            return false;
        }

        IReadOnlyList<FirmwareImageMap> maps = registration.GetMapVariants(out _, out issues);
        if (issues.Count != 0)
        {
            mapVariant = null;
            return false;
        }

        FirmwareImageMap[] exact = sourceLength is { } actualLength
            ? [.. maps.Where(map => map.CapacityBytes == actualLength)]
            : [];
        if (exact.Length > 1)
        {
            mapVariant = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteAmbiguous,
                "The trusted Standard declaration has multiple maps for this source length.")];
            return false;
        }

        string selectedId = exact.Length == 1
            ? exact[0].MapId
            : envelope.LayoutTemplateMapId;
        if (!maps.Any(map => StringComparer.Ordinal.Equals(map.MapId, selectedId)))
        {
            mapVariant = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                "The profile-declared source-envelope template is not a trusted Standard map.")];
            return false;
        }

        mapVariant = selectedId;
        return true;
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
        Compile(identity, requestedMapCapacity, [], selectedInputSlotIds,
            out composition, out metadataPlan, out issues, requestedTopology);
    }

    public void Compile(
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

        CompileRegistration(registration, requestedMapCapacity, capturedArtifacts, selectedInputSlotIds,
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

    private static void CompileRegistration(
        BuiltInV2Registration registration,
        long? requestedMapCapacity,
        IReadOnlyList<FirmwareArtifactPayload> capturedArtifacts,
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
            capturedArtifacts,
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
            _ => throw new InvalidOperationException(
                "Only registered map-bound dynamic routes use this compiler adapter."),
        };
    }
}
