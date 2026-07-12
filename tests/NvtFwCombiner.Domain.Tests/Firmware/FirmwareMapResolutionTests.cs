using System.Globalization;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using ResolvedFirmwareImageMap = NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests atomic canonical map selection over immutable artifact payload snapshots.</summary>
public sealed class FirmwareMapResolutionTests
{
    private const string FamilyHash = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    /// <summary>Verifies a metadata-independent map resolves with no artifact payloads.</summary>
    [Fact]
    public void ResolveMapAllowsMetadataIndependentUniqueMapWithoutArtifacts()
    {
        FirmwareImageMap map = Map("map", []);

        FirmwareMapResolutionResult result = Definition([map], []).ResolveMap(Inputs([]));

        Assert.Equal(FirmwareMapResolutionStatus.Unique, result.Status);
        ResolvedFirmwareImageMap resolved = Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap);
        Assert.Equal("map", resolved.ImageMap.MapId);
        Assert.Empty(resolved.ArtifactIdentities);
        Assert.Empty(resolved.ResolvedMetadataStructures);
        Assert.Empty(resolved.PredicateOutcomes);
        Assert.Equal(
            map.RegionSetBindings.Select(static binding => binding.EffectiveKey),
            resolved.FactProvenance.Select(static provenance => provenance.EffectiveKey));
    }

    /// <summary>Verifies an absent map-selected artifact yields one exact missing-artifact requirement.</summary>
    [Fact]
    public void ResolveMapReportsCandidateSpecificMissingArtifact()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        FirmwareImageMap map = Map("map", [metadata]);

        FirmwareMapResolutionResult result = Definition([map], [metadata]).ResolveMap(Inputs(
            [new FirmwareArtifactPayload("dp-firmware", [0x00])]));

        Assert.Equal(FirmwareMapResolutionStatus.Pending, result.Status);
        Assert.Equal(
            [new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "tp-firmware")],
            result.PendingRequirements);
        Assert.Null(result.ResolvedMap);
    }

    /// <summary>Verifies an observed locator contradiction rejects a candidate even when another required artifact is absent.</summary>
    [Fact]
    public void ResolveMapPrioritizesObservedMetadataContradictionOverMissingArtifact()
    {
        FirmwareMetadataSet metadata = MetadataSet(
            Config("a-missing", "dp-firmware"),
            Marker("z-marker", "tp-firmware"));
        FirmwareImageMap map = Map("map", [metadata]);

        FirmwareMapResolutionResult result = Definition([map], [metadata]).ResolveMap(Inputs(
            [new FirmwareArtifactPayload("tp-firmware", [0x00])]));

        Assert.Equal(FirmwareMapResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, result.RejectionKind);
        Assert.Empty(result.PendingRequirements);
    }

    /// <summary>Verifies a known metadata contradiction rejects even when Common FW derivation is otherwise pending.</summary>
    [Fact]
    public void ResolveMapPrioritizesMetadataContradictionOverCommonCategoryPending()
    {
        FirmwareMetadataSet metadata = MetadataSet(Marker("marker", "tp-firmware"));
        var map = FirmwareImageMap.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT00001"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                32,
                ["common"]),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 32),
                    FirmwareWriteConstraint.Forbidden)],
                ["region-evidence"])],
            [metadata],
            ["map-evidence"]);

        FirmwareMapResolutionResult result = Definition([map], [metadata]).ResolveMap(Inputs(
            [new FirmwareArtifactPayload("tp-firmware", [0x00])]));

        Assert.Equal(FirmwareMapResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, result.RejectionKind);
    }

    /// <summary>Verifies successful predicates retain only selected typed outcomes and all atomic input identities.</summary>
    [Fact]
    public void ResolveMapRetainsSelectedMetadataAndPredicateEvidence()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        FirmwareImageMap map = Map(
            "map",
            [metadata],
            [new FirmwareMetadataPredicate(
                "config",
                "chip-number",
                FirmwareMetadataPredicateOperator.Equal,
                [FirmwareMetadataValue.FromUnsignedInteger(2)])]);
        var tp = new FirmwareArtifactPayload("tp-firmware", [0x02]);
        var unrelated = new FirmwareArtifactPayload("dp-firmware", [0xAA]);

        FirmwareMapResolutionResult result = Definition([map], [metadata]).ResolveMap(Inputs([tp, unrelated]));

        ResolvedFirmwareImageMap resolved = Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap);
        FirmwareResolvedMetadataStructure structure = Assert.Single(resolved.ResolvedMetadataStructures);
        FirmwareMetadataPredicateOutcome predicate = Assert.Single(resolved.PredicateOutcomes);
        Assert.Equal(["dp-firmware", "tp-firmware"], resolved.ArtifactIdentities.Select(static identity => identity.ArtifactId));
        Assert.Equal("config", structure.DecodedStructure.MetadataStructureId);
        Assert.Equal(tp.Identity, structure.ArtifactIdentity);
        Assert.Equal(FirmwareMetadataLocatorKind.AbsoluteRange, structure.LocatorOutcome.LocatorKind);
        Assert.Equal(new ByteRange(0, 1), structure.LocatorOutcome.ResolvedRange.Range);
        FirmwareDecodedMetadataFact fact = Assert.Single(structure.DecodedStructure.Facts);
        Assert.Equal("tp-firmware", fact.ArtifactBindingId);
        Assert.Equal("config", fact.MetadataStructureId);
        Assert.Equal("chip-number", fact.FieldId);
        Assert.Equal(FirmwareMetadataValue.FromUnsignedInteger(2), fact.Value);
        Assert.Equal(FirmwarePredicateResult.Match, predicate.Result);
        Assert.Equal(FirmwareMetadataValue.FromUnsignedInteger(2), predicate.ActualValue);
        Assert.Same(map.Applicability.MetadataPredicates[0], predicate.Predicate);
        Assert.Equal(
            map.RegionSetBindings
                .Select(static binding => binding.Provenance)
                .Concat(map.MetadataSetBindings.Select(static binding => binding.Provenance)),
            resolved.FactProvenance);
    }

    /// <summary>Verifies decoded metadata outcomes participate in the selected-map fingerprint.</summary>
    [Fact]
    public void ResolveMapFingerprintChangesWhenDecodedMetadataChanges()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        FirmwareImageMap map = Map("map", [metadata]);
        FirmwareFamilyResolutionDefinition definition = Definition([map], [metadata]);

        ResolvedFirmwareImageMap first = Assert.IsType<ResolvedFirmwareImageMap>(
            definition.ResolveMap(Inputs([new FirmwareArtifactPayload("tp-firmware", [0x02])])).ResolvedMap);
        ResolvedFirmwareImageMap changed = Assert.IsType<ResolvedFirmwareImageMap>(
            definition.ResolveMap(Inputs([new FirmwareArtifactPayload("tp-firmware", [0x03])])).ResolvedMap);

        Assert.Equal("config", Assert.Single(first.ResolvedMetadataStructures).DecodedStructure.MetadataStructureId);
        Assert.NotEqual(first.ResolutionFingerprint, changed.ResolutionFingerprint);
    }

    /// <summary>Verifies semantically unordered predicates and expected values have one canonical fingerprint order.</summary>
    [Fact]
    public void ResolveMapFingerprintIgnoresPredicateAndOneOfDeclarationOrder()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        var equal = new FirmwareMetadataPredicate(
            "config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);
        var oneOfFirst = new FirmwareMetadataPredicate(
            "config",
            "chip-number",
            FirmwareMetadataPredicateOperator.OneOf,
            [FirmwareMetadataValue.FromUnsignedInteger(2), FirmwareMetadataValue.FromUnsignedInteger(1)]);
        var oneOfSecond = new FirmwareMetadataPredicate(
            "config",
            "chip-number",
            FirmwareMetadataPredicateOperator.OneOf,
            [FirmwareMetadataValue.FromUnsignedInteger(1), FirmwareMetadataValue.FromUnsignedInteger(2)]);

        ResolvedFirmwareImageMap first = Assert.IsType<ResolvedFirmwareImageMap>(Definition(
            [Map("map", [metadata], [oneOfFirst, equal])],
            [metadata]).ResolveMap(Inputs([new FirmwareArtifactPayload("tp-firmware", [0x02])])).ResolvedMap);
        ResolvedFirmwareImageMap reordered = Assert.IsType<ResolvedFirmwareImageMap>(Definition(
            [Map("map", [metadata], [equal, oneOfSecond])],
            [metadata]).ResolveMap(Inputs([new FirmwareArtifactPayload("tp-firmware", [0x02])])).ResolvedMap);

        Assert.Equal(first.ResolutionFingerprint, reordered.ResolutionFingerprint);
    }

    /// <summary>Verifies resolver-owned family, mode, capacity, and topology selections all affect the fingerprint.</summary>
    [Fact]
    public void ResolveMapFingerprintIncludesStaticSelectionFacts()
    {
        FirmwareImageMap baselineMap = Map(
            "map",
            [],
            memberId: "NT00001",
            modeId: "standard",
            capacityBytes: 32,
            topologyRequirement: TopologyRequirement.RequireCascade(2, 3));
        TopologySelection twoChips = new(2, "cascade", TopologySelectionSource.Requested, "chip-number");
        TopologySelection threeChips = new(3, "cascade", TopologySelectionSource.Requested, "chip-number");
        ResolvedFirmwareImageMap baseline = Resolve(
            Definition([baselineMap], []),
            Inputs([], requestedTopology: twoChips));
        ResolvedFirmwareImageMap changedFamily = Resolve(
            Definition([baselineMap], [], familyId: "synthetic-family-two"),
            Inputs([], requestedTopology: twoChips));
        ResolvedFirmwareImageMap changedMode = Resolve(
            Definition(
                [Map("map", [], modeId: "other", topologyRequirement: TopologyRequirement.RequireCascade(2, 3))],
                []),
            Inputs([], modeId: "other", requestedTopology: twoChips));
        ResolvedFirmwareImageMap changedCapacity = Resolve(
            Definition(
                [Map("map", [], capacityBytes: 64, topologyRequirement: TopologyRequirement.RequireCascade(2, 3))],
                []),
            Inputs([], capacityBytes: 64, requestedTopology: twoChips));
        ResolvedFirmwareImageMap changedTopology = Resolve(
            Definition([baselineMap], []),
            Inputs([], requestedTopology: threeChips));

        Assert.NotEqual(baseline.ResolutionFingerprint, changedFamily.ResolutionFingerprint);
        Assert.NotEqual(baseline.ResolutionFingerprint, changedMode.ResolutionFingerprint);
        Assert.NotEqual(baseline.ResolutionFingerprint, changedCapacity.ResolutionFingerprint);
        Assert.NotEqual(baseline.ResolutionFingerprint, changedTopology.ResolutionFingerprint);
    }

    /// <summary>Verifies marker-selected locator outcomes affect the fingerprint even when decoded facts are equal.</summary>
    [Fact]
    public void ResolveMapFingerprintIncludesMarkerLocatorOutcome()
    {
        byte[] artifactBytes = [0xA1, 0x02, 0xB2, 0x02];
        FirmwareMetadataSet firstMetadata = MetadataSet(MarkerConfig("config", "tp-firmware", 0xA1));
        FirmwareMetadataSet secondMetadata = MetadataSet(MarkerConfig("config", "tp-firmware", 0xB2));
        ResolvedFirmwareImageMap first = Resolve(
            Definition([Map("map", [firstMetadata])], [firstMetadata]),
            Inputs([new FirmwareArtifactPayload("tp-firmware", artifactBytes)]));
        ResolvedFirmwareImageMap second = Resolve(
            Definition([Map("map", [secondMetadata])], [secondMetadata]),
            Inputs([new FirmwareArtifactPayload("tp-firmware", artifactBytes)]));

        FirmwareResolvedMetadataStructure firstStructure = Assert.Single(first.ResolvedMetadataStructures);
        FirmwareResolvedMetadataStructure secondStructure = Assert.Single(second.ResolvedMetadataStructures);
        Assert.Equal(firstStructure.DecodedStructure.Facts.Single().Value, secondStructure.DecodedStructure.Facts.Single().Value);
        Assert.NotEqual(firstStructure.LocatorOutcome.SelectedMarkerStart, secondStructure.LocatorOutcome.SelectedMarkerStart);
        Assert.NotEqual(first.ResolutionFingerprint, second.ResolutionFingerprint);
    }

    /// <summary>Verifies metadata scalar kinds, predicate outcomes, UTF-8 evidence, and invariant number formatting have one vector.</summary>
    [Fact]
    public void ResolveMapFingerprintUsesInvariantTypedMetadataVector()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            FirmwareMetadataSet metadata = FingerprintVectorMetadataSet();
            ResolvedFirmwareImageMap resolved = Resolve(
                Definition([FingerprintVectorMap(metadata)], [metadata]),
                Inputs([new FirmwareArtifactPayload("tp-firmware", [0xFE, 0x02, 0xAA, 0x5A, 0xA1, 0x03])]));

            Assert.Equal(
                "bebb23ca88b05da44a936ff1dfd05d993e6db37a302a225b6e373e42e4614ade",
                resolved.ResolutionFingerprint);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>Verifies a supplied predicate mismatch rejects instead of remaining pending.</summary>
    [Fact]
    public void ResolveMapRejectsSuppliedPredicateMismatch()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        FirmwareImageMap map = Map(
            "map",
            [metadata],
            [new FirmwareMetadataPredicate(
                "config",
                "chip-number",
                FirmwareMetadataPredicateOperator.Equal,
                [FirmwareMetadataValue.FromUnsignedInteger(2)])]);

        FirmwareMapResolutionResult result = Definition([map], [metadata]).ResolveMap(Inputs(
            [new FirmwareArtifactPayload("tp-firmware", [0x03])]));

        Assert.Equal(FirmwareMapResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, result.RejectionKind);
    }

    /// <summary>Verifies a viable pending candidate prevents premature unique selection.</summary>
    [Fact]
    public void ResolveMapRemainsPendingWhenOneCandidateMatchesAndAnotherNeedsArtifact()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        FirmwareImageMap matching = Map("matching", []);
        FirmwareImageMap pending = Map("pending", [metadata]);

        FirmwareMapResolutionResult result = Definition([matching, pending], [metadata]).ResolveMap(Inputs([]));

        Assert.Equal(FirmwareMapResolutionStatus.Pending, result.Status);
        Assert.Equal(
            [new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "tp-firmware")],
            result.PendingRequirements);
    }

    /// <summary>Verifies pending requirements aggregate across viable candidates without duplicate artifact rows.</summary>
    [Fact]
    public void ResolveMapDeduplicatesPendingRequirementsAcrossCandidates()
    {
        FirmwareMetadataSet tpMetadata = MetadataSet("metadata-tp", Config("config-tp", "tp-firmware"));
        FirmwareMetadataSet dpMetadata = MetadataSet("metadata-dp", Config("config-dp", "dp-firmware"));
        FirmwareImageMap firstTp = Map("first-tp", [tpMetadata]);
        FirmwareImageMap secondTp = Map("second-tp", [tpMetadata]);
        FirmwareImageMap dp = Map("dp", [dpMetadata]);

        FirmwareMapResolutionResult result = Definition(
            [firstTp, secondTp, dp],
            [tpMetadata, dpMetadata]).ResolveMap(Inputs([]));

        Assert.Equal(FirmwareMapResolutionStatus.Pending, result.Status);
        Assert.Equal(
        [
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "dp-firmware"),
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "tp-firmware"),
        ],
            result.PendingRequirements);
    }

    /// <summary>Verifies ambiguity wins over a third candidate whose result remains pending.</summary>
    [Fact]
    public void ResolveMapRejectsMultipleMatchesEvenWhenAnotherCandidateIsPending()
    {
        FirmwareMetadataSet metadata = MetadataSet(Config("config", "tp-firmware"));
        FirmwareImageMap first = Map("first", []);
        FirmwareImageMap second = Map("second", []);
        FirmwareImageMap pending = Map("pending", [metadata]);

        FirmwareMapResolutionResult result = Definition([first, second, pending], [metadata]).ResolveMap(Inputs([]));

        Assert.Equal(FirmwareMapResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.AmbiguousMaps, result.RejectionKind);
    }

    /// <summary>Verifies technical capability evidence neither selects a map nor appears in resolved physical provenance.</summary>
    [Fact]
    public void ResolveMapExcludesCapabilityEvidenceFromResolvedMap()
    {
        FirmwareImageMap map = Map("map", []);
        FirmwareCapabilityFact capability = new(
            "ab-code-evidence",
            "ab-code",
            FirmwareCapabilityState.ConfirmedPresent,
            "synthetic capability evidence",
            ["capability-evidence"]);
        FirmwareMapFactKey key = new("NT00001", "map", FirmwareFactKind.Capability, "ab-code-evidence");
        var binding = new FirmwareMapFactBinding<FirmwareCapabilityFact>(
            key,
            key,
            capability.CanonicalFactId,
            capability,
            new FirmwareFactApplicability(
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                32),
            new FirmwareFactProvenance(key, key, [], capability.EvidenceRefs));
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            [map],
            [],
            [binding]);

        FirmwareMapResolutionResult result = definition.ResolveMap(Inputs([]));

        ResolvedFirmwareImageMap resolved = Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap);
        Assert.DoesNotContain(resolved.FactProvenance, provenance =>
            provenance.EffectiveKey.FactKind == FirmwareFactKind.Capability);
        Assert.DoesNotContain(resolved.FactProvenance, provenance =>
            StringComparer.Ordinal.Equals(provenance.EffectiveKey.FactId, capability.CapabilityFactId));
    }

    private static FirmwareFamilyResolutionDefinition Definition(
        IEnumerable<FirmwareImageMap> maps,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        string familyId = "synthetic-family")
    {
        return new FirmwareFamilyResolutionDefinition(
            familyId,
            "1.0.0",
            FamilyHash,
            maps,
            metadataSets);
    }

    private static FirmwareMapResolutionInputs Inputs(
        IEnumerable<FirmwareArtifactPayload> artifacts,
        string memberId = "NT00001",
        string modeId = "standard",
        long capacityBytes = 32,
        TopologySelection? requestedTopology = null)
    {
        return new FirmwareMapResolutionInputs(
            memberId,
            modeId,
            capacityBytes,
            requestedTopology,
            artifacts);
    }

    private static FirmwareImageMap Map(
        string mapId,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        IEnumerable<FirmwareMetadataPredicate>? predicates = null,
        string memberId = "NT00001",
        string modeId = "standard",
        long capacityBytes = 32,
        TopologyRequirement? topologyRequirement = null)
    {
        return FirmwareImageMap.CreateDirect(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                [memberId],
                [modeId],
                topologyRequirement ?? TopologyRequirement.NoTopologyConstraint(),
                capacityBytes,
                metadataPredicates: predicates),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, capacityBytes),
                    FirmwareWriteConstraint.Forbidden)],
                ["region-evidence"])],
            metadataSets,
            ["map-evidence"]);
    }

    private static FirmwareMetadataSet MetadataSet(params FirmwareMetadataStructure[] structures)
    {
        return MetadataSet("metadata", structures);
    }

    private static FirmwareMetadataSet MetadataSet(
        string metadataSetId,
        params FirmwareMetadataStructure[] structures)
    {
        return new FirmwareMetadataSet(metadataSetId, structures, ["metadata-evidence"]);
    }

    private static FirmwareMetadataStructure Config(string structureId, string artifactBindingId)
    {
        return new FirmwareMetadataStructure(
            structureId,
            artifactBindingId,
            1,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                "root"),
            [new FirmwareMetadataField(
                "chip-number",
                0,
                1,
                FirmwareMetadataEncoding.UnsignedInteger,
                FirmwareMetadataByteOrder.LittleEndian)],
            []);
    }

    private static FirmwareMetadataStructure Marker(string structureId, string artifactBindingId)
    {
        return new FirmwareMetadataStructure(
            structureId,
            artifactBindingId,
            1,
            new FirmwareMarkerRelativeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                [0xAA],
                new FirmwareUniqueMarkerSelection(),
                0,
                "root"),
            [],
            [FirmwareMetadataByteAssertion.Exact(0, [0xAA])]);
    }

    private static FirmwareMetadataStructure MarkerConfig(
        string structureId,
        string artifactBindingId,
        byte markerByte)
    {
        return new FirmwareMetadataStructure(
            structureId,
            artifactBindingId,
            1,
            new FirmwareMarkerRelativeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 4)),
                [markerByte],
                new FirmwareUniqueMarkerSelection(),
                1,
                "root"),
            [new FirmwareMetadataField(
                "chip-number",
                0,
                1,
                FirmwareMetadataEncoding.UnsignedInteger,
                FirmwareMetadataByteOrder.LittleEndian)],
            [FirmwareMetadataByteAssertion.Exact(0, [0x02])]);
    }

    private static FirmwareMetadataSet FingerprintVectorMetadataSet()
    {
        return MetadataSet(
            "metadata",
            new FirmwareMetadataStructure(
                "absolute",
                "tp-firmware",
                4,
                new FirmwareAbsoluteRangeLocator(
                    new FirmwareAddressedRange("flash", new ByteRange(0, 4)),
                    "root"),
                [
                    new FirmwareMetadataField(
                        "signed",
                        0,
                        1,
                        FirmwareMetadataEncoding.SignedInteger,
                        FirmwareMetadataByteOrder.LittleEndian),
                    new FirmwareMetadataField(
                        "unsigned",
                        1,
                        1,
                        FirmwareMetadataEncoding.UnsignedInteger,
                        FirmwareMetadataByteOrder.LittleEndian),
                    new FirmwareMetadataField("bytes", 2, 1, FirmwareMetadataEncoding.Bytes),
                    new FirmwareMetadataField("text", 3, 1, FirmwareMetadataEncoding.PrintableAscii),
                ],
                []),
            new FirmwareMetadataStructure(
                "marker",
                "tp-firmware",
                1,
                new FirmwareMarkerRelativeLocator(
                    new FirmwareAddressedRange("flash", new ByteRange(4, 1)),
                    [0xA1],
                    new FirmwareUniqueMarkerSelection(),
                    1,
                    "root"),
                [new FirmwareMetadataField(
                    "code",
                    0,
                    1,
                    FirmwareMetadataEncoding.UnsignedInteger,
                    FirmwareMetadataByteOrder.LittleEndian)],
                [FirmwareMetadataByteAssertion.Exact(0, [0x03])]));
    }

    private static FirmwareImageMap FingerprintVectorMap(FirmwareMetadataSet metadata)
    {
        return FirmwareImageMap.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT00001"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                32,
                metadataPredicates:
                [new FirmwareMetadataPredicate(
                    "marker",
                    "code",
                    FirmwareMetadataPredicateOperator.Equal,
                    [FirmwareMetadataValue.FromUnsignedInteger(3)])]),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 32),
                    FirmwareWriteConstraint.Forbidden)],
                ["實體-evidence"])],
            [metadata],
            ["map-證據"]);
    }

    private static ResolvedFirmwareImageMap Resolve(
        FirmwareFamilyResolutionDefinition definition,
        FirmwareMapResolutionInputs inputs)
    {
        return Assert.IsType<ResolvedFirmwareImageMap>(definition.ResolveMap(inputs).ResolvedMap);
    }
}
