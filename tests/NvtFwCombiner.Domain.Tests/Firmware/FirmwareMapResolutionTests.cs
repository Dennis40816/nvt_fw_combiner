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
        IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        return new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            maps,
            metadataSets);
    }

    private static FirmwareMapResolutionInputs Inputs(IEnumerable<FirmwareArtifactPayload> artifacts)
    {
        return new FirmwareMapResolutionInputs(
            "NT00001",
            "standard",
            32,
            requestedTopology: null,
            artifacts);
    }

    private static FirmwareImageMap Map(
        string mapId,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        IEnumerable<FirmwareMetadataPredicate>? predicates = null)
    {
        return FirmwareImageMap.CreateDirect(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                ["NT00001"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                32,
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
                    new ByteRange(0, 32),
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
}
