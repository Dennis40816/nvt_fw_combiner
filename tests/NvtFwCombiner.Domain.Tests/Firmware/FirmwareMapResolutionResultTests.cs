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
        IEnumerable<string>? commonFirmwareCategoryIds = null)
    {
        return FirmwareImageMap.CreateDirect(
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
                ["region-evidence"])],
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
