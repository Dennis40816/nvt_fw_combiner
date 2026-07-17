namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable evidence-backed group of physical regions in one address space.</summary>
public sealed class FirmwareRegionSet : IFirmwareMapFact
{
    private readonly FirmwareRegion[] _regions;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates a region set without resolving cross-set parent references.</summary>
    public FirmwareRegionSet(
        string regionSetId,
        string addressSpaceId,
        IEnumerable<FirmwareRegion> regions,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);

        ArgumentNullException.ThrowIfNull(regions);
        _regions = [.. regions];
        if (_regions.Length == 0)
        {
            throw new ArgumentException("Firmware region sets cannot be empty.", nameof(regions));
        }

        if (_regions.Any(static region => region is null))
        {
            throw new ArgumentException("Firmware region sets cannot contain null.", nameof(regions));
        }

        if (_regions.Select(static region => region.RegionId).Distinct(StringComparer.Ordinal).Count() !=
            _regions.Length)
        {
            throw new ArgumentException("Firmware region ids must be ordinally unique within a set.", nameof(regions));
        }

        Array.Sort(_regions, FirmwareRangeOrdering.Compare);
        _evidenceRefs = ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            "Firmware region sets require evidence.",
            "Evidence references cannot contain null or whitespace.",
            "Evidence references must be ordinally unique.");

        RegionSetId = regionSetId;
        AddressSpaceId = addressSpaceId;
        Regions = Array.AsReadOnly(_regions);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable physical fact-set identifier.</summary>
    public string RegionSetId { get; }

    /// <inheritdoc />
    public FirmwareFactKind FactKind => FirmwareFactKind.RegionSet;

    /// <inheritdoc />
    public string CanonicalFactId => RegionSetId;

    /// <summary>Address space used by every region range in this set.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Physical regions in deterministic range order.</summary>
    public IReadOnlyList<FirmwareRegion> Regions { get; }

    /// <summary>Evidence manifest ids in ordinal order.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }
}
