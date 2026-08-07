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

/// <summary>Applicability of one fact after the member identity is carried by its key.</summary>
public sealed class FirmwareFactApplicability(
    IEnumerable<string> modeIds,
    TopologyRequirement topologyRequirement,
    long capacityBytes,
    IEnumerable<string>? commonFirmwareCategoryIds = null,
    IEnumerable<FirmwareMetadataPredicate>? metadataPredicates = null)
{
    /// <summary>Firmware modes accepted by this fact.</summary>
    public IReadOnlyList<string> ModeIds { get; } = Array.AsReadOnly(
        ImmutableStringSnapshot.Create(
            modeIds,
            nameof(modeIds),
            "At least one identifier is required.",
            "Identifiers cannot contain null or whitespace.",
            "Identifiers must be ordinally unique."));

    /// <summary>Topology constraint accepted by this fact.</summary>
    public TopologyRequirement TopologyRequirement { get; } = RequiredValue.NotNull(topologyRequirement);

    /// <summary>Exact image capacity accepted by this fact.</summary>
    public long CapacityBytes { get; } = RequiredValue.Positive(capacityBytes);

    /// <summary>Common FW categories accepted by this fact; empty means category-independent.</summary>
    public IReadOnlyList<string> CommonFirmwareCategoryIds { get; } = Array.AsReadOnly(
        ImmutableStringSnapshot.Create(
            commonFirmwareCategoryIds ?? [],
            nameof(commonFirmwareCategoryIds),
            null,
            "Identifiers cannot contain null or whitespace.",
            "Identifiers must be ordinally unique."));

    /// <summary>Typed metadata predicates accepted by this fact.</summary>
    public IReadOnlyList<FirmwareMetadataPredicate> MetadataPredicates { get; } = Array.AsReadOnly(
        Composition.ImmutableReferenceSnapshot.Create(
            metadataPredicates ?? [],
            "Metadata predicates cannot contain null.",
            parameterName: nameof(metadataPredicates)));

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
public sealed class FirmwareFactProvenance(
    FirmwareMapFactKey effectiveKey,
    FirmwareMapFactKey directSourceKey,
    IEnumerable<FirmwareFactAliasHop> aliasChain,
    IEnumerable<string> directEvidenceRefs)
{
    /// <summary>Fact identity visible to the target member/map.</summary>
    public FirmwareMapFactKey EffectiveKey { get; } = RequiredValue.NotNull(effectiveKey);

    /// <summary>Terminal direct fact identity supplying the immutable value.</summary>
    public FirmwareMapFactKey DirectSourceKey { get; } = RequireSameKind(
        RequiredValue.NotNull(directSourceKey),
        effectiveKey);

    /// <summary>Ordered alias edges from effective target to direct source.</summary>
    public IReadOnlyList<FirmwareFactAliasHop> AliasChain { get; } = SnapshotAliasChain(
        effectiveKey,
        directSourceKey,
        aliasChain);

    /// <summary>Evidence references owned by the terminal direct fact.</summary>
    public IReadOnlyList<string> DirectEvidenceRefs { get; } = Array.AsReadOnly(
        SnapshotDirectEvidenceRefs(directEvidenceRefs));

    private static FirmwareMapFactKey RequireSameKind(
        FirmwareMapFactKey directSourceKey,
        FirmwareMapFactKey effectiveKey)
    {
        return effectiveKey.FactKind == directSourceKey.FactKind
            ? directSourceKey
            : throw new ArgumentException(
                "Effective and direct source keys must have the same fact kind.",
                nameof(directSourceKey));
    }

    private static ReadOnlyCollection<FirmwareFactAliasHop> SnapshotAliasChain(
        FirmwareMapFactKey effectiveKey,
        FirmwareMapFactKey directSourceKey,
        IEnumerable<FirmwareFactAliasHop> aliasChain)
    {
        FirmwareFactAliasHop[] snapshot = Composition.ImmutableReferenceSnapshot.CreateUnique(
            aliasChain,
            static hop => hop.AliasId,
            "Alias chains cannot contain null.",
            "Alias chains cannot repeat an alias id.",
            StringComparer.Ordinal);
        ValidateChain(effectiveKey, directSourceKey, snapshot);
        return Array.AsReadOnly(snapshot);
    }

    private static void ValidateChain(
        FirmwareMapFactKey effectiveKey,
        FirmwareMapFactKey directSourceKey,
        FirmwareFactAliasHop[] aliasChain)
    {
        if (aliasChain.Length == 0)
        {
            DomainInvariant.Reject(
                effectiveKey != directSourceKey,
                "Direct fact provenance requires equal effective and direct source keys.",
                nameof(directSourceKey));

            return;
        }

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

        DomainInvariant.Reject(
            expectedTarget != directSourceKey,
            "Alias provenance must terminate at its declared direct source key.",
            nameof(aliasChain));
    }

    private static string[] SnapshotDirectEvidenceRefs(IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        string[] snapshot = [.. evidenceRefs];
        DomainInvariant.Reject(
            snapshot.Length == 0 || snapshot.Any(string.IsNullOrWhiteSpace),
            "Direct evidence references must be non-empty values.", nameof(evidenceRefs));

        DomainInvariant.Reject(
            snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length,
            "Direct evidence references must be ordinally unique.", nameof(evidenceRefs));

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}

/// <summary>One immutable fact value bound to its effective and direct member/map identities.</summary>
public sealed class FirmwareMapFactBinding<TFact>(
    FirmwareMapFactKey effectiveKey,
    FirmwareMapFactKey directSourceKey,
    string canonicalFactId,
    TFact value,
    FirmwareFactApplicability applicability,
    FirmwareFactProvenance provenance)
    where TFact : class, IFirmwareMapFact
{
    /// <summary>Fact identity exposed by the effective target member/map.</summary>
    public FirmwareMapFactKey EffectiveKey { get; } = RequiredValue.NotNull(effectiveKey);

    /// <summary>Terminal direct fact identity supplying the immutable value.</summary>
    public FirmwareMapFactKey DirectSourceKey { get; } = RequiredValue.NotNull(directSourceKey);

    /// <summary>Physical fact identity that remains stable across aliases.</summary>
    public string CanonicalFactId { get; } = RequiredValue.NotBlank(canonicalFactId);

    /// <summary>Immutable canonical physical or capability value.</summary>
    public TFact Value { get; } = RequiredValue.NotNull(value);

    /// <summary>Applicability owned by this effective binding.</summary>
    public FirmwareFactApplicability Applicability { get; } = RequiredValue.NotNull(applicability);

    /// <summary>Effective-to-direct immutable evidence history.</summary>
    public FirmwareFactProvenance Provenance { get; } = RequireValidProvenance(
        RequiredValue.NotNull(provenance),
        effectiveKey,
        directSourceKey,
        canonicalFactId,
        value);

    private static FirmwareFactProvenance RequireValidProvenance(
        FirmwareFactProvenance provenance,
        FirmwareMapFactKey effectiveKey,
        FirmwareMapFactKey directSourceKey,
        string canonicalFactId,
        TFact value)
    {
        _ = ClosedEnum.IsDefined(value.FactKind) &&
            effectiveKey.FactKind == value.FactKind &&
            directSourceKey.FactKind == value.FactKind
            ? true
            : throw new ArgumentException(
                "Binding keys must match the immutable fact value kind.",
                nameof(effectiveKey));
        _ = StringComparer.Ordinal.Equals(canonicalFactId, value.CanonicalFactId)
            ? true
            : throw new ArgumentException(
                "Canonical fact id must match the immutable fact value.",
                nameof(canonicalFactId));
        _ = StringComparer.Ordinal.Equals(directSourceKey.FactId, canonicalFactId)
            ? true
            : throw new ArgumentException(
                "Direct source fact id must identify the immutable canonical fact value.",
                nameof(directSourceKey));

        return provenance.EffectiveKey == effectiveKey && provenance.DirectSourceKey == directSourceKey
            ? provenance
            : throw new ArgumentException("Binding keys must match its immutable provenance.", nameof(provenance));
    }
}
