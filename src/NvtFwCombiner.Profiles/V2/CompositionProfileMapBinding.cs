namespace NvtFwCombiner.Profiles.V2;

/// <summary>Immutable trusted-family identity and canonical fact requirements.</summary>
internal sealed class CompositionProfileMapBinding
{
    private readonly string[] _mapIds;
    private readonly string[] _requiredRegionIds;
    private readonly string[] _requiredMetadataStructureIds;
    private readonly string[] _requiredCapabilityIds;

    internal CompositionProfileMapBinding(
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<string> mapIds,
        IEnumerable<string> requiredRegionIds,
        IEnumerable<string> requiredMetadataStructureIds,
        IEnumerable<string> requiredCapabilityIds)
    {
        FamilyId = CompositionProfileValueRules.RequireId(familyId, nameof(familyId));
        FamilyVersion = CompositionProfileValueRules.RequireSemanticVersion(
            familyVersion,
            nameof(familyVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        if (!CompositionProfileValueRules.IsLowercaseSha256(familyContentHash))
        {
            throw new ArgumentException(
                "Family content hash must be 64 lowercase hexadecimal characters.",
                nameof(familyContentHash));
        }

        _mapIds = CompositionProfileValueRules.SnapshotIds(mapIds, nameof(mapIds), requireValue: true);
        _requiredRegionIds = CompositionProfileValueRules.SnapshotIds(
            requiredRegionIds,
            nameof(requiredRegionIds),
            requireValue: true);
        _requiredMetadataStructureIds = CompositionProfileValueRules.SnapshotIds(
            requiredMetadataStructureIds,
            nameof(requiredMetadataStructureIds),
            requireValue: false);
        _requiredCapabilityIds = CompositionProfileValueRules.SnapshotIds(
            requiredCapabilityIds,
            nameof(requiredCapabilityIds),
            requireValue: false);

        FamilyContentHash = familyContentHash;
        MapIds = Array.AsReadOnly(_mapIds);
        RequiredRegionIds = Array.AsReadOnly(_requiredRegionIds);
        RequiredMetadataStructureIds = Array.AsReadOnly(_requiredMetadataStructureIds);
        RequiredCapabilityIds = Array.AsReadOnly(_requiredCapabilityIds);
    }

    internal string FamilyId { get; }

    internal string FamilyVersion { get; }

    internal string FamilyContentHash { get; }

    internal IReadOnlyList<string> MapIds { get; }

    internal IReadOnlyList<string> RequiredRegionIds { get; }

    internal IReadOnlyList<string> RequiredMetadataStructureIds { get; }

    internal IReadOnlyList<string> RequiredCapabilityIds { get; }
}
