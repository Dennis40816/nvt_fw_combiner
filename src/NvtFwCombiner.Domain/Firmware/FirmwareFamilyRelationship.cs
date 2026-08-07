namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed author-facing roles for one partial shared-fact relationship.</summary>
public enum FirmwareSharedFactRole
{
    /// <summary>Initial Code geometry and its explicitly referenced metadata.</summary>
    InitialCodeShared,

    /// <summary>TP geometry and its explicitly referenced metadata.</summary>
    TpShared,

    /// <summary>Only explicitly referenced TP Flash Header facts.</summary>
    TpFlashHeaderShared,

    /// <summary>Only explicitly referenced DiffDLM facts.</summary>
    DiffDlmShared,
}

/// <summary>Closed kinds of canonical facts that a shared-fact relationship may reference.</summary>
public enum FirmwareSharedFactKind
{
    /// <summary>One exact physical region selected from every applicable map.</summary>
    Region,

    /// <summary>One exact immutable canonical metadata definition.</summary>
    MetadataDefinition,
}

/// <summary>
/// One resolved typed reference to an admitted canonical firmware fact. The
/// selected kind determines which typed value is present; display role never
/// participates in resolution.
/// </summary>
public sealed class FirmwareSharedFactReference
{
    private FirmwareSharedFactReference(
        FirmwareSharedFactKind kind,
        string factId,
        FirmwareRegion? region,
        FirmwareMetadataStructureDefinition? metadataDefinition)
    {
        ClosedEnum.ThrowIfUndefined(kind, "Unknown shared firmware fact kind.");

        ArgumentException.ThrowIfNullOrWhiteSpace(factId);
        bool isRegion = kind == FirmwareSharedFactKind.Region;
        DomainInvariant.Reject(
            isRegion != (region is not null) ||
            isRegion == (metadataDefinition is not null),
            "A shared firmware fact reference requires exactly the typed value selected by its kind.",
            nameof(kind));

        DomainInvariant.Reject(
            region is not null &&
            !StringComparer.Ordinal.Equals(factId, region.RegionId),
            "Shared region fact id must match the canonical region.",
            nameof(factId));

        DomainInvariant.Reject(
            metadataDefinition is not null &&
            !StringComparer.Ordinal.Equals(factId, metadataDefinition.DefinitionId),
            "Shared metadata-definition fact id must match the canonical definition.",
            nameof(factId));

        Kind = kind;
        FactId = factId;
        Region = region;
        MetadataDefinition = metadataDefinition;
    }

    /// <summary>Closed canonical fact kind.</summary>
    public FirmwareSharedFactKind Kind { get; }

    /// <summary>Stable canonical fact identifier.</summary>
    public string FactId { get; }

    /// <summary>Canonical region when <see cref="Kind"/> is <see cref="FirmwareSharedFactKind.Region"/>.</summary>
    public FirmwareRegion? Region { get; }

    /// <summary>
    /// Canonical metadata definition when <see cref="Kind"/> is
    /// <see cref="FirmwareSharedFactKind.MetadataDefinition"/>.
    /// </summary>
    public FirmwareMetadataStructureDefinition? MetadataDefinition { get; }

    /// <summary>Creates one exact canonical region reference.</summary>
    public static FirmwareSharedFactReference ForRegion(FirmwareRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return new FirmwareSharedFactReference(
            FirmwareSharedFactKind.Region,
            region.RegionId,
            region,
            null);
    }

    /// <summary>Creates one exact canonical metadata-definition reference.</summary>
    public static FirmwareSharedFactReference ForMetadataDefinition(
        FirmwareMetadataStructureDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new FirmwareSharedFactReference(
            FirmwareSharedFactKind.MetadataDefinition,
            definition.DefinitionId,
            null,
            definition);
    }
}

/// <summary>Common immutable identity, membership, reason, and evidence for one family relationship.</summary>
public abstract class FirmwareFamilyRelationship
{
    private readonly string[] _memberIds;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates the shared owner-declared relationship envelope.</summary>
    protected FirmwareFamilyRelationship(
        string relationshipId,
        IEnumerable<string> memberIds,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _memberIds = ImmutableStringSnapshot.Create(
            memberIds,
            nameof(memberIds),
            requiredMessage: null,
            invalidValueMessage: "Family relationship members cannot contain null or whitespace.",
            duplicateMessage: "Family relationship members must be ordinally unique.");
        DomainInvariant.Reject(
            _memberIds.Length < 2,
            "Family relationships require at least two distinct members.",
            nameof(memberIds));

        _evidenceRefs = ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            requiredMessage: "Family relationships require evidence references.",
            invalidValueMessage: "Family relationship evidence cannot contain null or whitespace.",
            duplicateMessage: "Family relationship evidence must be ordinally unique.");

        RelationshipId = relationshipId;
        Reason = reason;
        MemberIds = Array.AsReadOnly(_memberIds);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable relationship identifier.</summary>
    public string RelationshipId { get; }

    /// <summary>Explicit owner-declared members.</summary>
    public IReadOnlyList<string> MemberIds { get; }

    /// <summary>Owner-backed reason for the relationship.</summary>
    public string Reason { get; }

    /// <summary>Relationship evidence; never support, publication, or execution authority.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

}

/// <summary>
/// Strong complete-equivalence relationship. Members consume one family-owned
/// modeled firmware definition and cannot carry partial semantic overrides.
/// </summary>
public sealed class PerfectFamilyRelationship : FirmwareFamilyRelationship
{
    /// <summary>Creates one owner-declared complete perfect family.</summary>
    public PerfectFamilyRelationship(
        string relationshipId,
        IEnumerable<string> memberIds,
        string reason,
        IEnumerable<string> evidenceRefs)
        : base(relationshipId, memberIds, reason, evidenceRefs)
    {
    }
}

/// <summary>
/// Partial relationship that reuses only exact typed canonical facts on exact
/// canonical maps. The readable role never selects runtime behavior.
/// </summary>
public sealed class SharedFactRelationship : FirmwareFamilyRelationship
{
    private readonly FirmwareImageMap[] _applicableMaps;
    private readonly FirmwareSharedFactReference[] _sharedFactReferences;

    /// <summary>Creates one checked partial shared-fact relationship.</summary>
    public SharedFactRelationship(
        string relationshipId,
        FirmwareSharedFactRole role,
        IEnumerable<string> memberIds,
        IEnumerable<FirmwareImageMap> applicableMaps,
        IEnumerable<FirmwareSharedFactReference> sharedFactReferences,
        string reason,
        IEnumerable<string> evidenceRefs)
        : base(relationshipId, memberIds, reason, evidenceRefs)
    {
        ClosedEnum.ThrowIfUndefined(role, "Unknown shared-fact role.");

        _applicableMaps = Composition.ImmutableReferenceSnapshot.CreateUnique(
            applicableMaps,
            static map => map.MapId,
            "Shared-fact applicability cannot contain null maps.",
            "Shared-fact applicability map ids must be ordinally unique.",
            StringComparer.Ordinal,
            requireValue: false);
        DomainInvariant.Reject(
            _applicableMaps.Length == 0,
            "Shared-fact relationships require at least one applicable map.",
            nameof(applicableMaps));

        _sharedFactReferences = Composition.ImmutableReferenceSnapshot.Create(
            sharedFactReferences,
            "Shared-fact relationships cannot contain null references.",
            parameterName: nameof(sharedFactReferences));
        DomainInvariant.Reject(
            _sharedFactReferences.Length == 0,
            "Shared-fact relationships require at least one typed fact reference.",
            nameof(sharedFactReferences));

        DomainInvariant.Reject(
            _sharedFactReferences
            .Select(static reference => (reference.Kind, reference.FactId))
            .Distinct()
            .Count() != _sharedFactReferences.Length,
            "Shared-fact relationship references must be unique by kind and fact id.",
            nameof(sharedFactReferences));

        ValidateMemberCoverage(_applicableMaps, MemberIds);
        ValidateCanonicalReferenceIdentity(_applicableMaps, _sharedFactReferences);
        Array.Sort(
            _applicableMaps,
            static (left, right) => StringComparer.Ordinal.Compare(left.MapId, right.MapId));
        Array.Sort(_sharedFactReferences, CompareFactReferences);
        Role = role;
        ApplicableMaps = Array.AsReadOnly(_applicableMaps);
        SharedFactReferences = Array.AsReadOnly(_sharedFactReferences);
    }

    /// <summary>Readable author-facing role that grants no behavior.</summary>
    public FirmwareSharedFactRole Role { get; }

    /// <summary>Exact canonical maps on which these fact references apply.</summary>
    public IReadOnlyList<FirmwareImageMap> ApplicableMaps { get; }

    /// <summary>Exact typed canonical facts shared by the declared members.</summary>
    public IReadOnlyList<FirmwareSharedFactReference> SharedFactReferences { get; }

    private static void ValidateMemberCoverage(
        IReadOnlyList<FirmwareImageMap> applicableMaps,
        IReadOnlyList<string> memberIds)
    {
        var relationshipMembers = new HashSet<string>(memberIds, StringComparer.Ordinal);
        DomainInvariant.Reject(
            applicableMaps.Any(map =>
            map.Applicability.MemberIds.Any(memberId =>
                !relationshipMembers.Contains(memberId))),
            "Shared-fact applicability maps cannot admit members outside the relationship.",
            nameof(applicableMaps));

        DomainInvariant.Reject(
            memberIds.Any(memberId => applicableMaps.All(map =>
            !map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal))),
            "Shared-fact applicability maps must cover every relationship member.",
            nameof(applicableMaps));
    }

    private static void ValidateCanonicalReferenceIdentity(
        IReadOnlyList<FirmwareImageMap> applicableMaps,
        IReadOnlyList<FirmwareSharedFactReference> sharedFactReferences)
    {
        foreach (FirmwareImageMap map in applicableMaps)
        {
            foreach (FirmwareSharedFactReference reference in sharedFactReferences)
            {
                bool isCanonical = reference.Kind switch
                {
                    FirmwareSharedFactKind.Region =>
                        map.Regions.Any(candidate =>
                            StringComparer.Ordinal.Equals(
                                candidate.RegionId,
                                reference.FactId) &&
                            ReferenceEquals(candidate, reference.Region)),
                    FirmwareSharedFactKind.MetadataDefinition =>
                        HasExactMetadataDefinition(map, reference),
                    _ => false,
                };
                DomainInvariant.Reject(
                    !isCanonical,
                    $"Shared fact '{reference.Kind}:{reference.FactId}' must reuse one exact canonical value on applicable map '{map.MapId}'.",
                    nameof(sharedFactReferences));
            }
        }
    }

    private static bool HasExactMetadataDefinition(
        FirmwareImageMap map,
        FirmwareSharedFactReference reference)
    {
        FirmwareMetadataStructureDefinition[] definitions =
        [
            .. map.MetadataSetBindings
                .Select(static binding => binding.Value)
                .SelectMany(static set => set.Structures)
                .Where(structure => StringComparer.Ordinal.Equals(
                    structure.Definition.DefinitionId,
                    reference.FactId))
                .Select(static structure => structure.Definition),
        ];
        return definitions.Length != 0 &&
            definitions.All(definition =>
                ReferenceEquals(definition, reference.MetadataDefinition));
    }

    private static int CompareFactReferences(
        FirmwareSharedFactReference left,
        FirmwareSharedFactReference right)
    {
        int kind = left.Kind.CompareTo(right.Kind);
        return kind != 0
            ? kind
            : StringComparer.Ordinal.Compare(left.FactId, right.FactId);
    }
}

/// <summary>Identifies the relationship whose family-level invariant failed.</summary>
internal sealed class FirmwareFamilyRelationshipInvariantException(
    string relationshipId,
    string message) : ArgumentException(message)
{
    internal string RelationshipId { get; } = relationshipId;
}
