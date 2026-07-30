using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// One shared admission rule for route, executable, decisions, and the exact
/// profile-owned metadata bindings published as a capability.
/// </summary>
internal static class CapabilityPublicationCoherence
{
    internal static void ValidateDefinition(
        CapabilityRouteIdentity identity,
        CompiledComposition compiledComposition,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        MetadataPlanDefinition metadataPlan)
    {
        ArgumentNullException.ThrowIfNull(metadataPlan);
        ValidateCore(
            identity,
            compiledComposition.CompilationFingerprint,
            compiledComposition,
            authoring,
            publication,
            evidence,
            metadataPlan.Entries);
    }

    internal static void ValidateResolved(ResolvedCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ValidateResolved(
            capability.Identity,
            capability.CapabilityFingerprint,
            capability.CompiledComposition,
            capability.Authoring,
            capability.Publication,
            capability.Evidence,
            capability.MetadataPlan,
            capability.ResolutionToken);
    }

    internal static void ValidateResolved(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        CompiledComposition compiledComposition,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        ResolvedMetadataPlan metadataPlan,
        ResolutionToken resolutionToken)
    {
        ArgumentNullException.ThrowIfNull(metadataPlan);
        resolutionToken.EnsureValid(nameof(resolutionToken));
        metadataPlan.ResolutionToken.EnsureValid(nameof(metadataPlan));
        if (metadataPlan.ResolutionToken != resolutionToken)
        {
            throw new ArgumentException(
                "Resolved capability and metadata plan must use the same publication token.",
                nameof(resolutionToken));
        }

        ValidateCore(
            identity,
            capabilityFingerprint,
            compiledComposition,
            authoring,
            publication,
            evidence,
            metadataPlan.Definition.Entries);
    }

    private static void ValidateCore(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        CompiledComposition compiledComposition,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        IReadOnlyList<MetadataPlanEntry> metadataEntries)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(authoring);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(metadataEntries);
        if (!StringComparer.Ordinal.Equals(identity.IcId, compiledComposition.IcId) ||
            !StringComparer.Ordinal.Equals(
                identity.WorkflowId,
                compiledComposition.ExperienceId))
        {
            throw new ArgumentException(
                "Capability route identity must match the compiled IC and workflow.",
                nameof(identity));
        }

        if (!StringComparer.Ordinal.Equals(
                capabilityFingerprint,
                compiledComposition.CompilationFingerprint))
        {
            throw new ArgumentException(
                "Capability fingerprint must match the exact compiled composition.",
                nameof(capabilityFingerprint));
        }

        ValidateDecision(
            identity,
            capabilityFingerprint,
            authoring,
            "authoring decision");
        ValidateDecision(
            identity,
            capabilityFingerprint,
            publication,
            "publication decision");
        ValidateDecision(
            identity,
            capabilityFingerprint,
            evidence,
            "evidence decision");
        V2CompiledCompositionDetails? details = compiledComposition.V2Details;
        if (details?.Provenance.Context is MapBoundV2CompilationContext mapContext &&
            !StringComparer.Ordinal.Equals(
                identity.MapVariant,
                mapContext.ResolvedMap.ImageMap.MapId))
        {
            throw new ArgumentException(
                "Capability route map variant must match the compiled resolved map.",
                nameof(identity));
        }

        ValidateOutputNamingMetadata(details, metadataEntries);
    }

    private static void ValidateDecision<TValue>(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        PinnedCapabilityDecision<TValue> decision,
        string label)
        where TValue : struct, Enum
    {
        if (!StringComparer.Ordinal.Equals(decision.RouteId, identity.RouteId) ||
            !StringComparer.Ordinal.Equals(
                decision.CapabilityFingerprint,
                capabilityFingerprint))
        {
            throw new ArgumentException(
                $"Capability {label} must pin the current route id and capability fingerprint.",
                nameof(decision));
        }
    }

    private static void ValidateOutputNamingMetadata(
        V2CompiledCompositionDetails? details,
        IReadOnlyList<MetadataPlanEntry> metadataEntries)
    {
        if (details?.OutputNamingRequirement.RuleId is null)
        {
            return;
        }

        MapBoundV2CompilationContext mapContext =
            details.Provenance.Context as MapBoundV2CompilationContext ??
            throw new ArgumentException(
                "Typed metadata-backed output naming requires a compiled resolved map.",
                nameof(details));
        foreach (CompiledOutputTokenRequirement requirement in
                 details.OutputNamingRequirement.TokenRequirements.Where(
                     static requirement => requirement.SourceKind is
                         CompiledOutputTokenSourceKind.DpcmiVersion or
                         CompiledOutputTokenSourceKind.FirmwareConfigTpVersion))
        {
            MetadataPlanEntry? entry = metadataEntries.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(
                    candidate.BindingId,
                    requirement.MetadataBindingId));
            CompiledInputSpaceBinding? inputBinding =
                details.InputContract.SpaceBindings.SingleOrDefault(candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.AddressSpaceId,
                        requirement.MetadataSpaceId));
            string expectedStructureId =
                requirement.SourceKind ==
                    CompiledOutputTokenSourceKind.DpcmiVersion
                ? DpcmiMetadataContract.StructureId
                : FirmwareConfigGeneralParametersContract.StructureId;
            if (entry is null ||
                inputBinding is null ||
                !entry.Purposes.Contains(MetadataReferencePurpose.OutputNaming) ||
                !StringComparer.Ordinal.Equals(
                    entry.StructureDefinition.StructureId,
                    expectedStructureId) ||
                !StringComparer.Ordinal.Equals(
                    entry.SpaceId,
                    requirement.MetadataSpaceId) ||
                !StringComparer.Ordinal.Equals(
                    entry.SlotId,
                    inputBinding.SlotId) ||
                !ReferenceEquals(entry.ResolvedMap, mapContext.ResolvedMap))
            {
                throw new ArgumentException(
                    $"Capability metadata plan does not retain the exact compiled output-naming binding '{requirement.MetadataBindingId}'.",
                    nameof(metadataEntries));
            }
        }
    }
}
