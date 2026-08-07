using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>One explicit logical index to physical-field series membership.</summary>
public sealed record FirmwareMetadataFieldSeriesMember
{
    /// <summary>Creates one checked membership reference.</summary>
    public FirmwareMetadataFieldSeriesMember(int index, string fieldId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        FieldId = RequiredValue.NotBlank(fieldId);
        Index = index;
    }

    /// <summary>Owner-declared logical index.</summary>
    public int Index { get; }

    /// <summary>Exact physical field reference.</summary>
    public string FieldId { get; }
}

/// <summary>Exact active series indices for one owner-evidenced IC Count.</summary>
public sealed class FirmwareMetadataFieldSeriesApplicability
{
    private readonly int[] _activeIndices;

    /// <summary>Creates one exact IC Count table row.</summary>
    public FirmwareMetadataFieldSeriesApplicability(
        int chipCount,
        IEnumerable<int> activeIndices)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chipCount);
        ArgumentNullException.ThrowIfNull(activeIndices);
        _activeIndices = [.. activeIndices];
        DomainInvariant.Reject(
            _activeIndices.Any(static index => index < 0) ||
            _activeIndices.Distinct().Count() != _activeIndices.Length,
            "Series active indices must be nonnegative and unique.",
            nameof(activeIndices));

        Array.Sort(_activeIndices);
        ChipCount = chipCount;
        ActiveIndices = Array.AsReadOnly(_activeIndices);
    }

    /// <summary>Exact selected IC Count.</summary>
    public int ChipCount { get; }

    /// <summary>Explicitly active logical indices.</summary>
    public IReadOnlyList<int> ActiveIndices { get; }

    internal bool IsActive(int index)
    {
        return Array.BinarySearch(_activeIndices, index) >= 0;
    }
}

/// <summary>
/// One repeated semantic series that references explicitly declared physical
/// fields; it never generates geometry from an inferred stride.
/// </summary>
public sealed class FirmwareMetadataFieldSeries
{
    private readonly FirmwareMetadataFieldSeriesMember[] _members;
    private readonly FirmwareMetadataFieldSeriesApplicability[] _applicability;

    /// <summary>Creates one checked explicit field series.</summary>
    public FirmwareMetadataFieldSeries(
        string seriesId,
        IEnumerable<FirmwareMetadataFieldSeriesMember> members,
        IEnumerable<FirmwareMetadataFieldSeriesApplicability> applicability)
    {
        SeriesId = RequiredValue.NotBlank(seriesId);
        _members = ImmutableReferenceSnapshot.Create(
            members,
            "Metadata field series cannot contain null members.");
        DomainInvariant.Reject(
            _members.Length == 0 ||
            _members.Select(static member => member.Index).Distinct().Count() != _members.Length ||
            _members.Select(static member => member.FieldId)
                .Distinct(StringComparer.Ordinal).Count() != _members.Length,
            "Metadata field series require unique logical indices and physical field references.",
            nameof(members));

        Array.Sort(_members, static (left, right) => left.Index.CompareTo(right.Index));
        _applicability = ImmutableReferenceSnapshot.Create(
            applicability,
            "Metadata field series cannot contain null applicability rows.");
        DomainInvariant.Reject(
            _applicability.Length == 0,
            "Metadata field series require at least one explicit IC Count row.",
            nameof(applicability));

        DomainInvariant.Reject(
            _applicability.Select(static row => row.ChipCount).Distinct().Count() !=
            _applicability.Length,
            "Metadata field series IC Count rows must be unique.",
            nameof(applicability));

        HashSet<int> memberIndices = [.. _members.Select(static member => member.Index)];
        DomainInvariant.Reject(
            _applicability.Any(row => row.ActiveIndices.Any(index => !memberIndices.Contains(index))),
            "Metadata field series applicability references an unknown logical index.",
            nameof(applicability));

        Array.Sort(_applicability, static (left, right) =>
            left.ChipCount.CompareTo(right.ChipCount));
        Members = Array.AsReadOnly(_members);
        Applicability = Array.AsReadOnly(_applicability);
    }

    /// <summary>Stable series identity inside one structure.</summary>
    public string SeriesId { get; }

    /// <summary>Explicit logical-index-to-field memberships.</summary>
    public IReadOnlyList<FirmwareMetadataFieldSeriesMember> Members { get; }

    /// <summary>Explicit owner-evidenced IC Count rows.</summary>
    public IReadOnlyList<FirmwareMetadataFieldSeriesApplicability> Applicability { get; }

    internal FirmwareMetadataFieldApplicabilityState Resolve(
        string fieldId,
        TopologySelection? topology)
    {
        FirmwareMetadataFieldSeriesMember member = _members.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.FieldId, fieldId));
        FirmwareMetadataFieldSeriesApplicability? row =
            topology is null
                ? null
                : _applicability.FirstOrDefault(candidate =>
                    candidate.ChipCount == topology.ChipCount);
        return row is null
            ? FirmwareMetadataFieldApplicabilityState.Unknown
            : row.IsActive(member.Index)
                ? FirmwareMetadataFieldApplicabilityState.Active
                : FirmwareMetadataFieldApplicabilityState.Unused;
    }
}

/// <summary>One semantic group over exact fields and/or field series.</summary>
public sealed class FirmwareMetadataFieldGroup
{
    private readonly string[] _fieldIds;
    private readonly string[] _seriesIds;

    /// <summary>Creates one checked reference-only semantic group.</summary>
    public FirmwareMetadataFieldGroup(
        string groupId,
        IEnumerable<string> fieldIds,
        IEnumerable<string> seriesIds)
    {
        GroupId = RequiredValue.NotBlank(groupId);
        _fieldIds = SnapshotIds(fieldIds, nameof(fieldIds));
        _seriesIds = SnapshotIds(seriesIds, nameof(seriesIds));
        DomainInvariant.Reject(
            _fieldIds.Length == 0 && _seriesIds.Length == 0,
            "Metadata field groups require at least one field or series reference.",
            nameof(fieldIds));

        FieldIds = Array.AsReadOnly(_fieldIds);
        SeriesIds = Array.AsReadOnly(_seriesIds);
    }

    /// <summary>Stable group identity inside one structure.</summary>
    public string GroupId { get; }

    /// <summary>Exact scalar field references.</summary>
    public IReadOnlyList<string> FieldIds { get; }

    /// <summary>Exact series references.</summary>
    public IReadOnlyList<string> SeriesIds { get; }

    private static string[] SnapshotIds(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] snapshot = [.. values];
        DomainInvariant.Reject(
            snapshot.Any(string.IsNullOrWhiteSpace) ||
            snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length,
            "Metadata group references must be nonblank and unique.",
            parameterName);

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}
