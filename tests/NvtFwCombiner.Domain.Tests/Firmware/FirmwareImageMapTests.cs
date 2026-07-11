using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests canonical physical firmware image-map graphs.</summary>
public sealed partial class FirmwareImageMapTests
{
    /// <summary>Verifies cross-set parents, explicit gaps, and immutable deterministic projections.</summary>
    [Fact]
    public void ConstructorCreatesCompleteDeterministicRegionGraph()
    {
        FirmwareRegionSet[] regionSets =
        [
            Set("nested", [
                Region("system-data", 4, 4, parentRegionId: "system-image"),
                Region("system-code", 0, 4, parentRegionId: "system-image"),
            ]),
            Set("primary", [
                Region("tp-image", 12, 4, FirmwareRegionOwner.Tp, FirmwareRegionKind.Image),
                Gap("reserved-gap", 8, 4),
                Region("system-image", 0, 8, kind: FirmwareRegionKind.Image),
            ]),
        ];
        string[] metadataSetIds = ["version-metadata", "config-metadata"];
        string[] evidenceRefs = ["evidence-z", "evidence-a"];

        FirmwareImageMap map = Create(
            regionSets: regionSets,
            metadataSetIds: metadataSetIds,
            evidenceRefs: evidenceRefs);
        regionSets[0] = Set("changed", [Region("changed", 0, 16)]);
        metadataSetIds[0] = "changed";
        evidenceRefs[0] = "changed";

        Assert.Equal("synthetic-map", map.MapId);
        Assert.Equal("flash", map.AddressSpaceId);
        Assert.Equal(16, map.CapacityBytes);
        Assert.Equal(FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps, map.CoveragePolicy);
        Assert.Equal(["nested", "primary"],
            map.RegionSets.Select(static regionSet => regionSet.RegionSetId));
        Assert.Equal(["system-image", "system-code", "system-data", "reserved-gap", "tp-image"],
            map.Regions.Select(static region => region.RegionId));
        Assert.Equal(["config-metadata", "version-metadata"], map.MetadataSetIds);
        Assert.Equal(["evidence-a", "evidence-z"], map.EvidenceRefs);

        IList<FirmwareRegionSet> setView = Assert.IsType<IList<FirmwareRegionSet>>(
            map.RegionSets,
            exactMatch: false);
        IList<FirmwareRegion> regionView = Assert.IsType<IList<FirmwareRegion>>(
            map.Regions,
            exactMatch: false);
        IList<string> metadataSetView = Assert.IsType<IList<string>>(
            map.MetadataSetIds,
            exactMatch: false);
        IList<string> evidenceView = Assert.IsType<IList<string>>(
            map.EvidenceRefs,
            exactMatch: false);
        Assert.True(setView.IsReadOnly);
        Assert.True(regionView.IsReadOnly);
        Assert.True(metadataSetView.IsReadOnly);
        Assert.True(evidenceView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => setView[0] = Set("changed", [Region("changed", 0, 16)]));
        _ = Assert.Throws<NotSupportedException>(() => regionView[0] = Region("changed", 0, 16));
        _ = Assert.Throws<NotSupportedException>(() => metadataSetView[0] = "changed");
        _ = Assert.Throws<NotSupportedException>(() => evidenceView[0] = "changed");
    }

    /// <summary>Verifies every referenced region set uses the selected map address space.</summary>
    [Fact]
    public void ConstructorRejectsAddressSpaceMismatch()
    {
        FirmwareRegionSet wrongSpace = Set(
            "wrong-space",
            [Region("root", 0, 16)],
            addressSpaceId: "other");

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [wrongSpace]));
    }

    /// <summary>Verifies region-set and flattened region identities remain unambiguous.</summary>
    [Fact]
    public void ConstructorRejectsDuplicateGraphIdentities()
    {
        FirmwareRegionSet first = Set("duplicate-set", [Region("first", 0, 8)]);
        FirmwareRegionSet second = Set("duplicate-set", [Region("second", 8, 8)]);
        FirmwareRegionSet duplicateRegion = Set("other-set", [Region("first", 8, 8)]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [first, second]));
        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [first, duplicateRegion]));
    }

    /// <summary>Verifies every parent is present in the resolved map.</summary>
    [Fact]
    public void ConstructorRejectsUnknownParent()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16),
            Region("child", 0, 4, parentRegionId: "missing"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies a physical child cannot escape its parent range.</summary>
    [Fact]
    public void ConstructorRejectsChildOutsideParent()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16),
            Region("parent", 0, 8, parentRegionId: "root"),
            Region("child", 4, 8, parentRegionId: "parent"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies parent and child ranges cannot be identical.</summary>
    [Fact]
    public void ConstructorRejectsEqualParentAndChildRanges()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16, kind: FirmwareRegionKind.Image),
            Region("child", 0, 16, parentRegionId: "root"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies equal-range parent links cannot form a cycle.</summary>
    [Fact]
    public void ConstructorRejectsParentCycles()
    {
        FirmwareRegionSet twoNodeCycle = Set("two-node-cycle", [
            Region("first", 0, 16, parentRegionId: "second"),
            Region("second", 0, 16, parentRegionId: "first"),
        ]);
        FirmwareRegionSet threeNodeCycle = Set("three-node-cycle", [
            Region("alpha", 0, 16, parentRegionId: "beta"),
            Region("beta", 0, 16, parentRegionId: "gamma"),
            Region("gamma", 0, 16, parentRegionId: "alpha"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [twoNodeCycle]));
        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [threeNodeCycle]));
    }

    /// <summary>Verifies nested siblings cannot claim the same physical bytes.</summary>
    [Fact]
    public void ConstructorRejectsSiblingOverlap()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16, kind: FirmwareRegionKind.Image),
            Region("first", 0, 10, parentRegionId: "root"),
            Region("second", 8, 8, parentRegionId: "root"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies top-level siblings cannot overlap.</summary>
    [Fact]
    public void ConstructorRejectsRootOverlap()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("first", 0, 10),
            Region("second", 8, 8),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies adjacent children form a valid exact partition.</summary>
    [Fact]
    public void ConstructorAcceptsTouchingSiblingRanges()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16, kind: FirmwareRegionKind.Image),
            Region("first", 0, 8, parentRegionId: "root"),
            Region("second", 8, 8, parentRegionId: "root"),
        ]);

        FirmwareImageMap map = Create(regionSets: [regionSet]);

        Assert.Equal(["root", "first", "second"],
            map.Regions.Select(static region => region.RegionId));
    }

    /// <summary>Verifies a full-size container cannot hide an omitted child range.</summary>
    [Fact]
    public void ConstructorRejectsImplicitChildGap()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16, kind: FirmwareRegionKind.Image),
            Region("known", 0, 8, parentRegionId: "root"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies an explicit forbidden gap completes a child partition.</summary>
    [Fact]
    public void ConstructorAcceptsExplicitReservedChildGap()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 16, kind: FirmwareRegionKind.Image),
            Region("known-left", 0, 8, parentRegionId: "root"),
            Gap("reserved-gap", 8, 4, parentRegionId: "root"),
            Region("known-right", 12, 4, parentRegionId: "root"),
        ]);

        FirmwareImageMap map = Create(regionSets: [regionSet]);

        Assert.Contains(map.Regions, static region => region.Kind == FirmwareRegionKind.Reserved);
    }

    /// <summary>Verifies all set-like inputs produce one canonical projection regardless of order.</summary>
    [Fact]
    public void ConstructorCanonicalizesEquivalentInputPermutations()
    {
        FirmwareRegionSet roots = Set("roots", [
            Region("right", 8, 8),
            Region("left", 0, 8, kind: FirmwareRegionKind.Image),
        ]);
        FirmwareRegionSet children = Set("children", [
            Region("left-b", 4, 4, parentRegionId: "left"),
            Region("left-a", 0, 4, parentRegionId: "left"),
        ]);
        FirmwareImageMap first = Create(
            regionSets: [roots, children],
            metadataSetIds: ["version", "config"],
            evidenceRefs: ["evidence-z", "evidence-a"]);
        FirmwareImageMap second = Create(
            regionSets: [children, roots],
            metadataSetIds: ["config", "version"],
            evidenceRefs: ["evidence-a", "evidence-z"]);

        Assert.Equal(
            first.RegionSets.Select(static set => set.RegionSetId),
            second.RegionSets.Select(static set => set.RegionSetId));
        Assert.Equal(
            first.Regions.Select(static region => region.RegionId),
            second.Regions.Select(static region => region.RegionId));
        Assert.Equal(first.MetadataSetIds, second.MetadataSetIds);
        Assert.Equal(first.EvidenceRefs, second.EvidenceRefs);
    }

    /// <summary>Verifies leading, internal, and trailing root gaps remain invalid.</summary>
    [Fact]
    public void ConstructorRejectsImplicitRootGaps()
    {
        FirmwareRegion[][] incompletePartitions =
        [
            [Region("leading-gap", 1, 15)],
            [Region("left", 0, 7), Region("right", 8, 8)],
            [Region("trailing-gap", 0, 15)],
        ];

        foreach (FirmwareRegion[] regions in incompletePartitions)
        {
            _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [Set("regions", regions)]));
        }
    }

    /// <summary>Verifies every physical range stays inside the exact applicability capacity.</summary>
    [Fact]
    public void ConstructorRejectsRegionPastCapacity()
    {
        FirmwareRegionSet regionSet = Set("regions", [Region("root", 0, 17)]);

        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [regionSet]));
    }

    /// <summary>Verifies map identity, coverage, set, evidence, and applicability boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(mapId: " "));
        _ = Assert.Throws<ArgumentException>(() => Create(addressSpaceId: " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Create(
            coveragePolicy: (FirmwareImageMapCoveragePolicy)int.MaxValue));
        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: []));
        _ = Assert.Throws<ArgumentException>(() => Create(regionSets: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Create(metadataSetIds: ["metadata", "metadata"]));
        _ = Assert.Throws<ArgumentException>(() => Create(metadataSetIds: [" "]));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: []));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: ["evidence", "evidence"]));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: [" "]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareImageMap(
            "synthetic-map",
            "flash",
            null!,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [Set("regions", [Region("root", 0, 16)])],
            [],
            ["evidence"]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareImageMap(
            "synthetic-map",
            "flash",
            Applicability(),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [Set("regions", [Region("root", 0, 16)])],
            null!,
            ["evidence"]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareImageMap(
            "synthetic-map",
            "flash",
            Applicability(),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [Set("regions", [Region("root", 0, 16)])],
            [],
            null!));
    }

    private static FirmwareImageMap Create(
        string mapId = "synthetic-map",
        string addressSpaceId = "flash",
        long capacityBytes = 16,
        FirmwareImageMapCoveragePolicy coveragePolicy =
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
        IEnumerable<FirmwareRegionSet>? regionSets = null,
        IEnumerable<string>? metadataSetIds = null,
        IEnumerable<string>? evidenceRefs = null)
    {
        return new FirmwareImageMap(
            mapId,
            addressSpaceId,
            Applicability(capacityBytes),
            coveragePolicy,
            regionSets ?? [Set("regions", [Region("root", 0, capacityBytes)])],
            metadataSetIds ?? [],
            evidenceRefs ?? ["evidence"]);
    }

    private static FirmwareMapApplicability Applicability(long capacityBytes = 16)
    {
        return new FirmwareMapApplicability(
            ["NT00001"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            capacityBytes);
    }

    private static FirmwareRegionSet Set(
        string regionSetId,
        IEnumerable<FirmwareRegion> regions,
        string addressSpaceId = "flash")
    {
        return new FirmwareRegionSet(regionSetId, addressSpaceId, regions, ["region-evidence"]);
    }

    private static FirmwareRegion Gap(
        string regionId,
        long start,
        long length,
        string? parentRegionId = null)
    {
        return Region(
            regionId,
            start,
            length,
            FirmwareRegionOwner.Reserved,
            FirmwareRegionKind.Reserved,
            parentRegionId);
    }

    private static FirmwareRegion Region(
        string regionId,
        long start,
        long length,
        FirmwareRegionOwner owner = FirmwareRegionOwner.System,
        FirmwareRegionKind kind = FirmwareRegionKind.Data,
        string? parentRegionId = null)
    {
        return new FirmwareRegion(
            regionId,
            parentRegionId,
            owner,
            kind,
            new ByteRange(start, length),
            FirmwareWriteConstraint.Forbidden);
    }
}
