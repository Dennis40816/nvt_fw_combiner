namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable evidence-backed group of physical regions in one address space.</summary>
public sealed class FirmwareRegionSet : IFirmwareMapFact
{
    private readonly FirmwareRegionTemplate[] _regionTemplates;
    private readonly FirmwareRegionInstance[] _regionInstances;

    /// <summary>Creates a region set without resolving cross-set parent references.</summary>
    public FirmwareRegionSet(
        string regionSetId,
        string addressSpaceId,
        IEnumerable<FirmwareRegion> regions,
        IEnumerable<string> evidenceRefs)
        : this(
            regionSetId,
            addressSpaceId,
            regions,
            evidenceRefs,
            [],
            [])
    {
    }

    internal FirmwareRegionSet(
        string regionSetId,
        string addressSpaceId,
        IEnumerable<FirmwareRegion> regions,
        IEnumerable<string> evidenceRefs,
        IEnumerable<FirmwareRegionTemplate> regionTemplates,
        IEnumerable<FirmwareRegionInstance> regionInstances)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);

        _regionTemplates = Composition.ImmutableReferenceSnapshot.CreateUnique(
            regionTemplates,
            static template => template.TemplateId,
            "Firmware region sets cannot contain null templates.",
            "Firmware region template ids must be ordinally unique within a set.",
            StringComparer.Ordinal);
        Array.Sort(_regionTemplates, static (left, right) =>
            StringComparer.Ordinal.Compare(left.TemplateId, right.TemplateId));
        _regionInstances = Composition.ImmutableReferenceSnapshot.CreateUnique(
            regionInstances,
            static instance => instance.InstanceId,
            "Firmware region sets cannot contain null instances.",
            "Firmware region instance ids must be ordinally unique within a set.",
            StringComparer.Ordinal);
        Array.Sort(_regionInstances, static (left, right) =>
            StringComparer.Ordinal.Compare(left.InstanceId, right.InstanceId));
        DomainInvariant.Reject(
            _regionInstances.Any(instance =>
            !_regionTemplates.Any(template => ReferenceEquals(template, instance.Template))),
            "Every region instance must reference a template owned by the same region set.",
            nameof(regionInstances));

        FirmwareRegion[] regionsSnapshot = Composition.ImmutableReferenceSnapshot.CreateUnique(
            regions.Concat(_regionInstances.SelectMany(static instance => instance.ExpandRegions())),
            static region => region.RegionId,
            "Firmware region sets require non-null regions.",
            "Firmware region ids must be ordinally unique within a set.",
            StringComparer.Ordinal,
            requireValue: true);

        Array.Sort(regionsSnapshot, FirmwareRangeOrdering.Compare);
        string[] evidenceRefsSnapshot = ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            "Firmware region sets require evidence.",
            "Evidence references cannot contain null or whitespace.",
            "Evidence references must be ordinally unique.");

        RegionSetId = regionSetId;
        AddressSpaceId = addressSpaceId;
        Regions = Array.AsReadOnly(regionsSnapshot);
        RegionTemplates = Array.AsReadOnly(_regionTemplates);
        RegionInstances = Array.AsReadOnly(_regionInstances);
        EvidenceRefs = Array.AsReadOnly(evidenceRefsSnapshot);
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

    internal IReadOnlyList<FirmwareRegionTemplate> RegionTemplates { get; }

    internal IReadOnlyList<FirmwareRegionInstance> RegionInstances { get; }

    /// <summary>Evidence manifest ids in ordinal order.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }
}
