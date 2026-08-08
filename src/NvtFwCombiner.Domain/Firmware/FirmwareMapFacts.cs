using System.Collections.ObjectModel;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed kinds of physical or evidence-backed facts that may bind to one member/map.</summary>
public enum FirmwareFactKind
{
    /// <summary>One canonical physical region set.</summary>
    RegionSet,

    /// <summary>One canonical metadata structure set.</summary>
    MetadataSet,

    /// <summary>One map-scoped technical capability fact.</summary>
    Capability,
}

/// <summary>Immutable value that can be bound to one member/map fact identity.</summary>
public interface IFirmwareMapFact
{
    /// <summary>Closed kind of this physical or evidence fact.</summary>
    FirmwareFactKind FactKind { get; }

    /// <summary>Stable physical identity retained across aliases.</summary>
    string CanonicalFactId { get; }

    /// <summary>Evidence for the direct physical or capability fact.</summary>
    IReadOnlyList<string> EvidenceRefs { get; }
}

/// <summary>Exact ordinal identity of one fact as exposed by one family member and image map.</summary>
public sealed record FirmwareMapFactKey
{
    /// <summary>Creates one checked member/map fact identity.</summary>
    public FirmwareMapFactKey(string memberId, string mapId, FirmwareFactKind factKind, string factId)
    {
        MemberId = RequiredValue.NotBlank(memberId);
        MapId = RequiredValue.NotBlank(mapId);
        FactId = RequiredValue.NotBlank(factId);
        ClosedEnum.ThrowIfUndefined(factKind, "Unknown firmware fact kind.");

        FactKind = factKind;
    }

    /// <summary>Family member that exposes the fact.</summary>
    public string MemberId { get; }

    /// <summary>Physical image map that exposes the fact.</summary>
    public string MapId { get; }

    /// <summary>Closed fact kind.</summary>
    public FirmwareFactKind FactKind { get; }

    /// <summary>Member/map-local fact identifier.</summary>
    public string FactId { get; }
}

internal sealed class FirmwareApplicabilityScope(
    IEnumerable<string> modeIds,
    TopologyRequirement topologyRequirement,
    long capacityBytes,
    IEnumerable<string>? commonFirmwareCategoryIds,
    IEnumerable<FirmwareMetadataPredicate>? metadataPredicates,
    string invalidIdentifierMessage)
{
    internal IReadOnlyList<string> ModeIds { get; } = Array.AsReadOnly(ImmutableStringSnapshot.Create(
        modeIds,
        nameof(modeIds),
        "At least one identifier is required.",
        invalidIdentifierMessage,
        "Identifiers must be ordinally unique."));

    internal TopologyRequirement TopologyRequirement { get; } = RequiredValue.NotNull(topologyRequirement);

    internal long CapacityBytes { get; } = RequiredValue.Positive(capacityBytes);

    internal IReadOnlyList<string> CommonFirmwareCategoryIds { get; } = Array.AsReadOnly(
        ImmutableStringSnapshot.Create(
            commonFirmwareCategoryIds ?? [],
            nameof(commonFirmwareCategoryIds),
            null,
            invalidIdentifierMessage,
            "Identifiers must be ordinally unique."));

    internal IReadOnlyList<FirmwareMetadataPredicate> MetadataPredicates { get; } = Array.AsReadOnly(
        Composition.ImmutableReferenceSnapshot.Create(
            metadataPredicates ?? [],
            "Metadata predicates cannot contain null.",
            parameterName: nameof(metadataPredicates)));
}

/// <summary>Applicability of one fact after the member identity is carried by its key.</summary>
public sealed class FirmwareFactApplicability
{
    private readonly FirmwareApplicabilityScope _scope;

    /// <summary>Creates one immutable member-independent firmware-fact scope.</summary>
    public FirmwareFactApplicability(
        IEnumerable<string> modeIds,
        TopologyRequirement topologyRequirement,
        long capacityBytes,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        IEnumerable<FirmwareMetadataPredicate>? metadataPredicates = null)
    {
        _scope = new FirmwareApplicabilityScope(
            modeIds,
            topologyRequirement,
            capacityBytes,
            commonFirmwareCategoryIds,
            metadataPredicates,
            "Identifiers cannot contain null or whitespace.");
    }

    /// <summary>Firmware modes accepted by this fact.</summary>
    public IReadOnlyList<string> ModeIds => _scope.ModeIds;

    /// <summary>Topology constraint accepted by this fact.</summary>
    public TopologyRequirement TopologyRequirement => _scope.TopologyRequirement;

    /// <summary>Exact image capacity accepted by this fact.</summary>
    public long CapacityBytes => _scope.CapacityBytes;

    /// <summary>Common FW categories accepted by this fact; empty means category-independent.</summary>
    public IReadOnlyList<string> CommonFirmwareCategoryIds => _scope.CommonFirmwareCategoryIds;

    /// <summary>Typed metadata predicates accepted by this fact.</summary>
    public IReadOnlyList<FirmwareMetadataPredicate> MetadataPredicates => _scope.MetadataPredicates;

    /// <summary>Copies the non-member portion of one canonical map applicability shape.</summary>
    public static FirmwareFactApplicability FromMap(FirmwareMapApplicability applicability)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        return new FirmwareFactApplicability(
            applicability.ModeIds,
            applicability.TopologyRequirement,
            applicability.CapacityBytes,
            applicability.CommonFirmwareCategoryIds,
            applicability.MetadataPredicates);
    }

    /// <summary>
    /// Evaluates this fact scope against one already-resolved map without using profile or execution policy.
    /// </summary>
    internal FirmwareApplicabilityResult Evaluate(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        ArgumentNullException.ThrowIfNull(resolvedMap);
        if (!ModeIds.Contains(resolvedMap.ModeId, StringComparer.Ordinal) ||
            CapacityBytes != resolvedMap.CapacityBytes)
        {
            return FirmwareApplicabilityResult.NoMatch;
        }

        bool hasPendingRequirement = CommonFirmwareCategoryIds.Count != 0;

        if (TopologyRequirement.Kind != TopologyRequirementKind.None)
        {
            if (resolvedMap.TopologySelection is null)
            {
                hasPendingRequirement = true;
            }
            else if (!TopologyRequirement.Matches(resolvedMap.TopologySelection))
            {
                return FirmwareApplicabilityResult.NoMatch;
            }
        }

        var structuresById = resolvedMap.ResolvedMetadataStructures.ToDictionary(
            static structure => structure.DecodedStructure.MetadataStructureId,
            StringComparer.Ordinal);
        foreach (FirmwareMetadataPredicate predicate in MetadataPredicates)
        {
            if (!structuresById.TryGetValue(
                predicate.MetadataStructureId,
                out FirmwareResolvedMetadataStructure? structure))
            {
                hasPendingRequirement = true;
                continue;
            }

            var fields = structure.DecodedStructure.Facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
            FirmwarePredicateResult result = predicate.Evaluate(fields).Result;
            if (result == FirmwarePredicateResult.Missing)
            {
                hasPendingRequirement = true;
                continue;
            }

            if (result == FirmwarePredicateResult.NoMatch)
            {
                return FirmwareApplicabilityResult.NoMatch;
            }
        }

        return hasPendingRequirement
            ? FirmwareApplicabilityResult.Pending
            : FirmwareApplicabilityResult.Match;
    }

}

/// <summary>One target-to-source alias edge retained by immutable fact provenance.</summary>
public sealed class FirmwareFactAliasHop(
    string aliasId,
    FirmwareMapFactKey targetKey,
    FirmwareMapFactKey sourceKey,
    FirmwareFactApplicability applicability,
    string reason,
    IEnumerable<string> evidenceRefs)
{
    /// <summary>Family-global alias identifier.</summary>
    public string AliasId { get; } = RequiredValue.NotBlank(aliasId);

    /// <summary>Effective fact identity receiving the alias.</summary>
    public FirmwareMapFactKey TargetKey { get; } = RequiredValue.NotNull(targetKey);

    /// <summary>Fact identity supplying the alias value.</summary>
    public FirmwareMapFactKey SourceKey { get; } = RequiredValue.NotNull(sourceKey);

    /// <summary>Requested non-member applicability for this alias edge.</summary>
    public FirmwareFactApplicability Applicability { get; } = RequiredValue.NotNull(applicability);

    /// <summary>Evidence-backed source explanation.</summary>
    public string Reason { get; } = RequiredValue.NotBlank(reason);

    /// <summary>Alias-specific evidence references.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; } = SnapshotEvidence(
        targetKey,
        sourceKey,
        evidenceRefs);

    private static ReadOnlyCollection<string> SnapshotEvidence(
        FirmwareMapFactKey targetKey,
        FirmwareMapFactKey sourceKey,
        IEnumerable<string> evidenceRefs)
    {
        _ = targetKey.FactKind == sourceKey.FactKind
            ? true
            : throw new ArgumentException(
                "Alias source and target must have the same fact kind.",
                nameof(sourceKey));

        return targetKey == sourceKey
            ? throw new ArgumentException("An alias cannot point to the same fact key.", nameof(sourceKey))
            : Array.AsReadOnly(ImmutableStringSnapshot.Create(
                evidenceRefs,
                nameof(evidenceRefs),
                "Alias evidence references must be non-empty values.",
                "Alias evidence references must be non-empty values.",
                "Alias evidence references must be ordinally unique."));
    }
}

/// <summary>Immutable effective-to-direct fact history used by reports and future fingerprints.</summary>
public sealed class FirmwareFactProvenance
{
    internal FirmwareFactProvenance(
        FirmwareMapFactKey effectiveKey,
        IFirmwareMapFact directFact,
        IEnumerable<FirmwareFactAliasHop> aliasChain)
    {
        EffectiveKey = RequiredValue.NotNull(effectiveKey);
        DirectFact = RequiredValue.NotNull(directFact);
        FirmwareFactAliasHop[] snapshot = SnapshotAliasChain(EffectiveKey, aliasChain);
        AliasChain = Array.AsReadOnly(snapshot);
        DirectSourceKey = snapshot.Length == 0 ? EffectiveKey : snapshot[^1].SourceKey;

        _ = ClosedEnum.IsDefined(DirectFact.FactKind) &&
            EffectiveKey.FactKind == DirectFact.FactKind &&
            DirectSourceKey.FactKind == DirectFact.FactKind
            ? true
            : throw new ArgumentException(
                "Provenance keys must match the immutable fact value kind.",
                nameof(effectiveKey));
        _ = StringComparer.Ordinal.Equals(DirectSourceKey.FactId, DirectFact.CanonicalFactId)
            ? true
            : throw new ArgumentException(
                "Direct source fact id must identify the immutable canonical fact value.",
                nameof(directFact));
    }

    /// <summary>Fact identity visible to the target member/map.</summary>
    public FirmwareMapFactKey EffectiveKey { get; }

    /// <summary>Terminal direct fact identity supplying the immutable value.</summary>
    public FirmwareMapFactKey DirectSourceKey { get; }

    /// <summary>Ordered alias edges from effective target to direct source.</summary>
    public IReadOnlyList<FirmwareFactAliasHop> AliasChain { get; }

    /// <summary>Evidence references owned by the terminal direct fact.</summary>
    public IReadOnlyList<string> DirectEvidenceRefs => DirectFact.EvidenceRefs;

    internal IFirmwareMapFact DirectFact { get; }

    private static FirmwareFactAliasHop[] SnapshotAliasChain(
        FirmwareMapFactKey effectiveKey,
        IEnumerable<FirmwareFactAliasHop> aliasChain)
    {
        FirmwareFactAliasHop[] snapshot = Composition.ImmutableReferenceSnapshot.CreateUnique(
            aliasChain,
            static hop => hop.AliasId,
            "Alias chains cannot contain null.",
            "Alias chains cannot repeat an alias id.",
            StringComparer.Ordinal);
        ValidateChain(effectiveKey, snapshot);
        return snapshot;
    }

    private static void ValidateChain(
        FirmwareMapFactKey effectiveKey,
        FirmwareFactAliasHop[] aliasChain)
    {
        FirmwareMapFactKey expectedTarget = effectiveKey;
        var visitedKeys = new HashSet<FirmwareMapFactKey> { effectiveKey };
        foreach (FirmwareFactAliasHop hop in aliasChain)
        {
            DomainInvariant.Reject(
                hop.TargetKey != expectedTarget || hop.TargetKey.FactKind != effectiveKey.FactKind,
                "Alias provenance hops must form a contiguous target-to-source chain.",
                nameof(aliasChain));

            DomainInvariant.Reject(
                !visitedKeys.Add(hop.SourceKey),
                "Alias provenance chains cannot revisit a fact key.",
                nameof(aliasChain));

            expectedTarget = hop.SourceKey;
        }
    }
}

/// <summary>One immutable fact value bound to its effective and direct member/map identities.</summary>
public sealed class FirmwareMapFactBinding<TFact>
    where TFact : class, IFirmwareMapFact
{
    internal FirmwareMapFactBinding(
        FirmwareFactApplicability applicability,
        FirmwareFactProvenance provenance)
    {
        Applicability = RequiredValue.NotNull(applicability);
        Provenance = RequiredValue.NotNull(provenance);
        Value = Provenance.DirectFact is TFact value
            ? value
            : throw new ArgumentException(
                "Provenance direct fact must match the binding value type.",
                nameof(provenance));
    }

    /// <summary>Fact identity exposed by the effective target member/map.</summary>
    public FirmwareMapFactKey EffectiveKey => Provenance.EffectiveKey;

    /// <summary>Terminal direct fact identity supplying the immutable value.</summary>
    public FirmwareMapFactKey DirectSourceKey => Provenance.DirectSourceKey;

    /// <summary>Physical fact identity that remains stable across aliases.</summary>
    public string CanonicalFactId => Value.CanonicalFactId;

    /// <summary>Immutable canonical physical or capability value.</summary>
    public TFact Value { get; }

    /// <summary>Applicability owned by this effective binding.</summary>
    public FirmwareFactApplicability Applicability { get; }

    /// <summary>Effective-to-direct immutable evidence history.</summary>
    public FirmwareFactProvenance Provenance { get; }
}
