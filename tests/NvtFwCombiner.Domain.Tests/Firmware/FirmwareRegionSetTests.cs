using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests immutable physical firmware-region sets.</summary>
public sealed class FirmwareRegionSetTests
{
    /// <summary>Verifies regions and evidence are snapshotted in deterministic order.</summary>
    [Fact]
    public void ConstructorCreatesImmutableDeterministicSnapshots()
    {
        FirmwareRegion[] regions =
        [
            Region("later", 8, 4),
            Region("child", 0, 4, parentRegionId: "root"),
            Region("root", 0, 8, FirmwareRegionKind.Image),
        ];
        string[] evidenceRefs = ["evidence-z", "evidence-a"];

        var regionSet = new FirmwareRegionSet("primary-regions", "flash", regions, evidenceRefs);
        regions[0] = Region("changed", 16, 4);
        evidenceRefs[0] = "changed";

        Assert.Equal("primary-regions", regionSet.RegionSetId);
        Assert.Equal("flash", regionSet.AddressSpaceId);
        Assert.Equal(["root", "child", "later"],
            regionSet.Regions.Select(static region => region.RegionId));
        Assert.Equal(["evidence-a", "evidence-z"], regionSet.EvidenceRefs);

        IList<FirmwareRegion> regionView = Assert.IsType<IList<FirmwareRegion>>(
            regionSet.Regions,
            exactMatch: false);
        IList<string> evidenceView = Assert.IsType<IList<string>>(
            regionSet.EvidenceRefs,
            exactMatch: false);
        Assert.True(regionView.IsReadOnly);
        Assert.True(evidenceView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => regionView[0] = Region("changed", 16, 4));
        _ = Assert.Throws<NotSupportedException>(() => evidenceView[0] = "changed");
    }

    /// <summary>Verifies duplicate physical region ids fail before map construction.</summary>
    [Fact]
    public void ConstructorRejectsDuplicateRegionIds()
    {
        FirmwareRegion[] regions =
        [
            Region("duplicate", 0, 4),
            Region("duplicate", 4, 4),
        ];

        _ = Assert.Throws<ArgumentException>(() => Create(regions: regions));
    }

    /// <summary>Verifies identity, region, and evidence boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(regionSetId: " "));
        _ = Assert.Throws<ArgumentException>(() => Create(addressSpaceId: " "));
        _ = Assert.Throws<ArgumentException>(() => Create(regions: []));
        _ = Assert.Throws<ArgumentException>(() => Create(regions: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: []));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: ["evidence", "evidence"]));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: [" "]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareRegionSet(
            "primary-regions",
            "flash",
            [Region("root", 0, 4)],
            null!));
    }

    private static FirmwareRegionSet Create(
        string regionSetId = "primary-regions",
        string addressSpaceId = "flash",
        IEnumerable<FirmwareRegion>? regions = null,
        IEnumerable<string>? evidenceRefs = null)
    {
        return new FirmwareRegionSet(
            regionSetId,
            addressSpaceId,
            regions ?? [Region("root", 0, 4)],
            evidenceRefs ?? ["evidence"]);
    }

    private static FirmwareRegion Region(
        string regionId,
        long start,
        long length,
        FirmwareRegionKind kind = FirmwareRegionKind.Data,
        string? parentRegionId = null)
    {
        return new FirmwareRegion(
            regionId,
            parentRegionId,
            FirmwareRegionOwner.System,
            kind,
            new ByteRange(start, length),
            FirmwareWriteConstraint.Forbidden);
    }
}
