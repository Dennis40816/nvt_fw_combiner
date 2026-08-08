namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed outcome of selecting one canonical firmware image map.</summary>
public enum FirmwareMapResolutionStatus
{
    /// <summary>One or more viable candidates require resolver-owned facts that are unavailable.</summary>
    Pending,

    /// <summary>No candidate matched or more than one fully resolved candidate matched.</summary>
    Rejected,

    /// <summary>Exactly one candidate fully resolved with all required evidence.</summary>
    Unique,
}

/// <summary>Closed reason for a rejected map selection.</summary>
public enum FirmwareMapResolutionRejectionKind
{
    /// <summary>No candidate map satisfied the supplied selections and artifact evidence.</summary>
    NoMatchingMap,

    /// <summary>More than one candidate map fully satisfied the supplied selections and evidence.</summary>
    AmbiguousMaps,
}

/// <summary>Immutable public result of canonical map selection without retaining source payload bytes.</summary>
public sealed class FirmwareMapResolutionResult
{
    private FirmwareMapResolutionResult(
        FirmwareMapResolutionStatus status,
        FirmwareMapResolutionRejectionKind? rejectionKind,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap? resolvedMap)
    {
        ClosedEnum.ThrowIfUndefined(status, "Unknown map resolution status.");

        if (rejectionKind is { } knownRejectionKind && !ClosedEnum.IsDefined(knownRejectionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rejectionKind),
                rejectionKind,
                "Unknown map resolution rejection kind.");
        }

        bool isPending = status == FirmwareMapResolutionStatus.Pending;
        bool isRejected = status == FirmwareMapResolutionStatus.Rejected;
        bool isUnique = status == FirmwareMapResolutionStatus.Unique;
        DomainInvariant.Reject(
            isRejected != (rejectionKind is not null) ||
            isUnique != (resolvedMap is not null) ||
            (isPending && (rejectionKind is not null || resolvedMap is not null)) ||
            (isRejected && resolvedMap is not null) ||
            (isUnique && rejectionKind is not null),
            "Map resolution status payloads are inconsistent.");

        Status = status;
        RejectionKind = rejectionKind;
        ResolvedMap = resolvedMap;
    }

    /// <summary>Closed final status of the selection attempt.</summary>
    public FirmwareMapResolutionStatus Status { get; }

    /// <summary>Rejection reason when selection was rejected; otherwise null.</summary>
    public FirmwareMapResolutionRejectionKind? RejectionKind { get; }

    /// <summary>Exactly one payload-free resolved map when status is unique; otherwise null.</summary>
    public FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap? ResolvedMap { get; }

    internal static FirmwareMapResolutionResult Pending()
    {
        return new FirmwareMapResolutionResult(
            FirmwareMapResolutionStatus.Pending,
            rejectionKind: null,
            resolvedMap: null);
    }

    internal static FirmwareMapResolutionResult Rejected(FirmwareMapResolutionRejectionKind rejectionKind)
    {
        return new FirmwareMapResolutionResult(
            FirmwareMapResolutionStatus.Rejected,
            rejectionKind,
            resolvedMap: null);
    }

    internal static FirmwareMapResolutionResult Unique(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        return new FirmwareMapResolutionResult(
            FirmwareMapResolutionStatus.Unique,
            rejectionKind: null,
            resolvedMap);
    }
}

public sealed partial class FirmwareFamilyResolutionDefinition
{
    private static readonly object ResolvedMapConstructionToken = new();

    /// <summary>One uniquely selected canonical map with only immutable identities and decoded outcomes.</summary>
    public sealed partial class ResolvedFirmwareImageMap
    {
        internal ResolvedFirmwareImageMap(
            object constructionToken,
            FirmwareFamilyResolutionDefinition definition,
            FirmwareMapResolutionInputs inputs,
            FirmwareImageMap imageMap,
            IEnumerable<FirmwareResolvedMetadataStructure> resolvedMetadataStructures,
            IEnumerable<FirmwareMetadataPredicateOutcome> predicateOutcomes)
        {
            DomainInvariant.Reject(
                !ReferenceEquals(constructionToken, ResolvedMapConstructionToken),
                "Resolved maps may be constructed only by their owning family resolver.",
                nameof(constructionToken));

            FirmwareArtifactIdentity[] artifactIdentitiesSnapshot = [.. inputs.Artifacts.Select(static artifact => artifact.Identity)];
            FirmwareResolvedMetadataStructure[] resolvedMetadataStructuresSnapshot = [.. resolvedMetadataStructures];
            Array.Sort(resolvedMetadataStructuresSnapshot, static (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.DecodedStructure.MetadataStructureId,
                    right.DecodedStructure.MetadataStructureId));
            FirmwareMetadataPredicateOutcome[] predicateOutcomesSnapshot = [.. predicateOutcomes];
            FirmwareFactProvenance[] factProvenanceSnapshot = SnapshotFactProvenance(imageMap, inputs.MemberId);

            FamilyId = definition.FamilyId;
            FamilyVersion = definition.FamilyVersion;
            FamilyContentHash = definition.FamilyContentHash;
            FamilyRelationships = definition.FamilyRelationships;
            ImageMap = imageMap;
            MemberId = inputs.MemberId;
            ModeId = inputs.ModeId;
            CapacityBytes = inputs.CapacityBytes;
            TopologySelection = inputs.RequestedTopology;
            ArtifactIdentities = Array.AsReadOnly(artifactIdentitiesSnapshot);
            ResolvedMetadataStructures = Array.AsReadOnly(resolvedMetadataStructuresSnapshot);
            PredicateOutcomes = Array.AsReadOnly(predicateOutcomesSnapshot);
            FactProvenance = Array.AsReadOnly(factProvenanceSnapshot);
            ResolutionFingerprint = CalculateResolutionFingerprint(this);
        }

        /// <summary>Trusted source-family identifier.</summary>
        public string FamilyId { get; }

        internal IReadOnlyList<FirmwareFamilyRelationship> FamilyRelationships { get; }

        /// <summary>Trusted source-family version.</summary>
        public string FamilyVersion { get; }

        /// <summary>Trusted immutable source-family hash.</summary>
        public string FamilyContentHash { get; }

        /// <summary>Exactly one selected immutable canonical image map.</summary>
        public FirmwareImageMap ImageMap { get; }

        /// <summary>Selected IC member inside the image-map applicability.</summary>
        public string MemberId { get; }

        /// <summary>Selected mode inside the image-map applicability.</summary>
        public string ModeId { get; }

        /// <summary>Selected exact physical image capacity.</summary>
        public long CapacityBytes { get; }

        /// <summary>Requested or resolver-derived topology selection when the closed derivation exists.</summary>
        public TopologySelection? TopologySelection { get; }

        /// <summary>All identities in the atomic resolution input set; source bytes are never retained.</summary>
        public IReadOnlyList<FirmwareArtifactIdentity> ArtifactIdentities { get; }

        /// <summary>Successful candidate-scoped metadata locator and decode outcomes.</summary>
        public IReadOnlyList<FirmwareResolvedMetadataStructure> ResolvedMetadataStructures { get; }

        internal IReadOnlyList<FirmwareMetadataPredicateOutcome> PredicateOutcomes { get; }

        /// <summary>Selected member/map physical fact provenance in deterministic key order.</summary>
        public IReadOnlyList<FirmwareFactProvenance> FactProvenance { get; }

        /// <summary>Canonical lowercase SHA-256 over the resolved physical map and resolver-owned outcomes.</summary>
        public string ResolutionFingerprint { get; }

        private static FirmwareFactProvenance[] SnapshotFactProvenance(FirmwareImageMap imageMap, string memberId)
        {
            FirmwareFactProvenance[] snapshot =
            [
                .. imageMap.RegionSetBindings
                    .Where(binding => StringComparer.Ordinal.Equals(binding.EffectiveKey.MemberId, memberId))
                    .Select(static binding => binding.Provenance)
                    .Concat(imageMap.MetadataSetBindings
                        .Where(binding => StringComparer.Ordinal.Equals(binding.EffectiveKey.MemberId, memberId))
                        .Select(static binding => binding.Provenance)),
            ];
            Array.Sort(snapshot, static (left, right) => CompareFactKeys(left.EffectiveKey, right.EffectiveKey));
            return snapshot;
        }

        private static int CompareFactKeys(FirmwareMapFactKey left, FirmwareMapFactKey right)
        {
            int kind = left.FactKind.CompareTo(right.FactKind);
            return kind != 0
                ? kind
                : StringComparer.Ordinal.Compare(left.FactId, right.FactId);
        }
    }
}
