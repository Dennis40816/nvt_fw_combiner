using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>One format decision over captured configuration and inspected immutable artifacts.</summary>
internal sealed record AbMergeFormatSelection(
    string MapId,
    string FormatId,
    string DisplayName,
    byte TpAFormatByte,
    byte TpBFormatByte,
    long ConfigurationGeneration,
    string ConfigurationSourceSha256,
    string FamilyId,
    string FamilyVersion,
    string FamilyContentHash,
    FirmwareMetadataStructureResolution TpAPrimary,
    FirmwareMetadataStructureResolution TpBPrimary)
{
    // Migration evidence only; remove with the snapshot-based overload after its callers migrate.
    internal MetadataInspectionSnapshot? PrimaryInspection { get; init; }
}

/// <summary>A terminal format selection or blocking issues, never a partial fallback.</summary>
internal sealed class AbMergeFormatAdmissionResult(
    AbMergeFormatSelection? selection, IEnumerable<CompositionIssue> issues)
{
    internal AbMergeFormatSelection? Selection { get; } = selection;
    internal IReadOnlyList<CompositionIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
    internal bool Succeeded => Selection is not null && Issues.Count == 0;
}

/// <summary>Chooses a declared AB map from canonical primary facts and admitted configuration.</summary>
internal static class AbMergeFormatAdmission
{
    // Transitional, provenance-checked adapter. Classification exists only in the discovery overload.
    internal static AbMergeFormatAdmissionResult Assess(
        FirmwareFamilyResolutionDefinition family,
        string memberId,
        EventBufferFormatConfigurationState? state,
        MetadataInspectionSnapshot? primary,
        TopologySelection? selectedTopology,
        IReadOnlyCollection<FirmwareBinInspectionArtifact>? artifacts,
        IReadOnlyList<CompiledComposition> inputCandidates)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        if (family.AbFormatPolicy is not { } policy)
        {
            return Blocked("AB_FORMAT_NOT_DECLARED", "The selected family declares no AB format policy.");
        }

        if (AdmitCurrentConfiguration(policy, state) is null)
        {
            return Blocked("AB_FORMAT_CONFIGURATION_INVALID", "Save a valid Event Buffer Format configuration for this scope.");
        }

        FirmwareBinInspectionArtifact[] payloads = artifacts is null ? [] : [.. artifacts];
        if (primary is null || !HasUniqueArtifacts(payloads) ||
            !TryGetPrimary(family, memberId, primary, policy.PrimaryBindings.TpAStructureId, policy.PrimaryBindings, out _, out FirmwareArtifactIdentity? identityA) ||
            !TryGetPrimary(family, memberId, primary, policy.PrimaryBindings.TpBStructureId, policy.PrimaryBindings, out _, out FirmwareArtifactIdentity? identityB) ||
            FindMatchingArtifact(payloads, identityA!) is null || FindMatchingArtifact(payloads, identityB!) is null)
        {
            return InvalidPrimary();
        }

        AbMergeFormatAdmissionResult result = Assess(family, memberId, state, selectedTopology, payloads, inputCandidates);
        return result.Selection is { } selection
            ? new(selection with { PrimaryInspection = primary }, result.Issues)
            : result;
    }

    internal static AbMergeFormatAdmissionResult Assess(
        FirmwareFamilyResolutionDefinition family,
        string memberId,
        EventBufferFormatConfigurationState? state,
        TopologySelection? selectedTopology,
        IReadOnlyCollection<FirmwareBinInspectionArtifact>? artifacts,
        IReadOnlyList<CompiledComposition> inputCandidates)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        if (family.AbFormatPolicy is not { } policy)
        {
            return Blocked("AB_FORMAT_NOT_DECLARED", "The selected family declares no AB format policy.");
        }

        EventBufferFormatConfiguration? configuration = AdmitCurrentConfiguration(policy, state);
        if (configuration is null)
        {
            return Blocked("AB_FORMAT_CONFIGURATION_INVALID", "Save a valid Event Buffer Format configuration for this scope.");
        }

        FirmwareImageMap[] memberMaps = [.. policy.Variants
            .Where(variant => variant.MemberId == memberId)
            .Select(variant => family.ImageMaps.Single(map => map.MapId == variant.MapId))];
        if (memberMaps.Length == 0)
        {
            return Blocked("AB_FORMAT_NOT_DECLARED", "The selected IC has no declared AB format variants.");
        }

        FirmwareBinInspectionArtifact[] payloads = artifacts is null ? [] : [.. artifacts];
        if (!HasUniqueArtifacts(payloads) ||
            !family.TryResolveAbPrimaryFirmwareConfig(memberId,
                payloads.Select(static artifact => new FirmwareArtifactPayload(artifact.ArtifactId, artifact.Bytes)),
                selectedTopology, out FirmwareMetadataStructureResolution? primaryA, out FirmwareMetadataStructureResolution? primaryB) ||
            !TryGetDecodedPrimary(primaryA, policy.PrimaryBindings, out byte rawA) ||
            !TryGetDecodedPrimary(primaryB, policy.PrimaryBindings, out byte rawB) ||
            FindMatchingArtifact(payloads, primaryA.Resolved!.ArtifactIdentity) is not { } tpA ||
            FindMatchingArtifact(payloads, primaryB.Resolved!.ArtifactIdentity) is not { } tpB)
        {
            return InvalidPrimary();
        }

        bool requiresTopology = memberMaps.Any(map => map.Applicability.TopologyRequirement.Kind != TopologyRequirementKind.None);
        if (requiresTopology)
        {
            if (selectedTopology is null)
            {
                return Blocked("AB_FORMAT_TOPOLOGY_REQUIRED", "Complete the existing TPA/TPB topology admission first.");
            }

        }
        else if (selectedTopology is not null)
        {
            return Blocked("AB_FORMAT_TOPOLOGY_UNEXPECTED", "This IC does not accept a topology selector.");
        }

        // Pair equality is independent of whether this family exposes a topology selector.
        AbMergeTopologyAdmissionResult topologyAdmission = AbMergeTopologyAdmission.AssessCommonAcceptedPair(
            inputCandidates, tpA.Bytes.ToArray(), tpB.Bytes.ToArray(), selectedTopology);
        if (!topologyAdmission.Succeeded) { return new(null, topologyAdmission.Issues); }

        AbFormatMapResolutionResult resolved = AbFormatMapResolver.Resolve(family, memberId, configuration,
            rawA, rawB, selectedTopology, topologyAdmission.TpAChipCount, topologyAdmission.TpBChipCount);
        return resolved.Selection is { } selected
            ? new(new(selected.MapId, selected.FormatId, selected.DisplayName,
                rawA, rawB, state!.Generation, state.SourceSha256!, family.FamilyId, family.FamilyVersion,
                family.FamilyContentHash, primaryA, primaryB), [])
            : new(null, resolved.Issues);
    }

    private static EventBufferFormatConfiguration? AdmitCurrentConfiguration(
        FirmwareAbFormatPolicy policy, EventBufferFormatConfigurationState? state)
    {
        if (state is not { Status: EventBufferFormatConfigurationStatus.Ready, Configuration: { } configuration } ||
            configuration.ScopeId != policy.ScopeId || state.Generation < 0 ||
            state.SourceSha256 is null || !CapabilityRouteIdentity.IsSha256(state.SourceSha256))
        {
            return null;
        }

        // Reuse the config owner against this family catalog; a stale same-scope catalog is not authority.
        return EventBufferFormatConfigurationAdmission.Admit(policy.ScopeId,
            [.. policy.Formats.Select(format => new EventBufferFormatIdentity(format.UniqueId, format.DisplayName))],
            [.. configuration.Entries.Select(entry => new EventBufferFormatDraftEntry(entry.UniqueId, entry.AliasName,
                [.. entry.RecognitionValues.Select(value => (int)value)]))]).Configuration;
    }

    private static bool TryGetPrimary(
        FirmwareFamilyResolutionDefinition family, string memberId, MetadataInspectionSnapshot snapshot,
        string structureId, FirmwareAbPrimaryBindings bindings, out byte value, out FirmwareArtifactIdentity? identity)
    {
        value = default;
        identity = null;
        MetadataInspectionResult[] results = [.. snapshot.Results.Where(result =>
            result.PlanEntry.Definition.StructureDefinition.StructureId == structureId)];
        if (results.Length != 1)
        {
            return false;
        }

        MetadataInspectionResult result = results[0];
        MetadataPlanEntry entry = result.PlanEntry.Definition;
        if (entry.FamilyDefinition.FamilyId != family.FamilyId ||
            entry.FamilyDefinition.FamilyVersion != family.FamilyVersion ||
            entry.FamilyDefinition.FamilyContentHash != family.FamilyContentHash ||
            entry.ResolvedMap.MemberId != memberId ||
            !family.TryResolveStructure(entry.ResolvedMap.ImageMap.MapId, structureId, out FirmwareMetadataStructure? canonical) ||
            !ReferenceEquals(canonical, entry.StructureDefinition) ||
            result.State != MetadataInspectionState.Value ||
            result.Resolution?.Resolved is not { } resolved ||
            !ReferenceEquals(resolved.StructureDefinition, canonical) ||
            !snapshot.ArtifactIdentities.Contains(resolved.ArtifactIdentity))
        {
            return false;
        }

        if (!TryGetDecodedPrimary(result.Resolution, bindings, out value))
        {
            return false;
        }

        identity = resolved.ArtifactIdentity;
        return true;
    }

    private static bool TryGetDecodedPrimary(
        FirmwareMetadataStructureResolution resolution, FirmwareAbPrimaryBindings bindings, out byte value)
    {
        value = default;
        if (resolution.Resolved is not { } resolved)
        {
            return false;
        }

        FirmwareMetadataStructure canonical = resolved.StructureDefinition;
        FirmwareDecodedMetadataStructure decoded = resolved.DecodedStructure;
        FirmwareDecodedMetadataFact? fact = decoded.Facts.SingleOrDefault(candidate => candidate.FieldId == bindings.FieldId);
        FirmwareDecodedMetadataRelation? relation = decoded.Relations.SingleOrDefault(candidate => candidate.RelationId == bindings.RelationId);
        if (decoded.MetadataStructureId != canonical.StructureId || decoded.ArtifactBindingId != canonical.ArtifactBindingId ||
            fact?.Value is not { Kind: FirmwareMetadataValueKind.UnsignedInteger, UnsignedIntegerValue: <= byte.MaxValue } fieldValue ||
            relation is not { IsSatisfied: true, Kind: FirmwareMetadataFieldRelationKind.BitwiseComplement })
        {
            return false;
        }

        value = checked((byte)fieldValue.UnsignedIntegerValue.Value);
        return true;
    }

    private static bool HasUniqueArtifacts(FirmwareBinInspectionArtifact[] artifacts)
    {
        return !artifacts.Any(static artifact => artifact is null) &&
            artifacts.Select(static artifact => artifact.ArtifactId).Distinct(StringComparer.Ordinal).Count() == artifacts.Length;
    }

    private static AbMergeFormatAdmissionResult InvalidPrimary()
    {
        return Blocked("AB_FORMAT_PRIMARY_INVALID", "Both TP inputs require valid primary FWConfig bound to these exact input bytes.");
    }

    private static FirmwareBinInspectionArtifact? FindMatchingArtifact(
        IReadOnlyList<FirmwareBinInspectionArtifact> artifacts, FirmwareArtifactIdentity identity)
    {
        return artifacts.SingleOrDefault(artifact => artifact.ArtifactId == identity.ArtifactId &&
            artifact.LengthBytes == identity.LengthBytes && artifact.Sha256 == identity.Sha256);
    }

    private static AbMergeFormatAdmissionResult Blocked(string code, string message)
    {
        return new(null, [new(code, message)]);
    }
}
