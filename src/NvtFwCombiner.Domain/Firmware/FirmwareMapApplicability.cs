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

        _metadataPredicates = [.. metadataPredicates ?? []];
        if (_metadataPredicates.Any(static predicate => predicate is null))
        {
            throw new ArgumentException("Metadata predicates cannot contain null.", nameof(metadataPredicates));
        }

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

    /// <summary>Evaluates known selection facts without guessing missing values.</summary>
    public FirmwareApplicabilityResult Evaluate(FirmwareMapResolutionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!_memberIds.Contains(inputs.MemberId, StringComparer.Ordinal) ||
            !_modeIds.Contains(inputs.ModeId, StringComparer.Ordinal) ||
            inputs.CapacityBytes != CapacityBytes)
        {
            return FirmwareApplicabilityResult.NoMatch;
        }

        bool pending = false;
        if (TopologyRequirement.Kind != TopologyRequirementKind.None)
        {
            if (inputs.TopologySelection is null)
            {
                pending = true;
            }
            else if (!TopologyRequirement.Matches(inputs.TopologySelection))
            {
                return FirmwareApplicabilityResult.NoMatch;
            }
        }

        if (_commonFirmwareCategoryIds.Length != 0)
        {
            if (inputs.CommonFirmwareCategory is null)
            {
                pending = true;
            }
            else if (!_commonFirmwareCategoryIds.Contains(
                inputs.CommonFirmwareCategory.CategoryId,
                StringComparer.Ordinal))
            {
                return FirmwareApplicabilityResult.NoMatch;
            }
        }

        // Metadata facts become comparable only after the candidate map scopes their
        // artifact and metadata structure during locator resolution.
        pending |= _metadataPredicates.Length != 0;

        return pending ? FirmwareApplicabilityResult.Pending : FirmwareApplicabilityResult.Match;
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
            throw new ArgumentException("Identifiers cannot contain null or whitespace values.", parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Identifiers must be ordinally unique.", parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}
