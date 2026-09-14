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
    MetadataInspectionSnapshot PrimaryInspection);

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
    internal static AbMergeFormatAdmissionResult Assess(
        FirmwareFamilyResolutionDefinition family,
        string memberId,
        EventBufferFormatConfigurationState? state,
        MetadataInspectionSnapshot? primary,
        TopologySelection? selectedTopology,
        IReadOnlyCollection<FirmwareBinInspectionArtifact>? artifacts)
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
        if (primary is null || payloads.Any(static artifact => artifact is null) ||
            payloads.Select(static artifact => artifact.ArtifactId).Distinct(StringComparer.Ordinal).Count() != payloads.Length ||
            !TryGetPrimary(family, memberId, primary, policy.PrimaryBindings.TpAStructureId, policy.PrimaryBindings, out byte rawA, out FirmwareArtifactIdentity? identityA) ||
            !TryGetPrimary(family, memberId, primary, policy.PrimaryBindings.TpBStructureId, policy.PrimaryBindings, out byte rawB, out FirmwareArtifactIdentity? identityB) ||
            FindMatchingArtifact(payloads, identityA!) is not { } tpA ||
            FindMatchingArtifact(payloads, identityB!) is not { } tpB)
        {
            return Blocked("AB_FORMAT_PRIMARY_INVALID", "Both TP inputs require valid primary FWConfig bound to these exact input bytes.");
        }

        bool requiresTopology = memberMaps.Any(map => map.Applicability.TopologyRequirement.Kind != TopologyRequirementKind.None);
        AbMergeTopologyAdmissionResult? topologyAdmission = null;
        if (requiresTopology)
        {
            if (selectedTopology is null)
            {
                return Blocked("AB_FORMAT_TOPOLOGY_REQUIRED", "Complete the existing TPA/TPB topology admission first.");
            }

            // The existing topology owner inspects this same immutable input pair and current selector.
            // Never accept a success result retained from another pair or earlier selector.
            topologyAdmission = AbMergeTopologyAdmission.Assess(tpA.Bytes, tpB.Bytes, selectedTopology);
            if (!topologyAdmission.Succeeded)
            {
                return new(null, topologyAdmission.Issues);
            }
        }
        else if (selectedTopology is not null)
        {
            return Blocked("AB_FORMAT_TOPOLOGY_UNEXPECTED", "This IC does not accept a topology selector.");
        }

        EventBufferFormatEntry? formatA = configuration.Match(policy.ScopeId, rawA);
        EventBufferFormatEntry? formatB = configuration.Match(policy.ScopeId, rawB);
        string formatId = formatA?.UniqueId ?? policy.CommonFormatId;
        if (formatId != (formatB?.UniqueId ?? policy.CommonFormatId))
        {
            return Blocked("AB_FORMAT_MISMATCH", "TPA and TPB resolve to different Event Buffer Formats.");
        }

        FirmwareImageMap[] variants = [.. policy.Variants
            .Where(variant => variant.MemberId == memberId && variant.FormatId == formatId)
            .Select(variant => family.ImageMaps.Single(map => map.MapId == variant.MapId))];
        FirmwareImageMap[] baselines = [.. variants.Where(map =>
            map.Applicability.TopologyRequirement.Kind != TopologyRequirementKind.ExactCount &&
            map.Applicability.TopologyRequirement.Matches(selectedTopology))];
        if (baselines.Length != 1)
        {
            return Blocked("AB_FORMAT_MAP_UNAVAILABLE", "No unique AB baseline matches the selected format and topology.");
        }

        // Counts are observations from the existing topology owner, not the Cascade picker's minimum.
        FirmwareImageMap[] exact = [.. variants.Where(map =>
            map.Applicability.TopologyRequirement.Kind == TopologyRequirementKind.ExactCount &&
            topologyAdmission is { Succeeded: true, TpAChipCount: not null, TpBChipCount: not null } &&
            map.Applicability.TopologyRequirement.ExactChipCount == topologyAdmission.TpAChipCount &&
            map.Applicability.TopologyRequirement.ExactChipCount == topologyAdmission.TpBChipCount)];
        if (exact.Length > 1)
        {
            return Blocked("AB_FORMAT_MAP_UNAVAILABLE", "More than one exact-count AB map matches these TP inputs.");
        }

        FirmwareImageMap selected = exact.Length == 1 ? exact[0] : baselines[0];
        return new(new(selected.MapId, formatId, formatA?.DisplayName ?? policy.CommonDisplayName,
            rawA, rawB, state!.Generation, state.SourceSha256!, family.FamilyId, family.FamilyVersion,
            family.FamilyContentHash, primary), []);
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

        FirmwareDecodedMetadataStructure decoded = resolved.DecodedStructure;
        FirmwareDecodedMetadataFact? fact = decoded.Facts.SingleOrDefault(candidate => candidate.FieldId == bindings.FieldId);
        FirmwareDecodedMetadataRelation? relation = decoded.Relations.SingleOrDefault(candidate => candidate.RelationId == bindings.RelationId);
        if (decoded.MetadataStructureId != structureId || decoded.ArtifactBindingId != canonical.ArtifactBindingId ||
            fact?.Value is not { Kind: FirmwareMetadataValueKind.UnsignedInteger, UnsignedIntegerValue: <= byte.MaxValue } fieldValue ||
            relation is not { IsSatisfied: true, Kind: FirmwareMetadataFieldRelationKind.BitwiseComplement })
        {
            return false;
        }

        value = checked((byte)fieldValue.UnsignedIntegerValue.Value);
        identity = resolved.ArtifactIdentity;
        return true;
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
