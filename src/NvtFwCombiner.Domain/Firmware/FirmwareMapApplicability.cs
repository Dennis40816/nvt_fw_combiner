namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Three-state outcome of evaluating one firmware-map applicability shape.</summary>
internal enum FirmwareApplicabilityResult
{
    /// <summary>No known fact contradicts the shape, but a required fact is missing.</summary>
    Pending,

    /// <summary>At least one known selection fact contradicts the shape.</summary>
    NoMatch,

    /// <summary>Every required selection fact matches.</summary>
    Match,
}

/// <summary>Immutable applicability predicates for one canonical firmware-map shape.</summary>
public sealed class FirmwareMapApplicability
{
    private readonly FirmwareApplicabilityScope _scope;

    /// <summary>Creates a validated applicability shape.</summary>
    public FirmwareMapApplicability(
        IEnumerable<string> memberIds,
        IEnumerable<string> modeIds,
        TopologyRequirement topologyRequirement,
        long capacityBytes,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        IEnumerable<FirmwareMetadataPredicate>? metadataPredicates = null)
    {
        MemberIds = Array.AsReadOnly(ImmutableStringSnapshot.Create(
            memberIds,
            nameof(memberIds),
            "At least one identifier is required.",
            "Identifiers cannot contain null or whitespace values.",
            "Identifiers must be ordinally unique."));
        _scope = new FirmwareApplicabilityScope(
            modeIds,
            topologyRequirement,
            capacityBytes,
            commonFirmwareCategoryIds,
            metadataPredicates,
            "Identifiers cannot contain null or whitespace values.");
    }

    /// <summary>Accepted IC member ids in ordinal order.</summary>
    public IReadOnlyList<string> MemberIds { get; }

    /// <summary>Accepted mode ids in ordinal order.</summary>
    public IReadOnlyList<string> ModeIds => _scope.ModeIds;

    /// <summary>Required topology predicate.</summary>
    public TopologyRequirement TopologyRequirement => _scope.TopologyRequirement;

    /// <summary>Exact image capacity for this map shape.</summary>
    public long CapacityBytes => _scope.CapacityBytes;

    /// <summary>Accepted Common FW categories; empty means category-independent.</summary>
    public IReadOnlyList<string> CommonFirmwareCategoryIds => _scope.CommonFirmwareCategoryIds;

    /// <summary>Required decoded metadata predicates.</summary>
    public IReadOnlyList<FirmwareMetadataPredicate> MetadataPredicates => _scope.MetadataPredicates;

}
