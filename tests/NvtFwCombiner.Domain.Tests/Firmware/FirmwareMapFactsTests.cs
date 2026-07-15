using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests immutable member/map fact identities, bindings, and provenance.</summary>
public sealed class FirmwareMapFactsTests
{
    /// <summary>Verifies map-scoped capability evidence is immutable and remains a non-executable fact value.</summary>
    [Fact]
    public void CapabilityFactCreatesImmutableEvidenceValue()
    {
        string[] evidenceRefs = ["evidence-z", "evidence-a"];
        FirmwareCapabilityFact capability = new(
            "ab-code-evidence",
            "ab-code",
            FirmwareCapabilityState.ConfirmedPresent,
            "synthetic evidence",
            evidenceRefs);
        evidenceRefs[0] = "changed";

        Assert.Equal(FirmwareFactKind.Capability, capability.FactKind);
        Assert.Equal("ab-code-evidence", capability.CanonicalFactId);
        Assert.Equal(["evidence-a", "evidence-z"], capability.EvidenceRefs);
        Assert.Equal(FirmwareCapabilityState.ConfirmedPresent, capability.State);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareCapabilityFact(
            "invalid",
            "ab-code",
            (FirmwareCapabilityState)int.MaxValue,
            "synthetic evidence",
            ["evidence"]));
    }

    /// <summary>Verifies direct bindings retain their complete effective identity and terminal evidence.</summary>
    [Fact]
    public void DirectBindingRetainsEffectiveAndDirectIdentity()
    {
        FirmwareRegionSet regionSet = RegionSet();
        FirmwareMapFactBinding<FirmwareRegionSet> binding = DirectBinding("NT00001", "map", regionSet);

        Assert.Equal(new FirmwareMapFactKey("NT00001", "map", FirmwareFactKind.RegionSet, "regions"),
            binding.EffectiveKey);
        Assert.Equal(binding.EffectiveKey, binding.DirectSourceKey);
        Assert.Equal("regions", binding.CanonicalFactId);
        Assert.Same(regionSet, binding.Value);
        Assert.Empty(binding.Provenance.AliasChain);
        Assert.Equal(["region-evidence"], binding.Provenance.DirectEvidenceRefs);
    }

    /// <summary>Verifies alias provenance is target-to-source contiguous and retains immutable evidence snapshots.</summary>
    [Fact]
    public void AliasProvenanceRequiresContiguousTargetToSourceChain()
    {
        FirmwareMapFactKey effective = new("NT00001", "map-a", FirmwareFactKind.RegionSet, "regions");
        FirmwareMapFactKey intermediate = new("NT00002", "map-b", FirmwareFactKind.RegionSet, "regions");
        FirmwareMapFactKey direct = new("NT00003", "map-c", FirmwareFactKind.RegionSet, "regions");
        string[] directEvidence = ["z", "a"];
        FirmwareFactAliasHop first = Hop("alias-a", effective, intermediate);
        FirmwareFactAliasHop second = Hop("alias-b", intermediate, direct);

        FirmwareFactProvenance provenance = new(effective, direct, [first, second], directEvidence);
        directEvidence[0] = "changed";

        Assert.Equal(["alias-a", "alias-b"], provenance.AliasChain.Select(static hop => hop.AliasId));
        Assert.Equal(["a", "z"], provenance.DirectEvidenceRefs);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareFactProvenance(
            effective,
            direct,
            [second, first],
            ["direct"]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareFactProvenance(
            effective,
            direct,
            [first],
            ["direct"]));
        FirmwareFactAliasHop cycleBack = Hop("alias-cycle", intermediate, effective);
        FirmwareFactAliasHop leaveCycle = Hop("alias-leave", effective, direct);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareFactProvenance(
            effective,
            direct,
            [first, cycleBack, leaveCycle],
            ["direct"]));
    }

    /// <summary>Verifies keys and binding values cannot disagree about fact kind, physical identity, or provenance.</summary>
    [Fact]
    public void BindingRejectsMismatchedKindCanonicalIdAndProvenance()
    {
        FirmwareRegionSet regionSet = RegionSet();
        FirmwareMapFactKey effective = new("NT00001", "map", FirmwareFactKind.RegionSet, "regions");
        FirmwareFactProvenance provenance = new(effective, effective, [], ["region-evidence"]);
        FirmwareFactApplicability applicability = Applicability();

        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapFactBinding<FirmwareRegionSet>(
            new FirmwareMapFactKey("NT00001", "map", FirmwareFactKind.MetadataSet, "regions"),
            effective,
            "regions",
            regionSet,
            applicability,
            provenance));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapFactBinding<FirmwareRegionSet>(
            effective,
            effective,
            "different",
            regionSet,
            applicability,
            provenance));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapFactBinding<FirmwareRegionSet>(
            effective,
            effective,
            "regions",
            regionSet,
            applicability,
            new FirmwareFactProvenance(
                new FirmwareMapFactKey("NT00002", "map", FirmwareFactKind.RegionSet, "regions"),
                new FirmwareMapFactKey("NT00002", "map", FirmwareFactKind.RegionSet, "regions"),
                [],
                ["region-evidence"])));
    }

    /// <summary>Verifies a direct-source fact id always identifies the immutable canonical value.</summary>
    [Fact]
    public void BindingRejectsDirectSourceIdentityDifferentFromCanonicalValue()
    {
        FirmwareRegionSet regionSet = RegionSet();
        FirmwareFactApplicability applicability = Applicability();
        FirmwareMapFactKey wrongDirect = new("NT00001", "map", FirmwareFactKind.RegionSet, "wrong-source");

        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapFactBinding<FirmwareRegionSet>(
            wrongDirect,
            wrongDirect,
            regionSet.CanonicalFactId,
            regionSet,
            applicability,
            new FirmwareFactProvenance(wrongDirect, wrongDirect, [], regionSet.EvidenceRefs)));

        FirmwareMapFactKey effective = new("NT00001", "map", FirmwareFactKind.RegionSet, "target-regions");
        FirmwareMapFactKey aliasedWrongDirect = new("NT00002", "source-map", FirmwareFactKind.RegionSet, "wrong-source");
        FirmwareFactAliasHop hop = Hop("alias", effective, aliasedWrongDirect);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapFactBinding<FirmwareRegionSet>(
            effective,
            aliasedWrongDirect,
            regionSet.CanonicalFactId,
            regionSet,
            applicability,
            new FirmwareFactProvenance(effective, aliasedWrongDirect, [hop], regionSet.EvidenceRefs)));
    }

    /// <summary>Verifies alias-free map construction creates one direct binding per member and fact reference.</summary>
    [Fact]
    public void CreateDirectBuildsMemberBindingsAndDerivesCompatibilityProjections()
    {
        FirmwareRegionSet regionSet = RegionSet();
        FirmwareMetadataSet metadataSet = MetadataSet();
        var map = FirmwareImageMap.CreateDirect(
            "map",
            "flash",
            MapApplicability(["NT00001", "NT00002"]),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [regionSet],
            [metadataSet],
            ["map-evidence"]);

        Assert.Equal(2, map.RegionSetBindings.Count);
        Assert.Equal(2, map.MetadataSetBindings.Count);
        Assert.All(map.RegionSetBindings, binding =>
        {
            Assert.Equal(binding.EffectiveKey, binding.DirectSourceKey);
            Assert.Empty(binding.Provenance.AliasChain);
            Assert.Same(regionSet, binding.Value);
        });
        Assert.All(map.MetadataSetBindings, binding => Assert.Same(metadataSet, binding.Value));
        Assert.Equal(["regions"], map.RegionSets.Select(static value => value.RegionSetId));
        Assert.Equal(["metadata"], map.MetadataSetIds);
        Assert.Equal(["root"], map.Regions.Select(static region => region.RegionId));
    }

    /// <summary>Verifies member-scoped bindings cannot omit a map member or reuse one effective key.</summary>
    [Fact]
    public void MapRejectsIncompleteOrDuplicateMemberBindings()
    {
        FirmwareRegionSet regionSet = RegionSet();
        FirmwareMapFactBinding<FirmwareRegionSet> first = DirectBinding("NT00001", "map", regionSet);
        FirmwareMapFactBinding<FirmwareRegionSet> second = DirectBinding("NT00002", "map", regionSet);
        FirmwareMapApplicability applicability = MapApplicability(["NT00001", "NT00002"]);

        _ = Assert.Throws<ArgumentException>(() => new FirmwareImageMap(
            "map",
            "flash",
            applicability,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [first],
            [],
            ["map-evidence"]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareImageMap(
            "map",
            "flash",
            applicability,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [first, first, second],
            [],
            ["map-evidence"]));
    }

    /// <summary>Verifies one canonical fact id cannot conceal independently created physical values.</summary>
    [Fact]
    public void MapRejectsDivergentValuesForOneCanonicalFact()
    {
        FirmwareRegionSet firstValue = RegionSet();
        FirmwareRegionSet secondValue = RegionSet();
        FirmwareMapFactBinding<FirmwareRegionSet> first = DirectBinding("NT00001", "map", firstValue);
        FirmwareMapFactBinding<FirmwareRegionSet> second = DirectBinding("NT00002", "map", secondValue);

        _ = Assert.Throws<ArgumentException>(() => new FirmwareImageMap(
            "map",
            "flash",
            MapApplicability(["NT00001", "NT00002"]),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [first, second],
            [],
            ["map-evidence"]));
    }

    /// <summary>Verifies physical bindings cannot narrow their map or alias-hop applicability.</summary>
    [Fact]
    public void MapRejectsConditionalPhysicalBindingApplicability()
    {
        FirmwareRegionSet regionSet = RegionSet();
        FirmwareMapApplicability mapApplicability = MapApplicability(["NT00001"]);
        FirmwareFactApplicability[] mismatchedShapes =
        [
            Applicability(modeIds: ["ab"]),
            Applicability(capacityBytes: 32),
            Applicability(topologyRequirement: TopologyRequirement.RequireSingleChip()),
        ];

        foreach (FirmwareFactApplicability bindingApplicability in mismatchedShapes)
        {
            _ = Assert.Throws<ArgumentException>(() => new FirmwareImageMap(
                "map",
                "flash",
                mapApplicability,
                FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
                [DirectBinding("NT00001", "map", regionSet, bindingApplicability)],
                [],
                ["map-evidence"]));
        }

        FirmwareMapFactKey effective = new("NT00001", "map", FirmwareFactKind.RegionSet, "target-regions");
        FirmwareMapFactKey direct = new("NT00002", "source-map", FirmwareFactKind.RegionSet, "regions");
        FirmwareFactApplicability mapShape = Applicability();
        FirmwareFactAliasHop mismatchedHop = Hop("alias", effective, direct, Applicability(modeIds: ["ab"]));
        FirmwareMapFactBinding<FirmwareRegionSet> binding = new(
            effective,
            direct,
            "regions",
            regionSet,
            mapShape,
            new FirmwareFactProvenance(effective, direct, [mismatchedHop], regionSet.EvidenceRefs));

        _ = Assert.Throws<ArgumentException>(() => new FirmwareImageMap(
            "map",
            "flash",
            mapApplicability,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [binding],
            [],
            ["map-evidence"]));
    }

    private static FirmwareMapFactBinding<TFact> DirectBinding<TFact>(
        string memberId,
        string mapId,
        TFact value,
        FirmwareFactApplicability? applicability = null)
        where TFact : class, IFirmwareMapFact
    {
        FirmwareMapFactKey key = new(memberId, mapId, value.FactKind, value.CanonicalFactId);
        return new FirmwareMapFactBinding<TFact>(
            key,
            key,
            value.CanonicalFactId,
            value,
            applicability ?? Applicability(),
            new FirmwareFactProvenance(key, key, [], value.EvidenceRefs));
    }

    private static FirmwareFactAliasHop Hop(
        string aliasId,
        FirmwareMapFactKey target,
        FirmwareMapFactKey source,
        FirmwareFactApplicability? applicability = null)
    {
        return new FirmwareFactAliasHop(
            aliasId,
            target,
            source,
            applicability ?? Applicability(),
            "synthetic",
            ["alias-evidence"]);
    }

    private static FirmwareMapApplicability MapApplicability(IReadOnlyList<string> memberIds)
    {
        return new FirmwareMapApplicability(
            memberIds,
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            16);
    }

    private static FirmwareFactApplicability Applicability(
        IReadOnlyList<string>? modeIds = null,
        TopologyRequirement? topologyRequirement = null,
        long capacityBytes = 16)
    {
        return new FirmwareFactApplicability(
            modeIds ?? ["standard"],
            topologyRequirement ?? TopologyRequirement.NoTopologyConstraint(),
            capacityBytes);
    }

    private static FirmwareRegionSet RegionSet()
    {
        return new FirmwareRegionSet(
            "regions",
            "flash",
            [
                new FirmwareRegion(
                    "root",
                    null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 16),
                    FirmwareWriteConstraint.Forbidden),
            ],
            ["region-evidence"]);
    }

    private static FirmwareMetadataSet MetadataSet()
    {
        return new FirmwareMetadataSet(
            "metadata",
            [
                new FirmwareMetadataStructure(
                    "config",
                    "tp-firmware",
                    1,
                    new FirmwareAbsoluteRangeLocator(
                        new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                        "root"),
                    [],
                    []),
            ],
            ["metadata-evidence"]);
    }
}
