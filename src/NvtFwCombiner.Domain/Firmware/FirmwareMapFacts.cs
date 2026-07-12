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
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(factId);
        if (!Enum.IsDefined(factKind))
        {
            throw new ArgumentOutOfRangeException(nameof(factKind), factKind, "Unknown firmware fact kind.");
        }

        MemberId = memberId;
        MapId = mapId;
        FactKind = factKind;
        FactId = factId;
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
public sealed class FirmwareFactApplicability
{
    private readonly string[] _modeIds;
    private readonly string[] _commonFirmwareCategoryIds;
    private readonly FirmwareMetadataPredicate[] _metadataPredicates;

    /// <summary>Creates one immutable non-member fact applicability shape.</summary>
    public FirmwareFactApplicability(
        IEnumerable<string> modeIds,
        TopologyRequirement topologyRequirement,
        long capacityBytes,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        IEnumerable<FirmwareMetadataPredicate>? metadataPredicates = null)
    {
        _modeIds = SnapshotIds(modeIds, nameof(modeIds), requireValue: true);
        ArgumentNullException.ThrowIfNull(topologyRequirement);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);
        _commonFirmwareCategoryIds = SnapshotIds(
            commonFirmwareCategoryIds ?? [],
            nameof(commonFirmwareCategoryIds),
            requireValue: false);
        _metadataPredicates = [.. metadataPredicates ?? []];
        if (_metadataPredicates.Any(static predicate => predicate is null))
        {
            throw new ArgumentException("Metadata predicates cannot contain null.", nameof(metadataPredicates));
        }

        ModeIds = Array.AsReadOnly(_modeIds);
        TopologyRequirement = topologyRequirement;
        CapacityBytes = capacityBytes;
        CommonFirmwareCategoryIds = Array.AsReadOnly(_commonFirmwareCategoryIds);
        MetadataPredicates = Array.AsReadOnly(_metadataPredicates);
    }

    /// <summary>Firmware modes accepted by this fact.</summary>
    public IReadOnlyList<string> ModeIds { get; }

    /// <summary>Topology constraint accepted by this fact.</summary>
    public TopologyRequirement TopologyRequirement { get; }

    /// <summary>Exact image capacity accepted by this fact.</summary>
    public long CapacityBytes { get; }

    /// <summary>Common FW categories accepted by this fact; empty means category-independent.</summary>
    public IReadOnlyList<string> CommonFirmwareCategoryIds { get; }

    /// <summary>Typed metadata predicates accepted by this fact.</summary>
    public IReadOnlyList<FirmwareMetadataPredicate> MetadataPredicates { get; }

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

    /// <summary>Returns whether this binding shape exactly equals the non-member portion of one map.</summary>
    public bool MatchesMapApplicability(FirmwareMapApplicability applicability)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        return CapacityBytes == applicability.CapacityBytes &&
            TopologyRequirement == applicability.TopologyRequirement &&
            ModeIds.SequenceEqual(applicability.ModeIds, StringComparer.Ordinal) &&
            CommonFirmwareCategoryIds.SequenceEqual(applicability.CommonFirmwareCategoryIds, StringComparer.Ordinal) &&
            HaveSamePredicates(MetadataPredicates, applicability.MetadataPredicates);
    }

    /// <summary>Returns whether two fact applicability values have one exact physical binding shape.</summary>
    public bool HasSameShape(FirmwareFactApplicability other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CapacityBytes == other.CapacityBytes &&
            TopologyRequirement == other.TopologyRequirement &&
            ModeIds.SequenceEqual(other.ModeIds, StringComparer.Ordinal) &&
            CommonFirmwareCategoryIds.SequenceEqual(other.CommonFirmwareCategoryIds, StringComparer.Ordinal) &&
            HaveSamePredicates(MetadataPredicates, other.MetadataPredicates);
    }

    private static bool HaveSamePredicates(
        IReadOnlyList<FirmwareMetadataPredicate> left,
        IReadOnlyList<FirmwareMetadataPredicate> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        List<FirmwareMetadataPredicate> unmatched = [.. right];
        foreach (FirmwareMetadataPredicate predicate in left)
        {
            int matchIndex = unmatched.FindIndex(candidate => SamePredicate(predicate, candidate));
            if (matchIndex < 0)
            {
                return false;
            }

            unmatched.RemoveAt(matchIndex);
        }

        return true;
    }

    private static bool SamePredicate(FirmwareMetadataPredicate left, FirmwareMetadataPredicate right)
    {
        return StringComparer.Ordinal.Equals(left.MetadataStructureId, right.MetadataStructureId) &&
            StringComparer.Ordinal.Equals(left.FieldId, right.FieldId) &&
            left.Comparison == right.Comparison &&
            left.ExpectedValues.Count == right.ExpectedValues.Count &&
            left.ExpectedValues.All(value => right.ExpectedValues.Contains(value));
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
            throw new ArgumentException("Identifiers cannot contain null or whitespace.", parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Identifiers must be ordinally unique.", parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}

/// <summary>One target-to-source alias edge retained by immutable fact provenance.</summary>
public sealed class FirmwareFactAliasHop
{
    private readonly string[] _evidenceRefs;

    /// <summary>Creates one checked alias edge.</summary>
    public FirmwareFactAliasHop(
        string aliasId,
        FirmwareMapFactKey targetKey,
        FirmwareMapFactKey sourceKey,
        FirmwareFactApplicability applicability,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasId);
        ArgumentNullException.ThrowIfNull(targetKey);
        ArgumentNullException.ThrowIfNull(sourceKey);
        ArgumentNullException.ThrowIfNull(applicability);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (targetKey.FactKind != sourceKey.FactKind)
        {
            throw new ArgumentException("Alias source and target must have the same fact kind.", nameof(sourceKey));
        }

        if (targetKey == sourceKey)
        {
            throw new ArgumentException("An alias cannot point to the same fact key.", nameof(sourceKey));
        }

        _evidenceRefs = SnapshotEvidenceRefs(evidenceRefs);
        AliasId = aliasId;
        TargetKey = targetKey;
        SourceKey = sourceKey;
        Applicability = applicability;
        Reason = reason;
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Family-global alias identifier.</summary>
    public string AliasId { get; }

    /// <summary>Effective fact identity receiving the alias.</summary>
    public FirmwareMapFactKey TargetKey { get; }

    /// <summary>Fact identity supplying the alias value.</summary>
    public FirmwareMapFactKey SourceKey { get; }

    /// <summary>Requested non-member applicability for this alias edge.</summary>
    public FirmwareFactApplicability Applicability { get; }

    /// <summary>Evidence-backed source explanation.</summary>
    public string Reason { get; }

    /// <summary>Alias-specific evidence references.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

    private static string[] SnapshotEvidenceRefs(IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        string[] snapshot = [.. evidenceRefs];
        if (snapshot.Length == 0 || snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Alias evidence references must be non-empty values.", nameof(evidenceRefs));
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Alias evidence references must be ordinally unique.", nameof(evidenceRefs));
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}

/// <summary>Immutable effective-to-direct fact history used by reports and future fingerprints.</summary>
public sealed class FirmwareFactProvenance
{
    private readonly FirmwareFactAliasHop[] _aliasChain;
    private readonly string[] _directEvidenceRefs;

    /// <summary>Creates direct or aliased provenance with a contiguous target-to-source chain.</summary>
    public FirmwareFactProvenance(
        FirmwareMapFactKey effectiveKey,
        FirmwareMapFactKey directSourceKey,
        IEnumerable<FirmwareFactAliasHop> aliasChain,
        IEnumerable<string> directEvidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(effectiveKey);
        ArgumentNullException.ThrowIfNull(directSourceKey);
        if (effectiveKey.FactKind != directSourceKey.FactKind)
        {
            throw new ArgumentException("Effective and direct source keys must have the same fact kind.", nameof(directSourceKey));
        }

        ArgumentNullException.ThrowIfNull(aliasChain);
        _aliasChain = [.. aliasChain];
        if (_aliasChain.Any(static hop => hop is null))
        {
            throw new ArgumentException("Alias chains cannot contain null.", nameof(aliasChain));
        }

        if (_aliasChain.Select(static hop => hop.AliasId).Distinct(StringComparer.Ordinal).Count() != _aliasChain.Length)
        {
            throw new ArgumentException("Alias chains cannot repeat an alias id.", nameof(aliasChain));
        }

        ValidateChain(effectiveKey, directSourceKey, _aliasChain);
        _directEvidenceRefs = SnapshotDirectEvidenceRefs(directEvidenceRefs);
        EffectiveKey = effectiveKey;
        DirectSourceKey = directSourceKey;
        AliasChain = Array.AsReadOnly(_aliasChain);
        DirectEvidenceRefs = Array.AsReadOnly(_directEvidenceRefs);
    }

    /// <summary>Fact identity visible to the target member/map.</summary>
    public FirmwareMapFactKey EffectiveKey { get; }

    /// <summary>Terminal direct fact identity supplying the immutable value.</summary>
    public FirmwareMapFactKey DirectSourceKey { get; }

    /// <summary>Ordered alias edges from effective target to direct source.</summary>
    public IReadOnlyList<FirmwareFactAliasHop> AliasChain { get; }

    /// <summary>Evidence references owned by the terminal direct fact.</summary>
    public IReadOnlyList<string> DirectEvidenceRefs { get; }

    private static void ValidateChain(
        FirmwareMapFactKey effectiveKey,
        FirmwareMapFactKey directSourceKey,
        FirmwareFactAliasHop[] aliasChain)
    {
        if (aliasChain.Length == 0)
        {
            if (effectiveKey != directSourceKey)
            {
                throw new ArgumentException(
                    "Direct fact provenance requires equal effective and direct source keys.",
                    nameof(directSourceKey));
            }

            return;
        }

        FirmwareMapFactKey expectedTarget = effectiveKey;
        var visitedKeys = new HashSet<FirmwareMapFactKey> { effectiveKey };
        foreach (FirmwareFactAliasHop hop in aliasChain)
        {
            if (hop.TargetKey != expectedTarget || hop.TargetKey.FactKind != effectiveKey.FactKind)
            {
                throw new ArgumentException(
                    "Alias provenance hops must form a contiguous target-to-source chain.",
                    nameof(aliasChain));
            }

            if (!visitedKeys.Add(hop.SourceKey))
            {
                throw new ArgumentException(
                    "Alias provenance chains cannot revisit a fact key.",
                    nameof(aliasChain));
            }

            expectedTarget = hop.SourceKey;
        }

        if (expectedTarget != directSourceKey)
        {
            throw new ArgumentException(
                "Alias provenance must terminate at its declared direct source key.",
                nameof(aliasChain));
        }
    }

    private static string[] SnapshotDirectEvidenceRefs(IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        string[] snapshot = [.. evidenceRefs];
        if (snapshot.Length == 0 || snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Direct evidence references must be non-empty values.", nameof(evidenceRefs));
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Direct evidence references must be ordinally unique.", nameof(evidenceRefs));
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}

/// <summary>One immutable fact value bound to its effective and direct member/map identities.</summary>
public sealed class FirmwareMapFactBinding<TFact>
    where TFact : class, IFirmwareMapFact
{
    /// <summary>Creates one checked binding with value, applicability, and provenance kept together.</summary>
    public FirmwareMapFactBinding(
        FirmwareMapFactKey effectiveKey,
        FirmwareMapFactKey directSourceKey,
        string canonicalFactId,
        TFact value,
        FirmwareFactApplicability applicability,
        FirmwareFactProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(effectiveKey);
        ArgumentNullException.ThrowIfNull(directSourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalFactId);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(applicability);
        ArgumentNullException.ThrowIfNull(provenance);
        if (!Enum.IsDefined(value.FactKind) ||
            effectiveKey.FactKind != value.FactKind ||
            directSourceKey.FactKind != value.FactKind)
        {
            throw new ArgumentException("Binding keys must match the immutable fact value kind.", nameof(effectiveKey));
        }

        if (!StringComparer.Ordinal.Equals(canonicalFactId, value.CanonicalFactId))
        {
            throw new ArgumentException("Canonical fact id must match the immutable fact value.", nameof(canonicalFactId));
        }

        if (!StringComparer.Ordinal.Equals(directSourceKey.FactId, canonicalFactId))
        {
            throw new ArgumentException(
                "Direct source fact id must identify the immutable canonical fact value.",
                nameof(directSourceKey));
        }

        if (provenance.EffectiveKey != effectiveKey || provenance.DirectSourceKey != directSourceKey)
        {
            throw new ArgumentException("Binding keys must match its immutable provenance.", nameof(provenance));
        }

        EffectiveKey = effectiveKey;
        DirectSourceKey = directSourceKey;
        CanonicalFactId = canonicalFactId;
        Value = value;
        Applicability = applicability;
        Provenance = provenance;
    }

    /// <summary>Fact identity exposed by the effective target member/map.</summary>
    public FirmwareMapFactKey EffectiveKey { get; }

    /// <summary>Terminal direct fact identity supplying the immutable value.</summary>
    public FirmwareMapFactKey DirectSourceKey { get; }

    /// <summary>Physical fact identity that remains stable across aliases.</summary>
    public string CanonicalFactId { get; }

    /// <summary>Immutable canonical physical or capability value.</summary>
    public TFact Value { get; }

    /// <summary>Applicability owned by this effective binding.</summary>
    public FirmwareFactApplicability Applicability { get; }

    /// <summary>Effective-to-direct immutable evidence history.</summary>
    public FirmwareFactProvenance Provenance { get; }
}
