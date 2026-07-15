using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable post-normalization family facts used for candidate-scoped map resolution.</summary>
public sealed partial class FirmwareFamilyResolutionDefinition
{
    private readonly FirmwareImageMap[] _imageMaps;
    private readonly FirmwareMetadataSet[] _metadataSets;
    private readonly FirmwareMapFactBinding<FirmwareCapabilityFact>[] _capabilityBindings;
    private readonly string[] _requiredArtifactBindingIds;
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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        if (!IsLowercaseSha256(familyContentHash))
        {
            throw new ArgumentException(
                "Family content hash must be 64 lowercase hexadecimal characters.",
                nameof(familyContentHash));
        }

        _imageMaps = SnapshotMaps(imageMaps);
        _metadataSets = SnapshotMetadataSets(metadataSets);
        _capabilityBindings = SnapshotCapabilityBindings(capabilityBindings, _imageMaps);
        ValidateFamilyStructureIds(_metadataSets);

        Dictionary<string, FirmwareMetadataSet> metadataSetsById = _metadataSets.ToDictionary(
            static set => set.MetadataSetId,
            StringComparer.Ordinal);
        HashSet<string> referencedMetadataSetIds = new(StringComparer.Ordinal);
        HashSet<string> artifactBindingIds = new(StringComparer.Ordinal);
        _structuresByMap = new Dictionary<string, IReadOnlyList<FirmwareMetadataStructure>>(
            StringComparer.Ordinal);

        foreach (FirmwareImageMap map in _imageMaps)
        {
            FirmwareMetadataStructure[] structures = ResolveBoundMetadataStructures(
                map,
                metadataSetsById,
                referencedMetadataSetIds);
            ValidateCandidate(map, structures);
            foreach (FirmwareMetadataStructure structure in structures)
            {
                _ = artifactBindingIds.Add(structure.ArtifactBindingId);
            }

            _structuresByMap.Add(map.MapId, Array.AsReadOnly(structures));
        }

        if (_metadataSets.Any(set => !referencedMetadataSetIds.Contains(set.MetadataSetId)))
        {
            throw new ArgumentException(
                "Normalized family resolution definitions cannot contain unreferenced metadata sets.",
                nameof(metadataSets));
        }

        _requiredArtifactBindingIds = [.. artifactBindingIds];
        Array.Sort(_requiredArtifactBindingIds, StringComparer.Ordinal);

        FamilyId = familyId;
        FamilyVersion = familyVersion;
        FamilyContentHash = familyContentHash;
        ImageMaps = Array.AsReadOnly(_imageMaps);
        MetadataSets = Array.AsReadOnly(_metadataSets);
        CapabilityBindings = Array.AsReadOnly(_capabilityBindings);
        RequiredArtifactBindingIds = Array.AsReadOnly(_requiredArtifactBindingIds);
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

    /// <summary>Map-bound technical capability evidence that never changes map eligibility or Build support.</summary>
    public IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> CapabilityBindings { get; }

    /// <summary>Enumerates owned physical and capability provenance in deterministic binding order.</summary>
    public IEnumerable<FirmwareFactProvenance> EnumerateFactProvenance()
    {
        foreach (FirmwareImageMap map in _imageMaps)
        {
            foreach (FirmwareMapFactBinding<FirmwareRegionSet> binding in map.RegionSetBindings)
            {
                yield return binding.Provenance;
            }

            foreach (FirmwareMapFactBinding<FirmwareMetadataSet> binding in map.MetadataSetBindings)
            {
                yield return binding.Provenance;
            }
        }

        foreach (FirmwareMapFactBinding<FirmwareCapabilityFact> binding in _capabilityBindings)
        {
            yield return binding.Provenance;
        }
    }

    /// <summary>Artifact bindings reachable from at least one candidate map.</summary>
    public IReadOnlyList<string> RequiredArtifactBindingIds { get; }

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

    /// <summary>Resolves a field only through a candidate-selected metadata structure.</summary>
    public bool TryResolveField(
        string mapId,
        string structureId,
        string fieldId,
        [NotNullWhen(true)] out FirmwareMetadataField? field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        field = null;
        if (!TryResolveStructure(mapId, structureId, out FirmwareMetadataStructure? structure))
        {
            return false;
        }

        field = structure.Fields.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.FieldId, fieldId));
        return field is not null;
    }

    private static FirmwareImageMap[] SnapshotMaps(IEnumerable<FirmwareImageMap> imageMaps)
    {
        ArgumentNullException.ThrowIfNull(imageMaps);
        FirmwareImageMap[] snapshot = [.. imageMaps];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Family resolution definitions require an image map.", nameof(imageMaps));
        }

        if (snapshot.Any(static map => map is null))
        {
            throw new ArgumentException("Image maps cannot contain null.", nameof(imageMaps));
        }

        if (snapshot.Select(static map => map.MapId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Image map ids must be ordinally unique within a family.", nameof(imageMaps));
        }

        Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.MapId, right.MapId));
        return snapshot;
    }

    private static FirmwareMetadataSet[] SnapshotMetadataSets(IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        ArgumentNullException.ThrowIfNull(metadataSets);
        FirmwareMetadataSet[] snapshot = [.. metadataSets];
        if (snapshot.Any(static set => set is null))
        {
            throw new ArgumentException("Metadata sets cannot contain null.", nameof(metadataSets));
        }

        if (snapshot.Select(static set => set.MetadataSetId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Metadata set ids must be ordinally unique within a family.",
                nameof(metadataSets));
        }

        Array.Sort(snapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.MetadataSetId, right.MetadataSetId));
        return snapshot;
    }

    private static FirmwareMapFactBinding<FirmwareCapabilityFact>[] SnapshotCapabilityBindings(
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityBindings,
        IReadOnlyList<FirmwareImageMap> maps)
    {
        ArgumentNullException.ThrowIfNull(capabilityBindings);
        FirmwareMapFactBinding<FirmwareCapabilityFact>[] snapshot = [.. capabilityBindings];
        if (snapshot.Any(static binding => binding is null))
        {
            throw new ArgumentException("Capability bindings cannot contain null.", nameof(capabilityBindings));
        }

        if (snapshot.Select(static binding => binding.EffectiveKey).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Capability binding effective keys must be ordinally unique.", nameof(capabilityBindings));
        }

        foreach (FirmwareMapFactBinding<FirmwareCapabilityFact> binding in snapshot)
        {
            FirmwareImageMap effectiveMap = FindCapabilityMap(maps, binding.EffectiveKey, nameof(capabilityBindings));
            FirmwareImageMap directSourceMap = FindCapabilityMap(
                maps,
                binding.DirectSourceKey,
                nameof(capabilityBindings));
            if (binding.EffectiveKey.FactKind != FirmwareFactKind.Capability ||
                binding.DirectSourceKey.FactKind != FirmwareFactKind.Capability ||
                !effectiveMap.Applicability.MemberIds.Contains(binding.EffectiveKey.MemberId, StringComparer.Ordinal) ||
                !directSourceMap.Applicability.MemberIds.Contains(binding.DirectSourceKey.MemberId, StringComparer.Ordinal))
            {
                throw new ArgumentException("Capability bindings must use a member selected by their effective map.", nameof(capabilityBindings));
            }
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

    private static void ValidateFamilyStructureIds(IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        HashSet<string> structureIds = new(StringComparer.Ordinal);
        foreach (FirmwareMetadataStructure structure in metadataSets.SelectMany(static set => set.Structures))
        {
            if (!structureIds.Add(structure.StructureId))
            {
                throw new ArgumentException(
                    $"Metadata structure id '{structure.StructureId}' must be unique across the family.",
                    nameof(metadataSets));
            }
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
            if (!metadataSetsById.TryGetValue(binding.CanonicalFactId, out FirmwareMetadataSet? metadataSet) ||
                !ReferenceEquals(metadataSet, binding.Value))
            {
                throw new ArgumentException(
                    $"Image map '{map.MapId}' binding references an unknown canonical metadata set " +
                    $"'{binding.CanonicalFactId}'.",
                    nameof(map));
            }

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
            ValidateLocator(map, structure);
        }

        foreach (FirmwareMetadataPredicate predicate in map.Applicability.MetadataPredicates)
        {
            if (!structuresById.TryGetValue(
                predicate.MetadataStructureId,
                out FirmwareMetadataStructure? structure))
            {
                throw new ArgumentException(
                    $"Image map '{map.MapId}' predicate references an unselected metadata structure " +
                    $"'{predicate.MetadataStructureId}'.",
                    nameof(map));
            }

            FirmwareMetadataField? field = structure.Fields.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.FieldId, predicate.FieldId)) ?? throw new ArgumentException(
                    $"Image map '{map.MapId}' predicate references unknown field '{predicate.FieldId}' " +
                    $"in structure '{structure.StructureId}'.",
                    nameof(map));
            if (predicate.ExpectedValues.Any(value => !field.CanRepresent(value)))
            {
                throw new ArgumentException(
                    $"Image map '{map.MapId}' predicate value is not representable by " +
                    $"'{structure.StructureId}.{field.FieldId}'.",
                    nameof(map));
            }
        }
    }

    private static void ValidateLocator(FirmwareImageMap map, FirmwareMetadataStructure structure)
    {
        var regionsById = map.Regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);
        if (!regionsById.TryGetValue(
            structure.Locator.AllowedResultRegionId,
            out FirmwareRegion? allowedResultRegion))
        {
            throw new ArgumentException(
                $"Metadata structure '{structure.StructureId}' references unknown allowed region " +
                $"'{structure.Locator.AllowedResultRegionId}' in map '{map.MapId}'.",
                nameof(structure));
        }

        ByteRange mapRange = new(0, map.CapacityBytes);
        switch (structure.Locator)
        {
            case FirmwareAbsoluteRangeLocator absolute:
                ValidateAddressedRange(map, structure, absolute.Range, mapRange);
                EnsureContains(allowedResultRegion, absolute.Range.Range, structure, map, "absolute result");
                break;
            case FirmwareRegionRelativeLocator relative:
                if (!regionsById.TryGetValue(relative.RegionId, out FirmwareRegion? baseRegion))
                {
                    throw new ArgumentException(
                        $"Metadata structure '{structure.StructureId}' references unknown base region " +
                        $"'{relative.RegionId}' in map '{map.MapId}'.",
                        nameof(structure));
                }

                long start = checked(baseRegion.Range.Start + relative.Offset);
                ByteRange resultRange = new(start, structure.LengthBytes);
                EnsureContains(baseRegion, resultRange, structure, map, "region-relative result");
                EnsureContains(allowedResultRegion, resultRange, structure, map, "region-relative result");
                if (!mapRange.Contains(resultRange))
                {
                    throw new ArgumentException(
                        $"Metadata structure '{structure.StructureId}' escapes map '{map.MapId}'.",
                        nameof(structure));
                }

                break;
            case FirmwareMarkerRelativeLocator marker:
                ValidateAddressedRange(map, structure, marker.SearchRange, mapRange);
                ValidateMarkerEnvelope(marker, structure.LengthBytes);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(structure),
                    "Unknown firmware metadata locator type.");
        }
    }

    private static void ValidateAddressedRange(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareAddressedRange addressedRange,
        ByteRange mapRange)
    {
        if (!StringComparer.Ordinal.Equals(addressedRange.AddressSpaceId, map.AddressSpaceId))
        {
            throw new ArgumentException(
                $"Metadata structure '{structure.StructureId}' uses the wrong address space for map '{map.MapId}'.",
                nameof(addressedRange));
        }

        if (!mapRange.Contains(addressedRange.Range))
        {
            throw new ArgumentException(
                $"Metadata structure '{structure.StructureId}' addressed range escapes map '{map.MapId}'.",
                nameof(addressedRange));
        }
    }

    private static void EnsureContains(
        FirmwareRegion region,
        ByteRange range,
        FirmwareMetadataStructure structure,
        FirmwareImageMap map,
        string subject)
    {
        if (!region.Range.Contains(range))
        {
            throw new ArgumentException(
                $"Metadata structure '{structure.StructureId}' {subject} is outside region " +
                $"'{region.RegionId}' in map '{map.MapId}'.",
                nameof(range));
        }
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

    private static bool IsLowercaseSha256(string value)
    {
        return value.Length == 64 && value.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}
