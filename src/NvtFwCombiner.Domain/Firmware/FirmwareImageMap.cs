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
    private readonly FirmwareRegionSet[] _regionSets;
    private readonly FirmwareRegion[] _regions;
    private readonly string[] _metadataSetIds;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates a checked physical image map from resolved region sets.</summary>
    public FirmwareImageMap(
        string mapId,
        string addressSpaceId,
        FirmwareMapApplicability applicability,
        FirmwareImageMapCoveragePolicy coveragePolicy,
        IEnumerable<FirmwareRegionSet> regionSets,
        IEnumerable<string> metadataSetIds,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentNullException.ThrowIfNull(applicability);
        if (!Enum.IsDefined(coveragePolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(coveragePolicy), coveragePolicy, "Unknown map coverage policy.");
        }

        _regionSets = SnapshotRegionSets(regionSets, addressSpaceId);
        _regions = [.. _regionSets.SelectMany(static set => set.Regions)];
        if (_regions.Select(static region => region.RegionId).Distinct(StringComparer.Ordinal).Count() !=
            _regions.Length)
        {
            throw new ArgumentException("Firmware region ids must be ordinally unique across a map.", nameof(regionSets));
        }

        Array.Sort(_regions, CompareRegions);
        ValidateRegionGraph(_regions, applicability.CapacityBytes);
        _metadataSetIds = SnapshotIds(metadataSetIds, nameof(metadataSetIds), requireValue: false);
        _evidenceRefs = SnapshotIds(evidenceRefs, nameof(evidenceRefs), requireValue: true);

        MapId = mapId;
        AddressSpaceId = addressSpaceId;
        Applicability = applicability;
        CoveragePolicy = coveragePolicy;
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

    /// <summary>Referenced physical region sets in ordinal id order.</summary>
    public IReadOnlyList<FirmwareRegionSet> RegionSets { get; }

    /// <summary>Flattened physical region graph in deterministic range order.</summary>
    public IReadOnlyList<FirmwareRegion> Regions { get; }

    /// <summary>Canonical metadata-set references in ordinal order.</summary>
    public IReadOnlyList<string> MetadataSetIds { get; }

    /// <summary>Map-level evidence manifest ids in ordinal order.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    private static FirmwareRegionSet[] SnapshotRegionSets(
        IEnumerable<FirmwareRegionSet> regionSets,
        string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(regionSets);
        FirmwareRegionSet[] snapshot = [.. regionSets];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Firmware image maps require a region set.", nameof(regionSets));
        }

        if (snapshot.Any(static regionSet => regionSet is null))
        {
            throw new ArgumentException("Firmware image maps cannot contain null region sets.", nameof(regionSets));
        }

        if (snapshot.Select(static set => set.RegionSetId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Firmware region-set ids must be ordinally unique.", nameof(regionSets));
        }

        if (snapshot.Any(set => !StringComparer.Ordinal.Equals(set.AddressSpaceId, addressSpaceId)))
        {
            throw new ArgumentException("Every region set must use the map address space.", nameof(regionSets));
        }

        Array.Sort(snapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.RegionSetId, right.RegionSetId));
        return snapshot;
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

    private static int CompareRegions(FirmwareRegion left, FirmwareRegion right)
    {
        int startComparison = left.Range.Start.CompareTo(right.Range.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        int lengthComparison = right.Range.Length.CompareTo(left.Range.Length);
        return lengthComparison != 0
            ? lengthComparison
            : StringComparer.Ordinal.Compare(left.RegionId, right.RegionId);
    }

    private static string[] SnapshotIds(
        IEnumerable<string> values,
        string parameterName,
        bool requireValue)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] snapshot = [.. values];
        if (requireValue && snapshot.Length == 0)
        {
            throw new ArgumentException("At least one identifier is required.", parameterName);
        }

        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Identifiers cannot contain null or whitespace.", parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Identifiers must be ordinally unique.", parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    private enum ParentVisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
