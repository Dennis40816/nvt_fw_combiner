namespace NvtFwCombiner.Domain.Composition;

/// <summary>Immutable trusted-family identity and canonical fact requirements.</summary>
internal sealed class CompositionProfileMapBinding
{
    internal CompositionProfileMapBinding(
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<string> mapIds,
        IEnumerable<string> requiredRegionIds,
        IEnumerable<string> requiredMetadataStructureIds,
        IEnumerable<string> requiredCapabilityIds,
        IEnumerable<string>? optionalRegionIds = null)
    {
        FamilyId = CanonicalPolicyValueRules.RequireCanonicalId(familyId, nameof(familyId));
        FamilyVersion = CanonicalProfileValueRules.RequireSemanticVersion(
            familyVersion,
            nameof(familyVersion));
        _ = CanonicalSha256.Require(familyContentHash, nameof(familyContentHash));

        string[] mapIdsSnapshot = CanonicalPolicyValueRules.SnapshotCanonicalIds(mapIds, nameof(mapIds), requireValue: true);
        string[] requiredRegionIdsSnapshot = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            requiredRegionIds,
            nameof(requiredRegionIds),
            requireValue: true);
        string[] optionalRegionIdsSnapshot = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            optionalRegionIds ?? [],
            nameof(optionalRegionIds),
            requireValue: false);
        DomainInvariant.Reject(
            requiredRegionIdsSnapshot.Intersect(optionalRegionIdsSnapshot, StringComparer.Ordinal).Any(),
            "Required and optional map regions must be disjoint.",
            nameof(optionalRegionIds));
        string[] requiredMetadataStructureIdsSnapshot = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            requiredMetadataStructureIds,
            nameof(requiredMetadataStructureIds),
            requireValue: false);
        string[] requiredCapabilityIdsSnapshot = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            requiredCapabilityIds,
            nameof(requiredCapabilityIds),
            requireValue: false);

        FamilyContentHash = familyContentHash;
        MapIds = Array.AsReadOnly(mapIdsSnapshot);
        RequiredRegionIds = Array.AsReadOnly(requiredRegionIdsSnapshot);
        OptionalRegionIds = Array.AsReadOnly(optionalRegionIdsSnapshot);
        RequiredMetadataStructureIds = Array.AsReadOnly(requiredMetadataStructureIdsSnapshot);
        RequiredCapabilityIds = Array.AsReadOnly(requiredCapabilityIdsSnapshot);
    }

    internal string FamilyId { get; }

    internal string FamilyVersion { get; }

    internal string FamilyContentHash { get; }

    internal IReadOnlyList<string> MapIds { get; }

    internal IReadOnlyList<string> RequiredRegionIds { get; }

    internal IReadOnlyList<string> OptionalRegionIds { get; }

    internal IReadOnlyList<string> RequiredMetadataStructureIds { get; }

    internal IReadOnlyList<string> RequiredCapabilityIds { get; }
}
