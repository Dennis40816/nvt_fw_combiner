using NvtFwCombiner.Domain;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Immutable trusted-family identity and canonical fact requirements.</summary>
internal sealed class CompositionProfileMapBinding
{
    private readonly string[] _mapIds;
    private readonly string[] _requiredRegionIds;
    private readonly string[] _optionalRegionIds;
    private readonly string[] _requiredMetadataStructureIds;
    private readonly string[] _requiredCapabilityIds;

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
        FamilyVersion = CompositionProfileValueRules.RequireSemanticVersion(
            familyVersion,
            nameof(familyVersion));
        _ = CanonicalSha256.Require(familyContentHash, nameof(familyContentHash));

        _mapIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(mapIds, nameof(mapIds), requireValue: true);
        _requiredRegionIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            requiredRegionIds,
            nameof(requiredRegionIds),
            requireValue: true);
        _optionalRegionIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            optionalRegionIds ?? [],
            nameof(optionalRegionIds),
            requireValue: false);
        if (_requiredRegionIds.Intersect(_optionalRegionIds, StringComparer.Ordinal).Any())
        {
            throw new ArgumentException(
                "Required and optional map regions must be disjoint.",
                nameof(optionalRegionIds));
        }
        _requiredMetadataStructureIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            requiredMetadataStructureIds,
            nameof(requiredMetadataStructureIds),
            requireValue: false);
        _requiredCapabilityIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            requiredCapabilityIds,
            nameof(requiredCapabilityIds),
            requireValue: false);

        FamilyContentHash = familyContentHash;
        MapIds = Array.AsReadOnly(_mapIds);
        RequiredRegionIds = Array.AsReadOnly(_requiredRegionIds);
        OptionalRegionIds = Array.AsReadOnly(_optionalRegionIds);
        RequiredMetadataStructureIds = Array.AsReadOnly(_requiredMetadataStructureIds);
        RequiredCapabilityIds = Array.AsReadOnly(_requiredCapabilityIds);
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
