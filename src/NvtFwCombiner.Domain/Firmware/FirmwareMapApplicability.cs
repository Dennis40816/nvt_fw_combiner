namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Three-state outcome of evaluating one firmware-map applicability shape.</summary>
public enum FirmwareApplicabilityResult
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
    private readonly string[] _memberIds;
    private readonly string[] _modeIds;
    private readonly string[] _commonFirmwareCategoryIds;
    private readonly FirmwareMetadataPredicate[] _metadataPredicates;

    /// <summary>Creates a validated applicability shape.</summary>
    public FirmwareMapApplicability(
        IEnumerable<string> memberIds,
        IEnumerable<string> modeIds,
        TopologyRequirement topologyRequirement,
        long capacityBytes,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        IEnumerable<FirmwareMetadataPredicate>? metadataPredicates = null)
    {
        _memberIds = SnapshotIds(memberIds, nameof(memberIds), requireValue: true);
        _modeIds = SnapshotIds(modeIds, nameof(modeIds), requireValue: true);
        _commonFirmwareCategoryIds = SnapshotIds(
            commonFirmwareCategoryIds ?? [],
            nameof(commonFirmwareCategoryIds),
            requireValue: false);
        ArgumentNullException.ThrowIfNull(topologyRequirement);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);

        _metadataPredicates = Composition.ImmutableReferenceSnapshot.Create(
            metadataPredicates ?? [],
            "Metadata predicates cannot contain null.",
            parameterName: nameof(metadataPredicates));

        MemberIds = Array.AsReadOnly(_memberIds);
        ModeIds = Array.AsReadOnly(_modeIds);
        CommonFirmwareCategoryIds = Array.AsReadOnly(_commonFirmwareCategoryIds);
        MetadataPredicates = Array.AsReadOnly(_metadataPredicates);
        TopologyRequirement = topologyRequirement;
        CapacityBytes = capacityBytes;
    }

    /// <summary>Accepted IC member ids in ordinal order.</summary>
    public IReadOnlyList<string> MemberIds { get; }

    /// <summary>Accepted mode ids in ordinal order.</summary>
    public IReadOnlyList<string> ModeIds { get; }

    /// <summary>Required topology predicate.</summary>
    public TopologyRequirement TopologyRequirement { get; }

    /// <summary>Exact image capacity for this map shape.</summary>
    public long CapacityBytes { get; }

    /// <summary>Accepted Common FW categories; empty means category-independent.</summary>
    public IReadOnlyList<string> CommonFirmwareCategoryIds { get; }

    /// <summary>Required decoded metadata predicates.</summary>
    public IReadOnlyList<FirmwareMetadataPredicate> MetadataPredicates { get; }

    /// <summary>Evaluates known selection facts and reports every unresolved requirement.</summary>
    public FirmwareMapApplicabilityEvaluation Evaluate(FirmwareMapResolutionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!_memberIds.Contains(inputs.MemberId, StringComparer.Ordinal) ||
            !_modeIds.Contains(inputs.ModeId, StringComparer.Ordinal) ||
            inputs.CapacityBytes != CapacityBytes)
        {
            return FirmwareMapApplicabilityEvaluation.NoMatch();
        }

        List<FirmwareMapPendingRequirementKind> pendingRequirements = [];
        if (TopologyRequirement.Kind != TopologyRequirementKind.None)
        {
            if (inputs.RequestedTopology is null)
            {
                pendingRequirements.Add(FirmwareMapPendingRequirementKind.RequestedTopologyMissing);
            }
            else if (!TopologyRequirement.Matches(inputs.RequestedTopology))
            {
                return FirmwareMapApplicabilityEvaluation.NoMatch();
            }
        }

        if (_commonFirmwareCategoryIds.Length != 0)
        {
            pendingRequirements.Add(
                FirmwareMapPendingRequirementKind.CommonFirmwareCategoryDerivationUnavailable);
        }

        // Metadata facts become comparable only after the candidate map scopes their
        // artifact and metadata structure during locator resolution.
        if (_metadataPredicates.Length != 0)
        {
            pendingRequirements.Add(FirmwareMapPendingRequirementKind.MetadataResolutionRequired);
        }

        return pendingRequirements.Count == 0
            ? FirmwareMapApplicabilityEvaluation.Match()
            : FirmwareMapApplicabilityEvaluation.Pending(pendingRequirements);
    }

    private static string[] SnapshotIds(
        IEnumerable<string> values,
        string parameterName,
        bool requireValue)
    {
        return ImmutableStringSnapshot.Create(
            values,
            parameterName,
            requireValue ? "At least one identifier is required." : null,
            "Identifiers cannot contain null or whitespace values.",
            "Identifiers must be ordinally unique.");
    }
}
