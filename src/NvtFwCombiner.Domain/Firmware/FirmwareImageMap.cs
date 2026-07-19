using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed physical coverage policy for a canonical firmware image map.</summary>
public enum FirmwareImageMapCoveragePolicy
{
    /// <summary>Roots and every declared child layer partition their complete containing range.</summary>
    CompleteWithExplicitGaps,
}

/// <summary>Immutable canonical physical region graph for one firmware image-map shape.</summary>
public sealed class FirmwareImageMap
{
    private readonly FirmwareMapFactBinding<FirmwareRegionSet>[] _regionSetBindings;
    private readonly FirmwareMapFactBinding<FirmwareMetadataSet>[] _metadataSetBindings;
    private readonly FirmwareRegionSet[] _regionSets;
    private readonly FirmwareRegion[] _regions;
    private readonly string[] _metadataSetIds;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates a checked physical image map from member-scoped immutable fact bindings.</summary>
    public FirmwareImageMap(
        string mapId,
        string addressSpaceId,
        FirmwareMapApplicability applicability,
        FirmwareImageMapCoveragePolicy coveragePolicy,
        IEnumerable<FirmwareMapFactBinding<FirmwareRegionSet>> regionSetBindings,
        IEnumerable<FirmwareMapFactBinding<FirmwareMetadataSet>> metadataSetBindings,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentNullException.ThrowIfNull(applicability);
        if (!Enum.IsDefined(coveragePolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(coveragePolicy), coveragePolicy, "Unknown map coverage policy.");
        }

        _regionSetBindings = SnapshotBindings(
            regionSetBindings,
            mapId,
            FirmwareFactKind.RegionSet,
            requireValue: true);
        _metadataSetBindings = SnapshotBindings(
            metadataSetBindings,
            mapId,
            FirmwareFactKind.MetadataSet,
            requireValue: false);
        Dictionary<string, FirmwareMetadataStructure> structuresById = BuildStructureIndex(_metadataSetBindings);
        ValidateBindingApplicability(_regionSetBindings, applicability, structuresById);
        ValidateBindingApplicability(_metadataSetBindings, applicability, structuresById);
        ValidateBindingCoverage(_regionSetBindings, applicability.MemberIds, FirmwareFactKind.RegionSet);
        ValidateBindingCoverage(_metadataSetBindings, applicability.MemberIds, FirmwareFactKind.MetadataSet);
        ValidateCanonicalValueIdentity(_regionSetBindings);
        ValidateCanonicalValueIdentity(_metadataSetBindings);
        _regionSets = DeriveCanonicalValues(_regionSetBindings, addressSpaceId);
        _regions = [.. _regionSets.SelectMany(static set => set.Regions)];
        if (_regions.Select(static region => region.RegionId).Distinct(StringComparer.Ordinal).Count() !=
            _regions.Length)
        {
            throw new ArgumentException(
                "Firmware region ids must be ordinally unique across a map.",
                nameof(regionSetBindings));
        }

        Array.Sort(_regions, FirmwareRangeOrdering.Compare);
        ValidateRegionGraph(_regions, applicability.CapacityBytes);
        _metadataSetIds = DeriveCanonicalIds(_metadataSetBindings);
        _evidenceRefs = ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            "At least one identifier is required.",
            "Identifiers cannot contain null or whitespace.",
            "Identifiers must be ordinally unique.");

        MapId = mapId;
        AddressSpaceId = addressSpaceId;
        Applicability = applicability;
        CoveragePolicy = coveragePolicy;
        RegionSetBindings = Array.AsReadOnly(_regionSetBindings);
        MetadataSetBindings = Array.AsReadOnly(_metadataSetBindings);
        RegionSets = Array.AsReadOnly(_regionSets);
        Regions = Array.AsReadOnly(_regions);
        MetadataSetIds = Array.AsReadOnly(_metadataSetIds);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable canonical image-map identifier.</summary>
    public string MapId { get; }

    /// <summary>Physical address space used by all region ranges.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Static selection predicates and exact image capacity.</summary>
    public FirmwareMapApplicability Applicability { get; }

    /// <summary>Exact image capacity selected by applicability.</summary>
    public long CapacityBytes => Applicability.CapacityBytes;

    /// <summary>Physical coverage invariant enforced by this map.</summary>
    public FirmwareImageMapCoveragePolicy CoveragePolicy { get; }

    /// <summary>Member-specific region-set bindings that provide the map's canonical region graph.</summary>
    public IReadOnlyList<FirmwareMapFactBinding<FirmwareRegionSet>> RegionSetBindings { get; }

    /// <summary>Member-specific metadata-set bindings selected by this physical map.</summary>
    public IReadOnlyList<FirmwareMapFactBinding<FirmwareMetadataSet>> MetadataSetBindings { get; }

    /// <summary>Canonical region-set projection derived only from <see cref="RegionSetBindings"/>.</summary>
    public IReadOnlyList<FirmwareRegionSet> RegionSets { get; }

    /// <summary>Flattened physical region graph in deterministic range order.</summary>
    public IReadOnlyList<FirmwareRegion> Regions { get; }

    /// <summary>Canonical metadata-set id projection derived only from <see cref="MetadataSetBindings"/>.</summary>
    public IReadOnlyList<string> MetadataSetIds { get; }

    /// <summary>Map-level evidence manifest ids in ordinal order.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    private static FirmwareMapFactBinding<TFact>[] SnapshotBindings<TFact>(
        IEnumerable<FirmwareMapFactBinding<TFact>> bindings,
        string mapId,
        FirmwareFactKind expectedKind,
        bool requireValue)
        where TFact : class, IFirmwareMapFact
    {
        FirmwareMapFactBinding<TFact>[] snapshot = ImmutableReferenceSnapshot.Create(
            bindings,
            "Firmware image-map bindings must be non-null and include required values.",
            requireValue);

        foreach (FirmwareMapFactBinding<TFact> binding in snapshot)
        {
            if (!StringComparer.Ordinal.Equals(binding.EffectiveKey.MapId, mapId) ||
                (!StringComparer.Ordinal.Equals(binding.DirectSourceKey.MapId, mapId) &&
                binding.Provenance.AliasChain.Count == 0))
            {
                throw new ArgumentException("Direct map bindings must use the containing map id.", nameof(bindings));
            }

            if (binding.EffectiveKey.FactKind != expectedKind ||
                binding.DirectSourceKey.FactKind != expectedKind)
            {
                throw new ArgumentException("Image-map bindings use the wrong fact kind.", nameof(bindings));
            }
        }

        if (snapshot.Select(static binding => binding.EffectiveKey).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Image-map binding effective keys must be unique.", nameof(bindings));
        }

        Array.Sort(snapshot, CompareBindings);
        return snapshot;
    }

    private static void ValidateBindingCoverage<TFact>(
        IReadOnlyList<FirmwareMapFactBinding<TFact>> bindings,
        IReadOnlyList<string> memberIds,
        FirmwareFactKind expectedKind)
        where TFact : class, IFirmwareMapFact
    {
        foreach (IGrouping<string, FirmwareMapFactBinding<TFact>> group in bindings.GroupBy(
                     static binding => binding.EffectiveKey.FactId,
                     StringComparer.Ordinal))
        {
            FirmwareMapFactBinding<TFact>[] references = [.. group];
            if (references.Any(binding => !memberIds.Contains(binding.EffectiveKey.MemberId, StringComparer.Ordinal)) ||
                references.Select(static binding => binding.EffectiveKey.MemberId).Distinct(StringComparer.Ordinal).Count() !=
                memberIds.Count ||
                references.Length != memberIds.Count)
            {
                throw new ArgumentException(
                    "Every image-map fact reference must bind exactly once for every map member.",
                    nameof(bindings));
            }

            if (references.Any(binding => binding.EffectiveKey.FactKind != expectedKind) ||
                references.Select(static binding => binding.CanonicalFactId).Distinct(StringComparer.Ordinal).Count() != 1 ||
                references.Skip(1).Any(binding => !ReferenceEquals(binding.Value, references[0].Value)))
            {
                throw new ArgumentException(
                    "One image-map fact reference must retain one canonical immutable value for all members.",
                    nameof(bindings));
            }
        }
    }

    private static void ValidateBindingApplicability<TFact>(
        IReadOnlyList<FirmwareMapFactBinding<TFact>> bindings,
        FirmwareMapApplicability mapApplicability,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
        where TFact : class, IFirmwareMapFact
    {
        var mapFactApplicability = FirmwareFactApplicability.FromMap(mapApplicability);
        foreach (FirmwareMapFactBinding<TFact> binding in bindings)
        {
            if (!FirmwareFactApplicabilityRelations.HasSameScope(
                    binding.Applicability,
                    mapFactApplicability,
                    structuresById))
            {
                throw new ArgumentException(
                    "Physical fact bindings must equal the containing map applicability.",
                    nameof(bindings));
            }

            if (binding.Provenance.AliasChain.Count != 0 &&
                !FirmwareFactApplicabilityRelations.HasSameScope(
                    binding.Applicability,
                    binding.Provenance.AliasChain[0].Applicability,
                    structuresById))
            {
                throw new ArgumentException(
                    "An alias binding must equal its first target-to-source hop applicability.",
                    nameof(bindings));
            }
        }
    }

    private static Dictionary<string, FirmwareMetadataStructure> BuildStructureIndex(
        IReadOnlyList<FirmwareMapFactBinding<FirmwareMetadataSet>> bindings)
    {
        var structuresById = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal);
        foreach (FirmwareMetadataStructure structure in bindings
                     .GroupBy(static binding => binding.CanonicalFactId, StringComparer.Ordinal)
                     .Select(static group => group.First().Value)
                     .SelectMany(static set => set.Structures))
        {
            if (!structuresById.TryAdd(structure.StructureId, structure))
            {
                throw new ArgumentException(
                    $"Metadata structure id '{structure.StructureId}' is ambiguous within one image map.",
                    nameof(bindings));
            }
        }

        return structuresById;
    }

    private static FirmwareRegionSet[] DeriveCanonicalValues(
        IReadOnlyList<FirmwareMapFactBinding<FirmwareRegionSet>> bindings,
        string addressSpaceId)
    {
        FirmwareRegionSet[] values =
        [
            .. bindings
                .GroupBy(static binding => binding.CanonicalFactId, StringComparer.Ordinal)
                .Select(static group => group.First().Value)
                .OrderBy(static value => value.CanonicalFactId, StringComparer.Ordinal),
        ];
        return values.Any(value => !StringComparer.Ordinal.Equals(value.AddressSpaceId, addressSpaceId))
            ? throw new ArgumentException("Every region set must use the map address space.", nameof(bindings))
            : values;
    }

    private static void ValidateCanonicalValueIdentity<TFact>(
        IReadOnlyList<FirmwareMapFactBinding<TFact>> bindings)
        where TFact : class, IFirmwareMapFact
    {
        foreach (IGrouping<string, FirmwareMapFactBinding<TFact>> group in bindings.GroupBy(
                     static binding => binding.CanonicalFactId,
                     StringComparer.Ordinal))
        {
            FirmwareMapFactBinding<TFact> first = group.First();
            if (group.Skip(1).Any(binding => !ReferenceEquals(binding.Value, first.Value)))
            {
                throw new ArgumentException(
                    "Bindings sharing one canonical fact id must share one immutable value instance.",
                    nameof(bindings));
            }
        }
    }

    private static string[] DeriveCanonicalIds(
        IReadOnlyList<FirmwareMapFactBinding<FirmwareMetadataSet>> bindings)
    {
        return
        [
            .. bindings
                .Select(static binding => binding.CanonicalFactId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static int CompareBindings<TFact>(
        FirmwareMapFactBinding<TFact> left,
        FirmwareMapFactBinding<TFact> right)
        where TFact : class, IFirmwareMapFact
    {
        int memberComparison = StringComparer.Ordinal.Compare(left.EffectiveKey.MemberId, right.EffectiveKey.MemberId);
        if (memberComparison != 0)
        {
            return memberComparison;
        }

        int kindComparison = left.EffectiveKey.FactKind.CompareTo(right.EffectiveKey.FactKind);
        return kindComparison != 0
            ? kindComparison
            : StringComparer.Ordinal.Compare(left.EffectiveKey.FactId, right.EffectiveKey.FactId);
    }

    private static void ValidateRegionGraph(IReadOnlyList<FirmwareRegion> regions, long capacityBytes)
    {
        var regionsById = regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);

        foreach (FirmwareRegion region in regions)
        {
            if (region.Range.EndExclusive > capacityBytes)
            {
                throw new ArgumentException(
                    $"Firmware region '{region.RegionId}' exceeds the map capacity.",
                    nameof(regions));
            }

            if (region.ParentRegionId is not { } parentId)
            {
                continue;
            }

            if (!regionsById.ContainsKey(parentId))
            {
                throw new ArgumentException(
                    $"Firmware region '{region.RegionId}' references unknown parent '{parentId}'.",
                    nameof(regions));
            }
        }

        ValidateAcyclicParents(regions, regionsById);
        foreach (FirmwareRegion region in regions)
        {
            if (region.ParentRegionId is not { } parentId)
            {
                continue;
            }

            FirmwareRegion parent = regionsById[parentId];
            if (parent.Range == region.Range || !parent.Range.Contains(region.Range))
            {
                throw new ArgumentException(
                    $"Firmware parent '{parentId}' must properly contain child '{region.RegionId}'.",
                    nameof(regions));
            }
        }

        ValidateCompletePartitions(regions, regionsById, capacityBytes);
    }

    private static void ValidateAcyclicParents(
        IEnumerable<FirmwareRegion> regions,
        Dictionary<string, FirmwareRegion> regionsById)
    {
        var states = regions.ToDictionary(
            static region => region.RegionId,
            static _ => ParentVisitState.Unvisited,
            StringComparer.Ordinal);
        foreach (FirmwareRegion region in regions)
        {
            if (states[region.RegionId] != ParentVisitState.Unvisited)
            {
                continue;
            }

            List<string> path = [];
            FirmwareRegion? current = region;
            while (current is not null)
            {
                ParentVisitState state = states[current.RegionId];
                if (state == ParentVisitState.Visiting)
                {
                    throw new ArgumentException("Firmware region parent relationships cannot contain cycles.", nameof(regions));
                }

                if (state == ParentVisitState.Visited)
                {
                    break;
                }

                states[current.RegionId] = ParentVisitState.Visiting;
                path.Add(current.RegionId);
                current = current.ParentRegionId is { } parentId
                    ? regionsById[parentId]
                    : null;
            }

            foreach (string regionId in path)
            {
                states[regionId] = ParentVisitState.Visited;
            }
        }
    }

    private static void ValidateCompletePartitions(
        IEnumerable<FirmwareRegion> regions,
        Dictionary<string, FirmwareRegion> regionsById,
        long capacityBytes)
    {
        List<FirmwareRegion> roots = [];
        Dictionary<string, List<FirmwareRegion>> childrenByParent = new(StringComparer.Ordinal);
        foreach (FirmwareRegion region in regions)
        {
            if (region.ParentRegionId is not { } parentId)
            {
                roots.Add(region);
                continue;
            }

            if (!childrenByParent.TryGetValue(parentId, out List<FirmwareRegion>? children))
            {
                children = [];
                childrenByParent.Add(parentId, children);
            }

            children.Add(region);
        }

        ValidatePartition(roots, new ByteRange(0, capacityBytes), "the map root");
        foreach (KeyValuePair<string, List<FirmwareRegion>> entry in childrenByParent)
        {
            ValidatePartition(entry.Value, regionsById[entry.Key].Range, $"region '{entry.Key}'");
        }
    }

    private static void ValidatePartition(
        List<FirmwareRegion> regions,
        ByteRange expectedRange,
        string subject)
    {
        long coveredUntil = expectedRange.Start;
        foreach (FirmwareRegion region in regions)
        {
            if (region.Range.Start != coveredUntil)
            {
                throw new ArgumentException(
                    $"Firmware children of {subject} must partition its range without overlap or implicit gaps.",
                    nameof(regions));
            }

            coveredUntil = region.Range.EndExclusive;
        }

        if (regions.Count == 0 || coveredUntil != expectedRange.EndExclusive)
        {
            throw new ArgumentException(
                $"Firmware children of {subject} must cover its exact range.",
                nameof(regions));
        }
    }

    private enum ParentVisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
