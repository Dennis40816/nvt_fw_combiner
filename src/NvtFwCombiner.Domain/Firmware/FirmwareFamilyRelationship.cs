using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed owner-declared relationship kinds for firmware semantics.</summary>
public enum FirmwareFamilyRelationshipKind
{
    /// <summary>All modeled firmware semantics are shared without member override.</summary>
    PerfectLikeFamily,

    /// <summary>Only Initial Code geometry and its owned metadata are shared.</summary>
    InitialCodeSharedFamily,

    /// <summary>Only TP geometry and its owned metadata are shared.</summary>
    TpSharedFamily,
}

/// <summary>
/// Immutable normalized relationship. Partial relationships retain references
/// to the canonical metadata definitions they share and never copy fields or
/// offsets.
/// </summary>
public sealed class FirmwareFamilyRelationship
{
    private readonly string[] _memberIds;
    private readonly string[] _sharedRegionIds;
    private readonly FirmwareMetadataStructureDefinition[] _metadataDefinitions;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates one normalized, firmware-semantic-only relationship.</summary>
    public FirmwareFamilyRelationship(
        string relationshipId,
        FirmwareFamilyRelationshipKind kind,
        IEnumerable<string> memberIds,
        IEnumerable<string> sharedRegionIds,
        IEnumerable<FirmwareMetadataStructureDefinition> metadataDefinitions,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _memberIds = SnapshotStrings(memberIds, nameof(memberIds));
        if (_memberIds.Length < 2)
        {
            throw new ArgumentException(
                "Family relationships require at least two distinct members.",
                nameof(memberIds));
        }

        _sharedRegionIds = SnapshotStrings(sharedRegionIds, nameof(sharedRegionIds));
        _metadataDefinitions = ImmutableReferenceSnapshot.CreateUnique(
            metadataDefinitions,
            static definition => definition.DefinitionId,
            "Family relationship metadata definitions cannot contain null.",
            "Family relationship metadata definition ids must be ordinally unique.",
            StringComparer.Ordinal);
        Array.Sort(
            _metadataDefinitions,
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.DefinitionId, right.DefinitionId));
        _evidenceRefs = SnapshotStrings(evidenceRefs, nameof(evidenceRefs));
        if (_evidenceRefs.Length == 0)
        {
            throw new ArgumentException(
                "Family relationships require evidence references.",
                nameof(evidenceRefs));
        }

        if (kind == FirmwareFamilyRelationshipKind.PerfectLikeFamily &&
            (_sharedRegionIds.Length != 0 || _metadataDefinitions.Length != 0))
        {
            throw new ArgumentException(
                "Perfect-like relationships own the complete family definition and cannot declare a partial scope.",
                nameof(kind));
        }

        if (kind != FirmwareFamilyRelationshipKind.PerfectLikeFamily &&
            _sharedRegionIds.Length == 0)
        {
            throw new ArgumentException(
                "Shared-part relationships require at least one shared region.",
                nameof(sharedRegionIds));
        }

        RelationshipId = relationshipId;
        Kind = kind;
        Reason = reason;
        MemberIds = Array.AsReadOnly(_memberIds);
        SharedRegionIds = Array.AsReadOnly(_sharedRegionIds);
        MetadataDefinitions = Array.AsReadOnly(_metadataDefinitions);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable relationship identifier.</summary>
    public string RelationshipId { get; }

    /// <summary>Closed relationship kind.</summary>
    public FirmwareFamilyRelationshipKind Kind { get; }

    /// <summary>Explicit owner-declared members.</summary>
    public IReadOnlyList<string> MemberIds { get; }

    /// <summary>Exact shared physical region ids for partial relationships.</summary>
    public IReadOnlyList<string> SharedRegionIds { get; }

    /// <summary>Same immutable canonical logical metadata definitions owned by the shared part.</summary>
    public IReadOnlyList<FirmwareMetadataStructureDefinition> MetadataDefinitions { get; }

    /// <summary>Owner-backed reason for the relationship.</summary>
    public string Reason { get; }

    /// <summary>Relationship evidence references; not evidence classification or publication authority.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    private static string[] SnapshotStrings(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] snapshot = [.. values];
        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Values cannot contain null or whitespace.", parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Values must be ordinally unique.", parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}
