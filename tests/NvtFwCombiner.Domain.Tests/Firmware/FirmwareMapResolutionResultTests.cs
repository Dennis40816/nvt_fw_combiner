using System.Reflection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using ResolvedFirmwareImageMap = NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests the closed payload-free result model used by canonical map selection.</summary>
public sealed class FirmwareMapResolutionResultTests
{
    private const string FamilyHash = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    /// <summary>Verifies pending requirements are typed, immutable values with exact artifact scope only when needed.</summary>
    [Fact]
    public void PendingRequirementRequiresArtifactOnlyForMissingArtifact()
    {
        var artifact = new FirmwareMapResolutionPendingRequirement(
            FirmwareMapResolutionPendingKind.ArtifactMissing,
            "tp-firmware");
        var topology = new FirmwareMapResolutionPendingRequirement(
            FirmwareMapResolutionPendingKind.RequestedTopologyMissing);

        Assert.Equal("tp-firmware", artifact.ArtifactBindingId);
        Assert.Null(topology.ArtifactBindingId);
        Assert.Equal(artifact, new FirmwareMapResolutionPendingRequirement(
            FirmwareMapResolutionPendingKind.ArtifactMissing,
            "tp-firmware"));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapResolutionPendingRequirement(
            FirmwareMapResolutionPendingKind.ArtifactMissing));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapResolutionPendingRequirement(
            FirmwareMapResolutionPendingKind.RequestedTopologyMissing,
            "tp-firmware"));
    }

    /// <summary>Verifies pending and rejected results cannot expose a selected map or raw candidate state.</summary>
    [Fact]
    public void ResultUsesClosedStatusPayloads()
    {
        var pending = FirmwareMapResolutionResult.Pending(
        [
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "z-artifact"),
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.RequestedTopologyMissing),
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "a-artifact"),
        ]);
        var rejected = FirmwareMapResolutionResult.Rejected(
            FirmwareMapResolutionRejectionKind.AmbiguousMaps);

        Assert.Equal(FirmwareMapResolutionStatus.Pending, pending.Status);
        Assert.Null(pending.RejectionKind);
        Assert.Null(pending.ResolvedMap);
        Assert.Equal(
        [
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.RequestedTopologyMissing),
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "a-artifact"),
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                "z-artifact"),
        ],
            pending.PendingRequirements);
        Assert.Equal(FirmwareMapResolutionStatus.Rejected, rejected.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.AmbiguousMaps, rejected.RejectionKind);
        Assert.Empty(rejected.PendingRequirements);
        Assert.Null(rejected.ResolvedMap);
        Assert.Equal(
            FirmwareMapResolutionRejectionKind.NoMatchingMap,
            FirmwareMapResolutionResult.Rejected(FirmwareMapResolutionRejectionKind.NoMatchingMap).RejectionKind);
        _ = Assert.Throws<ArgumentException>(() => FirmwareMapResolutionResult.Pending([]));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMapResolutionResult.Pending(
        [
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.RequestedTopologyMissing),
            new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.RequestedTopologyMissing),
        ]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => FirmwareMapResolutionResult.Rejected(
            (FirmwareMapResolutionRejectionKind)999));
        Assert.Empty(typeof(FirmwareMapResolutionResult).GetConstructors());
    }

    /// <summary>Verifies a unique result retains all input identities and selected physical provenance but no source payload.</summary>
    [Fact]
    public void UniqueResultSnapshotsPayloadFreeSelectionEvidence()
    {
        FirmwareImageMap map = Map();
        FirmwareFamilyResolutionDefinition definition = Definition(map);
        byte[] source = [0x01, 0x02];
        var zArtifact = new FirmwareArtifactPayload("z-artifact", source);
        var aArtifact = new FirmwareArtifactPayload("a-artifact", [0x03]);
        source[0] = 0xFF;
        FirmwareMapResolutionResult result = definition.ResolveMap(Inputs(zArtifact, aArtifact));
        ResolvedFirmwareImageMap resolved = Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap);

        Assert.Equal(FirmwareMapResolutionStatus.Unique, result.Status);
        Assert.Same(resolved, result.ResolvedMap);
        Assert.Equal(["a-artifact", "z-artifact"], resolved.ArtifactIdentities.Select(static identity => identity.ArtifactId));
        Assert.Empty(resolved.ResolvedMetadataStructures);
        Assert.Empty(resolved.PredicateOutcomes);
        Assert.Equal(
            map.RegionSetBindings.Select(static binding => binding.EffectiveKey),
            resolved.FactProvenance.Select(static provenance => provenance.EffectiveKey));
        Assert.DoesNotContain(
            typeof(ResolvedFirmwareImageMap).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            field => IsPayloadContainer(field.FieldType));
        Assert.DoesNotContain(
            typeof(ResolvedFirmwareImageMap).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => IsPayloadContainer(property.PropertyType));
        Assert.Empty(typeof(ResolvedFirmwareImageMap).GetConstructors());
    }

    /// <summary>Verifies resolution fingerprints are canonical, payload-free, and sensitive to atomic artifact identity.</summary>
    [Fact]
    public void UniqueResultCalculatesCanonicalResolutionFingerprint()
    {
        FirmwareFamilyResolutionDefinition definition = Definition(Map());
        var firstA = new FirmwareArtifactPayload("a-artifact", [0x01]);
        var firstZ = new FirmwareArtifactPayload("z-artifact", [0x02]);
        var changedZ = new FirmwareArtifactPayload("z-artifact", [0x03]);

        ResolvedFirmwareImageMap first = Assert.IsType<ResolvedFirmwareImageMap>(
            definition.ResolveMap(Inputs(firstZ, firstA)).ResolvedMap);
        ResolvedFirmwareImageMap reordered = Assert.IsType<ResolvedFirmwareImageMap>(
            definition.ResolveMap(Inputs(firstA, firstZ)).ResolvedMap);
        ResolvedFirmwareImageMap changed = Assert.IsType<ResolvedFirmwareImageMap>(
            definition.ResolveMap(Inputs(firstA, changedZ)).ResolvedMap);

        Assert.Equal(
            "6db193dc60ba9d7793605c6a9acab7377131112b8c75c205f76b38fc6ab5f09c",
            first.ResolutionFingerprint);
        Assert.Equal(first.ResolutionFingerprint, reordered.ResolutionFingerprint);
        Assert.NotEqual(first.ResolutionFingerprint, changed.ResolutionFingerprint);
    }

    /// <summary>Verifies physical direct and alias evidence are both part of the selected-map fingerprint.</summary>
    [Fact]
    public void UniqueResultFingerprintIncludesPhysicalFactProvenance()
    {
        ResolvedFirmwareImageMap directOne = Assert.IsType<ResolvedFirmwareImageMap>(
            Definition(Map(regionEvidence: "region-evidence-one")).ResolveMap(Inputs()).ResolvedMap);
        ResolvedFirmwareImageMap directTwo = Assert.IsType<ResolvedFirmwareImageMap>(
            Definition(Map(regionEvidence: "region-evidence-two")).ResolveMap(Inputs()).ResolvedMap);
        ResolvedFirmwareImageMap aliasOne = Assert.IsType<ResolvedFirmwareImageMap>(
            Definition(AliasedMap("alias-evidence-one")).ResolveMap(Inputs()).ResolvedMap);
        ResolvedFirmwareImageMap aliasTwo = Assert.IsType<ResolvedFirmwareImageMap>(
            Definition(AliasedMap("alias-evidence-two")).ResolveMap(Inputs()).ResolvedMap);

        Assert.NotEqual(directOne.ResolutionFingerprint, directTwo.ResolutionFingerprint);
        Assert.NotEqual(aliasOne.ResolutionFingerprint, aliasTwo.ResolutionFingerprint);
        Assert.Contains(aliasOne.FactProvenance, static provenance => provenance.AliasChain.Count == 1);
    }

    /// <summary>Verifies maps missing a topology or Common FW derivation cannot become unique.</summary>
    [Fact]
    public void ResolveMapKeepsIncompleteStaticApplicabilityPending()
    {
        FirmwareMapResolutionResult topologyPending = Definition(Map(
            topologyRequirement: TopologyRequirement.RequireSingleChip())).ResolveMap(Inputs());
        FirmwareMapResolutionResult categoryPending = Definition(Map(
            commonFirmwareCategoryIds: ["common"])).ResolveMap(Inputs());
        FirmwareMapResolutionResult topologyMismatch = Definition(Map(
            topologyRequirement: TopologyRequirement.RequireSingleChip())).ResolveMap(Inputs(
            requestedTopology: new TopologySelection(
                2,
                "cascade",
                TopologySelectionSource.Requested,
                "compile-request")));

        Assert.Equal(FirmwareMapResolutionStatus.Pending, topologyPending.Status);
        Assert.Equal(
            [new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.RequestedTopologyMissing)],
            topologyPending.PendingRequirements);
        Assert.Equal(FirmwareMapResolutionStatus.Pending, categoryPending.Status);
        Assert.Equal(
            [new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.CommonFirmwareCategoryDerivationUnavailable)],
            categoryPending.PendingRequirements);
        Assert.Equal(FirmwareMapResolutionStatus.Rejected, topologyMismatch.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, topologyMismatch.RejectionKind);
    }

    /// <summary>Verifies a friend assembly cannot forge resolver authority with an arbitrary construction token.</summary>
    [Fact]
    public void ResolvedMapRejectsFabricatedConstructionToken()
    {
        FirmwareImageMap map = Map();
        FirmwareFamilyResolutionDefinition definition = Definition(map);

        _ = Assert.Throws<ArgumentException>(() => new ResolvedFirmwareImageMap(
            new object(),
            definition,
            Inputs(),
            map,
            [],
            []));
    }

    private static FirmwareFamilyResolutionDefinition Definition(FirmwareImageMap map)
    {
        return new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            [map],
            []);
    }

    private static FirmwareMapResolutionInputs Inputs(
        FirmwareArtifactPayload? first = null,
        FirmwareArtifactPayload? second = null,
        TopologySelection? requestedTopology = null)
    {
        return new FirmwareMapResolutionInputs(
            "NT00001",
            "standard",
            32,
            requestedTopology,
            [.. new[] { first, second }.Where(static artifact => artifact is not null)!]);
    }

    private static FirmwareImageMap Map(
        TopologyRequirement? topologyRequirement = null,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        string regionEvidence = "region-evidence")
    {
        return FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT00001"],
                ["standard"],
                topologyRequirement ?? TopologyRequirement.NoTopologyConstraint(),
                32,
                commonFirmwareCategoryIds),
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
                [regionEvidence])],
            [],
            ["map-evidence"]);
    }

    private static FirmwareImageMap AliasedMap(string aliasEvidence)
    {
        var applicability = new FirmwareMapApplicability(
            ["NT00001"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            32);
        var regionSet = new FirmwareRegionSet(
            "physical",
            "flash",
            [new FirmwareRegion(
                "root",
                parentRegionId: null,
                FirmwareRegionOwner.System,
                FirmwareRegionKind.Image,
                new ByteRange(0, 32),
                FirmwareWriteConstraint.Forbidden)],
            ["direct-evidence"]);
        var target = new FirmwareMapFactKey("NT00001", "map", FirmwareFactKind.RegionSet, "alias-physical");
        var source = new FirmwareMapFactKey("NT00001", "map", FirmwareFactKind.RegionSet, "physical");
        var provenance = new FirmwareFactProvenance(
            target,
            source,
            [new FirmwareFactAliasHop(
                "physical-alias",
                target,
                source,
                FirmwareFactApplicability.FromMap(applicability),
                "Synthetic physical fact inheritance.",
                [aliasEvidence])],
            regionSet.EvidenceRefs);
        var binding = new FirmwareMapFactBinding<FirmwareRegionSet>(
            target,
            source,
            regionSet.CanonicalFactId,
            regionSet,
            FirmwareFactApplicability.FromMap(applicability),
            provenance);

        return new FirmwareImageMap(
            "map",
            "flash",
            applicability,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [binding],
            [],
            ["map-evidence"]);
    }

    private static bool IsPayloadContainer(Type type)
    {
        return type == typeof(FirmwareArtifactPayload) ||
            type == typeof(FirmwareMapResolutionInputs) ||
            type == typeof(byte[]) ||
            type == typeof(Memory<byte>) ||
            type == typeof(ReadOnlyMemory<byte>) ||
            (type.HasElementType && IsPayloadContainer(type.GetElementType()!)) ||
            (type.IsGenericType && type.GetGenericArguments().Any(IsPayloadContainer));
    }
}
