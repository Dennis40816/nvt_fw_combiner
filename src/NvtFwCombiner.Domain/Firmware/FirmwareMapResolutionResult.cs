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

/// <summary>Closed resolver-owned reason why a viable candidate cannot yet become unique.</summary>
public enum FirmwareMapResolutionPendingKind
{
    /// <summary>A candidate requires an explicit topology selection that was not supplied.</summary>
    RequestedTopologyMissing,

    /// <summary>A candidate-scoped structure requires one artifact binding that was not supplied.</summary>
    ArtifactMissing,

    /// <summary>A candidate requires Common FW category derivation whose closed contract is unavailable.</summary>
    CommonFirmwareCategoryDerivationUnavailable,
}

/// <summary>One immutable resolver-owned requirement that prevents unique map selection.</summary>
public sealed class FirmwareMapResolutionPendingRequirement : IEquatable<FirmwareMapResolutionPendingRequirement>
{
    /// <summary>Creates one checked pending requirement.</summary>
    public FirmwareMapResolutionPendingRequirement(
        FirmwareMapResolutionPendingKind kind,
        string? artifactBindingId = null)
    {
        ClosedEnum.ThrowIfUndefined(kind, "Unknown map resolution pending kind.");

        bool requiresArtifact = kind == FirmwareMapResolutionPendingKind.ArtifactMissing;
        DomainInvariant.Reject(
            requiresArtifact && string.IsNullOrWhiteSpace(artifactBindingId),
            "Missing-artifact requirements must identify one artifact binding.",
            nameof(artifactBindingId));

        DomainInvariant.Reject(
            !requiresArtifact && artifactBindingId is not null,
            "Only missing-artifact requirements may identify one artifact binding.",
            nameof(artifactBindingId));

        Kind = kind;
        ArtifactBindingId = artifactBindingId;
    }

    /// <summary>Closed missing requirement kind.</summary>
    public FirmwareMapResolutionPendingKind Kind { get; }

    /// <summary>Required artifact binding for missing-artifact requirements; otherwise null.</summary>
    public string? ArtifactBindingId { get; }

    /// <inheritdoc />
    public bool Equals(FirmwareMapResolutionPendingRequirement? other)
    {
        return other is not null &&
            Kind == other.Kind &&
            StringComparer.Ordinal.Equals(ArtifactBindingId, other.ArtifactBindingId);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as FirmwareMapResolutionPendingRequirement);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, StringComparer.Ordinal.GetHashCode(ArtifactBindingId ?? string.Empty));
    }
}

/// <summary>Immutable public result of canonical map selection without retaining source payload bytes.</summary>
public sealed class FirmwareMapResolutionResult
{
    private readonly FirmwareMapResolutionPendingRequirement[] _pendingRequirements;

    private FirmwareMapResolutionResult(
        FirmwareMapResolutionStatus status,
        FirmwareMapResolutionRejectionKind? rejectionKind,
        IEnumerable<FirmwareMapResolutionPendingRequirement> pendingRequirements,
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

        _pendingRequirements = Composition.ImmutableReferenceSnapshot.Create(
            pendingRequirements,
            "Pending map-resolution requirements cannot contain null.");

        DomainInvariant.Reject(
            _pendingRequirements.Distinct().Count() != _pendingRequirements.Length,
            "Pending map-resolution requirements must be unique.",
            nameof(pendingRequirements));

        Array.Sort(_pendingRequirements, static (left, right) =>
        {
            int kind = left.Kind.CompareTo(right.Kind);
            return kind != 0
                ? kind
                : StringComparer.Ordinal.Compare(left.ArtifactBindingId, right.ArtifactBindingId);
        });
        bool isPending = status == FirmwareMapResolutionStatus.Pending;
        bool isRejected = status == FirmwareMapResolutionStatus.Rejected;
        bool isUnique = status == FirmwareMapResolutionStatus.Unique;
        DomainInvariant.Reject(
            isPending != (_pendingRequirements.Length != 0) ||
            isRejected != (rejectionKind is not null) ||
            isUnique != (resolvedMap is not null) ||
            (isPending && (rejectionKind is not null || resolvedMap is not null)) ||
            (isRejected && (resolvedMap is not null || _pendingRequirements.Length != 0)) ||
            (isUnique && (rejectionKind is not null || _pendingRequirements.Length != 0)),
            "Map resolution status payloads are inconsistent.");

        Status = status;
        RejectionKind = rejectionKind;
        PendingRequirements = Array.AsReadOnly(_pendingRequirements);
        ResolvedMap = resolvedMap;
    }

    /// <summary>Closed final status of the selection attempt.</summary>
    public FirmwareMapResolutionStatus Status { get; }

    /// <summary>Rejection reason when selection was rejected; otherwise null.</summary>
    public FirmwareMapResolutionRejectionKind? RejectionKind { get; }

    /// <summary>Resolver-owned missing requirements when selection is pending; otherwise empty.</summary>
    public IReadOnlyList<FirmwareMapResolutionPendingRequirement> PendingRequirements { get; }

    /// <summary>Exactly one payload-free resolved map when status is unique; otherwise null.</summary>
    public FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap? ResolvedMap { get; }

    internal static FirmwareMapResolutionResult Pending(
        IEnumerable<FirmwareMapResolutionPendingRequirement> pendingRequirements)
    {
        return new FirmwareMapResolutionResult(
            FirmwareMapResolutionStatus.Pending,
            rejectionKind: null,
            pendingRequirements,
            resolvedMap: null);
    }

    internal static FirmwareMapResolutionResult Rejected(FirmwareMapResolutionRejectionKind rejectionKind)
    {
        return new FirmwareMapResolutionResult(
            FirmwareMapResolutionStatus.Rejected,
            rejectionKind,
            [],
            resolvedMap: null);
    }

    internal static FirmwareMapResolutionResult Unique(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        return new FirmwareMapResolutionResult(
            FirmwareMapResolutionStatus.Unique,
            rejectionKind: null,
            [],
            resolvedMap);
    }
}

public sealed partial class FirmwareFamilyResolutionDefinition
{
    private static readonly object ResolvedMapConstructionToken = new();

    /// <summary>One uniquely selected canonical map with only immutable identities and decoded outcomes.</summary>
    public sealed partial class ResolvedFirmwareImageMap
    {
        private readonly FirmwareArtifactIdentity[] _artifactIdentities;
        private readonly FirmwareResolvedMetadataStructure[] _resolvedMetadataStructures;
        private readonly FirmwareMetadataPredicateOutcome[] _predicateOutcomes;
        private readonly FirmwareFactProvenance[] _factProvenance;

        internal ResolvedFirmwareImageMap(
            object constructionToken,
            FirmwareFamilyResolutionDefinition definition,
            FirmwareMapResolutionInputs inputs,
            FirmwareImageMap imageMap,
            IEnumerable<FirmwareResolvedMetadataStructure> resolvedMetadataStructures,
            IEnumerable<FirmwareMetadataPredicateOutcome> predicateOutcomes,
            IEnumerable<FirmwareMetadataStructure>? expectedMetadataStructures = null)
        {
            DomainInvariant.Reject(
                !ReferenceEquals(constructionToken, ResolvedMapConstructionToken),
                "Resolved maps may be constructed only by their owning family resolver.", nameof(constructionToken));

            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentNullException.ThrowIfNull(imageMap);
            DomainInvariant.Reject(
                !definition.ImageMaps.Any(candidate => ReferenceEquals(candidate, imageMap)),
                "The selected image map must belong to the normalized family definition.", nameof(imageMap));

            ValidateStaticSelection(imageMap, inputs);
            _artifactIdentities = SnapshotArtifactIdentities(inputs.Artifacts.Select(static artifact => artifact.Identity));
            FirmwareMetadataStructure[] expectedStructures =
            [
                .. expectedMetadataStructures ??
                   definition.GetStructuresForMap(imageMap.MapId),
            ];
            _resolvedMetadataStructures = SnapshotMetadataStructures(
                resolvedMetadataStructures,
                imageMap,
                expectedStructures,
                _artifactIdentities);
            _predicateOutcomes = SnapshotPredicateOutcomes(
                predicateOutcomes,
                imageMap,
                _resolvedMetadataStructures);
            _factProvenance = SnapshotFactProvenance(imageMap, inputs.MemberId);

            FamilyId = definition.FamilyId;
            FamilyVersion = definition.FamilyVersion;
            FamilyContentHash = definition.FamilyContentHash;
            FamilyRelationships = definition.FamilyRelationships;
            ImageMap = imageMap;
            MemberId = inputs.MemberId;
            ModeId = inputs.ModeId;
            CapacityBytes = inputs.CapacityBytes;
            TopologySelection = inputs.RequestedTopology;
            ArtifactIdentities = Array.AsReadOnly(_artifactIdentities);
            ResolvedMetadataStructures = Array.AsReadOnly(_resolvedMetadataStructures);
            PredicateOutcomes = Array.AsReadOnly(_predicateOutcomes);
            FactProvenance = Array.AsReadOnly(_factProvenance);
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

        /// <summary>Successful typed applicability predicate outcomes in declared map order.</summary>
        public IReadOnlyList<FirmwareMetadataPredicateOutcome> PredicateOutcomes { get; }

        /// <summary>Selected member/map physical fact provenance in deterministic key order.</summary>
        public IReadOnlyList<FirmwareFactProvenance> FactProvenance { get; }

        /// <summary>Canonical lowercase SHA-256 over the resolved physical map and resolver-owned outcomes.</summary>
        public string ResolutionFingerprint { get; }

        private static FirmwareArtifactIdentity[] SnapshotArtifactIdentities(
            IEnumerable<FirmwareArtifactIdentity> artifactIdentities)
        {
            FirmwareArtifactIdentity[] snapshot = Composition.ImmutableReferenceSnapshot.CreateUnique(
                artifactIdentities,
                static identity => identity.ArtifactId,
                "Resolved artifact identities must be non-null and ordinally unique.",
                "Resolved artifact identities must be non-null and ordinally unique.",
                StringComparer.Ordinal);

            Array.Sort(snapshot, static (left, right) =>
                StringComparer.Ordinal.Compare(left.ArtifactId, right.ArtifactId));
            return snapshot;
        }

        private static FirmwareResolvedMetadataStructure[] SnapshotMetadataStructures(
            IEnumerable<FirmwareResolvedMetadataStructure> resolvedMetadataStructures,
            FirmwareImageMap imageMap,
            FirmwareMetadataStructure[] expectedStructures,
            IReadOnlyList<FirmwareArtifactIdentity> artifactIdentities)
        {
            ArgumentNullException.ThrowIfNull(expectedStructures);
            FirmwareResolvedMetadataStructure[] snapshot = Composition.ImmutableReferenceSnapshot.Create(
                resolvedMetadataStructures,
                "Resolved maps may retain only unique successful metadata outcomes bound to selected identities.");
            Dictionary<string, FirmwareMetadataStructure> expectedById = expectedStructures.ToDictionary(
                static structure => structure.StructureId,
                StringComparer.Ordinal);
            DomainInvariant.Reject(
                snapshot.Length != expectedStructures.Length ||
                snapshot.Any(structure =>
                    !StringComparer.Ordinal.Equals(structure.MapId, imageMap.MapId) ||
                    !expectedById.TryGetValue(structure.DecodedStructure.MetadataStructureId, out FirmwareMetadataStructure? expected) ||
                    !StringComparer.Ordinal.Equals(
                        structure.DecodedStructure.ArtifactBindingId,
                        expected.ArtifactBindingId) ||
                    !HasExactDecodedFields(structure.DecodedStructure, expected) ||
                    !StringComparer.Ordinal.Equals(
                        structure.LocatorOutcome.ResolvedRange.AddressSpaceId,
                        imageMap.AddressSpaceId) ||
                    structure.LocatorOutcome.ResolvedRange.Range.EndExclusive > imageMap.CapacityBytes ||
                    !artifactIdentities.Any(identity =>
                        StringComparer.Ordinal.Equals(
                            identity.ArtifactId,
                            structure.DecodedStructure.ArtifactBindingId) &&
                        identity == structure.ArtifactIdentity)) ||
                snapshot.Select(static structure => structure.DecodedStructure.MetadataStructureId)
                    .Distinct(StringComparer.Ordinal).Count() != snapshot.Length,
                "Resolved maps may retain only unique successful metadata outcomes bound to selected identities.",
                nameof(resolvedMetadataStructures));

            Array.Sort(snapshot, static (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.DecodedStructure.MetadataStructureId,
                    right.DecodedStructure.MetadataStructureId));
            return snapshot;
        }

        private static FirmwareMetadataPredicateOutcome[] SnapshotPredicateOutcomes(
            IEnumerable<FirmwareMetadataPredicateOutcome> predicateOutcomes,
            FirmwareImageMap imageMap,
            IReadOnlyList<FirmwareResolvedMetadataStructure> resolvedMetadataStructures)
        {
            ArgumentNullException.ThrowIfNull(predicateOutcomes);
            FirmwareMetadataPredicateOutcome[] snapshot = [.. predicateOutcomes];
            var structuresById = resolvedMetadataStructures.ToDictionary(
                static structure => structure.DecodedStructure.MetadataStructureId,
                StringComparer.Ordinal);
            return snapshot.Length != imageMap.Applicability.MetadataPredicates.Count ||
                snapshot.Any(static outcome => outcome is null || outcome.Result != FirmwarePredicateResult.Match) ||
                snapshot.Where((outcome, index) => !ReferenceEquals(
                    outcome.Predicate,
                    imageMap.Applicability.MetadataPredicates[index])).Any() ||
                snapshot.Any(outcome => !MatchesRetainedDecodedValue(outcome, structuresById))
                ? throw new ArgumentException(
                    "Resolved maps require one successful outcome for every declared metadata predicate.",
                    nameof(predicateOutcomes))
                : snapshot;
        }

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

        private static void ValidateStaticSelection(FirmwareImageMap imageMap, FirmwareMapResolutionInputs inputs)
        {
            DomainInvariant.Reject(
                !imageMap.Applicability.MemberIds.Contains(inputs.MemberId, StringComparer.Ordinal) ||
                !imageMap.Applicability.ModeIds.Contains(inputs.ModeId, StringComparer.Ordinal) ||
                imageMap.CapacityBytes != inputs.CapacityBytes ||
                (imageMap.Applicability.TopologyRequirement.Kind != TopologyRequirementKind.None &&
                 (inputs.RequestedTopology is null ||
                  !imageMap.Applicability.TopologyRequirement.Matches(inputs.RequestedTopology))) ||
                imageMap.Applicability.CommonFirmwareCategoryIds.Count != 0,
                "Resolved map selection does not satisfy all closed static applicability.");
        }

        private static bool HasExactDecodedFields(
            FirmwareDecodedMetadataStructure decoded,
            FirmwareMetadataStructure expected)
        {
            return decoded.Facts.Count == expected.Fields.Count &&
                decoded.Facts.Zip(expected.Fields).All(pair =>
                    StringComparer.Ordinal.Equals(pair.First.FieldId, pair.Second.FieldId) &&
                    pair.Second.CanRepresent(pair.First.Value));
        }

        private static bool MatchesRetainedDecodedValue(
            FirmwareMetadataPredicateOutcome outcome,
            Dictionary<string, FirmwareResolvedMetadataStructure> structuresById)
        {
            if (!structuresById.TryGetValue(outcome.Predicate.MetadataStructureId, out FirmwareResolvedMetadataStructure? structure))
            {
                return false;
            }

            var fields = structure.DecodedStructure.Facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
            FirmwareMetadataPredicateOutcome expected = outcome.Predicate.Evaluate(fields);
            return expected.Result == FirmwarePredicateResult.Match &&
                outcome.ActualValue == expected.ActualValue;
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
