using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable post-normalization family facts used for candidate-scoped map resolution.</summary>
public sealed partial class FirmwareFamilyResolutionDefinition
{
    private readonly FirmwareImageMap[] _imageMaps;
    private readonly FirmwareMetadataSet[] _metadataSets;
    private readonly FirmwareMapFactBinding<FirmwareCapabilityFact>[] _capabilityBindings;
    private readonly FirmwareFamilyRelationship[] _familyRelationships;
    private readonly Dictionary<string, IReadOnlyList<FirmwareMetadataStructure>> _structuresByMap;

    /// <summary>Creates one atomic family, map, and metadata resolution definition.</summary>
    public FirmwareFamilyResolutionDefinition(
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<FirmwareImageMap> imageMaps,
        IEnumerable<FirmwareMetadataSet> metadataSets)
        : this(familyId, familyVersion, familyContentHash, imageMaps, metadataSets, [])
    {
    }

    /// <summary>Creates one normalized family after Profiles has validated map-bound capability semantics.</summary>
    internal FirmwareFamilyResolutionDefinition(
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<FirmwareImageMap> imageMaps,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityBindings)
        : this(
            familyId,
            familyVersion,
            familyContentHash,
            imageMaps,
            metadataSets,
            capabilityBindings,
            [])
    {
    }

    /// <summary>Creates one normalized family with explicit firmware-semantic relationships.</summary>
    internal FirmwareFamilyResolutionDefinition(
        string familyId,
        string familyVersion,
        string familyContentHash,
        IEnumerable<FirmwareImageMap> imageMaps,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityBindings,
        IEnumerable<FirmwareFamilyRelationship> familyRelationships)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        _ = CanonicalSha256.Require(familyContentHash, nameof(familyContentHash));

        _imageMaps = SnapshotMaps(imageMaps);
        _metadataSets = SnapshotMetadataSets(metadataSets);
        _capabilityBindings = SnapshotCapabilityBindings(capabilityBindings, _imageMaps);
        _familyRelationships = ImmutableReferenceSnapshot.CreateUnique(
            familyRelationships,
            static relationship => relationship.RelationshipId,
            "Family relationships cannot contain null.",
            "Family relationship ids must be ordinally unique.",
            StringComparer.Ordinal);
        ValidateFamilyRelationships(
            _familyRelationships,
            _imageMaps,
            _capabilityBindings);
        Array.Sort(
            _familyRelationships,
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.RelationshipId, right.RelationshipId));
        ValidateFamilyStructureIds(_metadataSets);

        Dictionary<string, FirmwareMetadataSet> metadataSetsById = _metadataSets.ToDictionary(
            static set => set.MetadataSetId,
            StringComparer.Ordinal);
        HashSet<string> referencedMetadataSetIds = new(StringComparer.Ordinal);
        _structuresByMap = new Dictionary<string, IReadOnlyList<FirmwareMetadataStructure>>(
            StringComparer.Ordinal);

        foreach (FirmwareImageMap map in _imageMaps)
        {
            FirmwareMetadataStructure[] structures = ResolveBoundMetadataStructures(
                map,
                metadataSetsById,
                referencedMetadataSetIds);
            ValidateCandidate(map, structures);
            _structuresByMap.Add(map.MapId, Array.AsReadOnly(structures));
        }

        DomainInvariant.Reject(
            _metadataSets.Any(set => !referencedMetadataSetIds.Contains(set.MetadataSetId)),
            "Normalized family resolution definitions cannot contain unreferenced metadata sets.",
            nameof(metadataSets));

        FamilyId = familyId;
        FamilyVersion = familyVersion;
        FamilyContentHash = familyContentHash;
        ImageMaps = Array.AsReadOnly(_imageMaps);
        MetadataSets = Array.AsReadOnly(_metadataSets);
        CapabilityBindings = Array.AsReadOnly(_capabilityBindings);
        FamilyRelationships = Array.AsReadOnly(_familyRelationships);
    }

    /// <summary>Stable source-family identifier.</summary>
    public string FamilyId { get; }

    /// <summary>Trusted source-family semantic version.</summary>
    public string FamilyVersion { get; }

    /// <summary>Trusted canonical source-family content hash.</summary>
    public string FamilyContentHash { get; }

    /// <summary>Candidate image maps in ordinal map-id order.</summary>
    public IReadOnlyList<FirmwareImageMap> ImageMaps { get; }

    /// <summary>Referenced metadata sets in ordinal set-id order.</summary>
    public IReadOnlyList<FirmwareMetadataSet> MetadataSets { get; }

    internal IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> CapabilityBindings { get; }

    internal IReadOnlyList<FirmwareFamilyRelationship> FamilyRelationships { get; }

    /// <summary>Returns metadata structures selected by one exact candidate map.</summary>
    public IReadOnlyList<FirmwareMetadataStructure> GetStructuresForMap(string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        return _structuresByMap.TryGetValue(mapId, out IReadOnlyList<FirmwareMetadataStructure>? structures)
            ? structures
            : throw new KeyNotFoundException($"Unknown firmware image map '{mapId}'.");
    }

    /// <summary>Resolves a structure only through metadata sets selected by the candidate map.</summary>
    public bool TryResolveStructure(
        string mapId,
        string structureId,
        [NotNullWhen(true)] out FirmwareMetadataStructure? structure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        structure = null;
        if (!_structuresByMap.TryGetValue(mapId, out IReadOnlyList<FirmwareMetadataStructure>? structures))
        {
            return false;
        }

        structure = structures.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.StructureId, structureId));
        return structure is not null;
    }

    private static FirmwareImageMap[] SnapshotMaps(IEnumerable<FirmwareImageMap> imageMaps)
    {
        FirmwareImageMap[] snapshot = ImmutableReferenceSnapshot.CreateUnique(
            imageMaps,
            static map => map.MapId,
            "Family resolution definitions require non-null image maps.",
            "Image map ids must be ordinally unique within a family.",
            StringComparer.Ordinal,
            requireValue: true);

        Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.MapId, right.MapId));
        return snapshot;
    }

    private static FirmwareMetadataSet[] SnapshotMetadataSets(IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        FirmwareMetadataSet[] snapshot = ImmutableReferenceSnapshot.CreateUnique(
            metadataSets,
            static set => set.MetadataSetId,
            "Metadata sets cannot contain null.",
            "Metadata set ids must be ordinally unique within a family.",
            StringComparer.Ordinal);

        Array.Sort(snapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.MetadataSetId, right.MetadataSetId));
        return snapshot;
    }

    private static FirmwareMapFactBinding<FirmwareCapabilityFact>[] SnapshotCapabilityBindings(
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityBindings,
        IReadOnlyList<FirmwareImageMap> maps)
    {
        FirmwareMapFactBinding<FirmwareCapabilityFact>[] snapshot = ImmutableReferenceSnapshot.CreateUnique(
            capabilityBindings,
            static binding => binding.EffectiveKey,
            "Capability bindings cannot contain null.",
            "Capability binding effective keys must be ordinally unique.");

        foreach (FirmwareMapFactBinding<FirmwareCapabilityFact> binding in snapshot)
        {
            FirmwareImageMap effectiveMap = FindCapabilityMap(maps, binding.EffectiveKey, nameof(capabilityBindings));
            FirmwareImageMap directSourceMap = FindCapabilityMap(
                maps,
                binding.DirectSourceKey,
                nameof(capabilityBindings));
            DomainInvariant.Reject(
                binding.EffectiveKey.FactKind != FirmwareFactKind.Capability ||
                binding.DirectSourceKey.FactKind != FirmwareFactKind.Capability ||
                !effectiveMap.Applicability.MemberIds.Contains(binding.EffectiveKey.MemberId, StringComparer.Ordinal) ||
                !directSourceMap.Applicability.MemberIds.Contains(binding.DirectSourceKey.MemberId, StringComparer.Ordinal),
                "Capability bindings must use a member selected by their effective map.", nameof(capabilityBindings));
        }

        Array.Sort(snapshot, static (left, right) =>
        {
            int member = StringComparer.Ordinal.Compare(left.EffectiveKey.MemberId, right.EffectiveKey.MemberId);
            if (member != 0)
            {
                return member;
            }

            int map = StringComparer.Ordinal.Compare(left.EffectiveKey.MapId, right.EffectiveKey.MapId);
            return map != 0
                ? map
                : StringComparer.Ordinal.Compare(left.EffectiveKey.FactId, right.EffectiveKey.FactId);
        });
        return snapshot;
    }

    private static FirmwareImageMap FindCapabilityMap(
        IReadOnlyList<FirmwareImageMap> maps,
        FirmwareMapFactKey key,
        string parameterName)
    {
        _ = key.FactKind == FirmwareFactKind.Capability
            ? true
            : throw new ArgumentException("Capability bindings must use capability fact keys.", parameterName);

        return maps.FirstOrDefault(map => StringComparer.Ordinal.Equals(map.MapId, key.MapId)) ??
            throw new ArgumentException(
                $"Capability binding references unknown image map '{key.MapId}'.",
                parameterName);
    }

    private static void ValidateFamilyRelationships(
        IReadOnlyList<FirmwareFamilyRelationship> relationships,
        IReadOnlyList<FirmwareImageMap> maps,
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilities)
    {
        HashSet<string> perfectMembers = new(StringComparer.Ordinal);
        HashSet<(string MapId, FirmwareSharedFactKind Kind, string FactId)> sharedFacts = [];
        foreach (FirmwareFamilyRelationship relationship in relationships)
        {
            if (relationship is PerfectFamilyRelationship perfect)
            {
                HashSet<string> members = [.. perfect.MemberIds];
                if (members.Any(member => !perfectMembers.Add(member)))
                {
                    throw new FirmwareFamilyRelationshipInvariantException(
                        relationship.RelationshipId,
                        "A member cannot have more than one perfect-family relationship.");
                }

                FirmwareImageMap[] selectedMaps =
                    [.. maps.Where(map => map.Applicability.MemberIds.Any(members.Contains))];
                if (perfect.MemberIds.Any(member =>
                        selectedMaps.All(map => !map.Applicability.MemberIds.Contains(member, StringComparer.Ordinal))))
                {
                    throw new FirmwareFamilyRelationshipInvariantException(
                        relationship.RelationshipId,
                        "Every perfect-family member must be selected by a family-owned map.");
                }

                if (selectedMaps.Any(map => !members.SetEquals(map.Applicability.MemberIds)))
                {
                    throw new FirmwareFamilyRelationshipInvariantException(
                        relationship.RelationshipId,
                        "Perfect-family relationships cannot contain member-specific maps.");
                }

                if (maps.Any(map =>
                        HasMemberAlias(map.RegionSetBindings, members) ||
                        HasMemberAlias(map.MetadataSetBindings, members)))
                {
                    throw new FirmwareFamilyRelationshipInvariantException(
                        relationship.RelationshipId,
                        "Perfect-family firmware semantics cannot use member-specific fact aliases.");
                }

                if (capabilities.Any(binding => members.Contains(binding.EffectiveKey.MemberId)))
                {
                    throw new FirmwareFamilyRelationshipInvariantException(
                        relationship.RelationshipId,
                        "Perfect-family firmware semantics cannot contain member-specific capability facts.");
                }
            }
            else if (relationship is SharedFactRelationship shared &&
                shared.ApplicableMaps.Any(map => shared.SharedFactReferences.Any(reference =>
                    !sharedFacts.Add((map.MapId, reference.Kind, reference.FactId)))))
            {
                throw new FirmwareFamilyRelationshipInvariantException(
                    relationship.RelationshipId,
                    "A canonical map fact cannot have more than one shared relationship.");
            }
        }
    }

    private static bool HasMemberAlias<TFact>(
        IEnumerable<FirmwareMapFactBinding<TFact>> bindings,
        HashSet<string> members)
        where TFact : class, IFirmwareMapFact
    {
        return bindings.Any(binding => binding.Provenance.AliasChain.Any(hop =>
            members.Contains(hop.TargetKey.MemberId) ||
            members.Contains(hop.SourceKey.MemberId)));
    }

    private static void ValidateFamilyStructureIds(IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        HashSet<string> structureIds = new(StringComparer.Ordinal);
        foreach (FirmwareMetadataStructure structure in metadataSets.SelectMany(static set => set.Structures))
        {
            DomainInvariant.Reject(
                !structureIds.Add(structure.StructureId),
                $"Metadata structure id '{structure.StructureId}' must be unique across the family.",
                nameof(metadataSets));
        }
    }

    private static FirmwareMetadataStructure[] ResolveBoundMetadataStructures(
        FirmwareImageMap map,
        Dictionary<string, FirmwareMetadataSet> metadataSetsById,
        HashSet<string> referencedMetadataSetIds)
    {
        List<FirmwareMetadataStructure> structures = [];
        foreach (FirmwareMapFactBinding<FirmwareMetadataSet> binding in map.MetadataSetBindings
                     .GroupBy(static binding => binding.CanonicalFactId, StringComparer.Ordinal)
                     .Select(static group => group.First()))
        {
            DomainInvariant.Reject(
                !metadataSetsById.TryGetValue(binding.CanonicalFactId, out FirmwareMetadataSet? metadataSet) ||
                !ReferenceEquals(metadataSet, binding.Value),
                $"Image map '{map.MapId}' binding references an unknown canonical metadata set '{binding.CanonicalFactId}'.",
                nameof(map));

            _ = referencedMetadataSetIds.Add(binding.CanonicalFactId);
            structures.AddRange(binding.Value.Structures);
        }

        FirmwareMetadataStructure[] snapshot = [.. structures];
        Array.Sort(snapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.StructureId, right.StructureId));
        return snapshot;
    }

    private static void ValidateCandidate(
        FirmwareImageMap map,
        IReadOnlyList<FirmwareMetadataStructure> structures)
    {
        var structuresById = structures.ToDictionary(
            static structure => structure.StructureId,
            StringComparer.Ordinal);

        foreach (FirmwareMetadataStructure structure in structures)
        {
            ValidateLocator(map, structure, structuresById);
        }
        ValidateMetadataDependencyGraph(map, structuresById);

        foreach (FirmwareMetadataPredicate predicate in map.Applicability.MetadataPredicates)
        {
            DomainInvariant.Reject(
                !structuresById.TryGetValue(
                    predicate.MetadataStructureId,
                    out FirmwareMetadataStructure? structure),
                $"Image map '{map.MapId}' predicate references an unselected metadata structure '{predicate.MetadataStructureId}'.",
                nameof(map));

            FirmwareMetadataField? field = structure.Fields.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.FieldId, predicate.FieldId)) ?? throw new ArgumentException(
                    $"Image map '{map.MapId}' predicate references unknown field '{predicate.FieldId}' " +
                    $"in structure '{structure.StructureId}'.",
                    nameof(map));
            DomainInvariant.Reject(
                predicate.ExpectedValues.Any(value => !field.CanRepresent(value)),
                $"Image map '{map.MapId}' predicate value is not representable by '{structure.StructureId}.{field.FieldId}'.",
                nameof(map));
        }
    }

    private static void ValidateLocator(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        Dictionary<string, FirmwareMetadataStructure> structuresById)
    {
        var regionsById = map.Regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);
        DomainInvariant.Reject(
            !regionsById.TryGetValue(
                structure.Locator.AllowedResultRegionId,
                out FirmwareRegion? allowedResultRegion),
            $"Metadata structure '{structure.StructureId}' references unknown allowed region '{structure.Locator.AllowedResultRegionId}' in map '{map.MapId}'.",
            nameof(structure));

        ByteRange mapRange = new(0, map.CapacityBytes);
        switch (structure.Locator)
        {
            case FirmwareAbsoluteRangeLocator absolute:
                ValidateAddressedRange(map, structure, absolute.Range, mapRange);
                EnsureContains(allowedResultRegion, absolute.Range.Range, structure, map, "absolute result");
                break;
            case FirmwareRegionRelativeLocator relative:
                DomainInvariant.Reject(
                    !regionsById.TryGetValue(relative.RegionId, out FirmwareRegion? baseRegion),
                    $"Metadata structure '{structure.StructureId}' references unknown base region '{relative.RegionId}' in map '{map.MapId}'.",
                    nameof(structure));

                long relativeStart = checked(baseRegion.Range.Start + relative.Offset);
                ByteRange relativeResultRange = new(relativeStart, structure.LengthBytes);
                EnsureContains(baseRegion, relativeResultRange, structure, map, "region-relative result");
                EnsureContains(allowedResultRegion, relativeResultRange, structure, map, "region-relative result");
                DomainInvariant.Reject(
                    !mapRange.Contains(relativeResultRange),
                    $"Metadata structure '{structure.StructureId}' escapes map '{map.MapId}'.",
                    nameof(structure));

                break;
            case FirmwareMarkerRelativeLocator marker:
                ValidateAddressedRange(map, structure, marker.SearchRange, mapRange);
                ValidateMarkerEnvelope(marker, structure.LengthBytes);
                break;
            case FirmwareMetadataFieldSelectedLocator selected:
                DomainInvariant.Reject(
                    !structuresById.TryGetValue(
                        selected.PrerequisiteStructureId,
                        out FirmwareMetadataStructure? prerequisite),
                    $"Metadata structure '{structure.StructureId}' references unknown prerequisite '{selected.PrerequisiteStructureId}' in map '{map.MapId}'.",
                    nameof(structure));

                FirmwareMetadataField prerequisiteField =
                    prerequisite.Fields.FirstOrDefault(field =>
                        StringComparer.Ordinal.Equals(
                            field.FieldId,
                            selected.PrerequisiteFieldId)) ??
                    throw new ArgumentException(
                        $"Metadata structure '{structure.StructureId}' references unknown prerequisite field " +
                        $"'{selected.PrerequisiteFieldId}' in map '{map.MapId}'.",
                        nameof(structure));
                DomainInvariant.Reject(
                    prerequisiteField.Encoding !=
                    FirmwareMetadataEncoding.UnsignedInteger,
                    $"Metadata structure '{structure.StructureId}' prerequisite field must be unsigned.",
                    nameof(structure));

                foreach (FirmwareMetadataFieldSelectedBranch branch in
                         selected.Branches)
                {
                    ValidateAddressedRange(
                        map,
                        structure,
                        branch.AnchorRange,
                        mapRange);
                    long start = checked(
                        branch.AnchorRange.Range.Start +
                        selected.ResultOffset);
                    ByteRange resultRange = new(start, structure.LengthBytes);
                    DomainInvariant.Reject(
                        !branch.AnchorRange.Range.Contains(resultRange),
                        $"Metadata structure '{structure.StructureId}' selected result escapes its anchor in map '{map.MapId}'.",
                        nameof(structure));

                    EnsureContains(
                        allowedResultRegion,
                        resultRange,
                        structure,
                        map,
                        "metadata-selected result");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(structure),
                    "Unknown firmware metadata locator type.");
        }
    }

    private static void ValidateMetadataDependencyGraph(
        FirmwareImageMap map,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string structureId in structuresById.Keys)
        {
            Visit(structureId);
        }

        void Visit(string structureId)
        {
            if (visited.Contains(structureId))
            {
                return;
            }

            DomainInvariant.Reject(
                !visiting.Add(structureId),
                $"Metadata dependency graph contains a cycle at '{structureId}' in map '{map.MapId}'.",
                nameof(structuresById));

            FirmwareMetadataStructure structure = structuresById[structureId];
            if (structure.Locator is FirmwareMetadataFieldSelectedLocator selected)
            {
                Visit(selected.PrerequisiteStructureId);
            }

            _ = visiting.Remove(structureId);
            _ = visited.Add(structureId);
        }
    }

    private static void ValidateAddressedRange(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareAddressedRange addressedRange,
        ByteRange mapRange)
    {
        DomainInvariant.Reject(
            !StringComparer.Ordinal.Equals(addressedRange.AddressSpaceId, map.AddressSpaceId),
            $"Metadata structure '{structure.StructureId}' uses the wrong address space for map '{map.MapId}'.",
            nameof(addressedRange));

        DomainInvariant.Reject(
            !mapRange.Contains(addressedRange.Range),
            $"Metadata structure '{structure.StructureId}' addressed range escapes map '{map.MapId}'.",
            nameof(addressedRange));
    }

    private static void EnsureContains(
        FirmwareRegion region,
        ByteRange range,
        FirmwareMetadataStructure structure,
        FirmwareImageMap map,
        string subject)
    {
        DomainInvariant.Reject(
            !region.Range.Contains(range),
            $"Metadata structure '{structure.StructureId}' {subject} is outside region '{region.RegionId}' in map '{map.MapId}'.",
            nameof(range));
    }

    private static void ValidateMarkerEnvelope(FirmwareMarkerRelativeLocator marker, long structureLength)
    {
        long firstMatch = marker.SearchRange.Range.Start;
        long lastMatch = checked(marker.SearchRange.Range.EndExclusive - marker.MarkerBytes.Length);
        long firstResult = checked(firstMatch + marker.ResultOffset);
        long lastResult = checked(lastMatch + marker.ResultOffset);
        _ = checked(firstResult + structureLength);
        _ = checked(lastResult + structureLength);
    }

}
