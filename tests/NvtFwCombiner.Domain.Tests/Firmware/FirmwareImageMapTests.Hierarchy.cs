using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

public sealed partial class FirmwareImageMapTests
{
    /// <summary>Verifies exact partitions at non-zero parent and grandparent boundaries.</summary>
    [Fact]
    public void ConstructorAcceptsNonZeroThreeLevelHierarchy()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("prefix", 0, 8),
            Region("grandparent", 8, 24, kind: FirmwareRegionKind.Image),
            Region("parent", 8, 12, kind: FirmwareRegionKind.Image, parentRegionId: "grandparent"),
            Region("grandparent-right", 20, 12, parentRegionId: "grandparent"),
            Region("leaf-left", 8, 4, parentRegionId: "parent"),
            Region("leaf-right", 12, 8, parentRegionId: "parent"),
        ]);

        FirmwareImageMap map = Create(capacityBytes: 32, regionSets: [regionSet]);

        Assert.Equal(6, map.Regions.Count);
    }

    /// <summary>Verifies a non-zero grandchild partition cannot contain a leading gap.</summary>
    [Fact]
    public void ConstructorRejectsGrandchildGap()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 32, kind: FirmwareRegionKind.Image),
            Region("prefix", 0, 8, parentRegionId: "root"),
            Region("parent", 8, 24, kind: FirmwareRegionKind.Image, parentRegionId: "root"),
            Region("only-child", 9, 23, parentRegionId: "parent"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(capacityBytes: 32, regionSets: [regionSet]));
    }

    /// <summary>Verifies overlapping grandchildren are rejected.</summary>
    [Fact]
    public void ConstructorRejectsGrandchildOverlap()
    {
        FirmwareRegionSet regionSet = Set("regions", [
            Region("root", 0, 32, kind: FirmwareRegionKind.Image),
            Region("prefix", 0, 8, parentRegionId: "root"),
            Region("parent", 8, 24, kind: FirmwareRegionKind.Image, parentRegionId: "root"),
            Region("first", 8, 16, parentRegionId: "parent"),
            Region("second", 20, 12, parentRegionId: "parent"),
        ]);

        _ = Assert.Throws<ArgumentException>(() => Create(capacityBytes: 32, regionSets: [regionSet]));
    }

    /// <summary>Verifies deep valid hierarchies are checked iteratively without stack recursion.</summary>
    [Fact]
    public void ConstructorAcceptsDeepIterativeHierarchy()
    {
        const int depth = 2048;
        const int capacity = 4096;
        List<FirmwareRegion> regions =
        [
            Region("level-0", 0, capacity, kind: FirmwareRegionKind.Image),
        ];
        for (int level = 1; level <= depth; level++)
        {
            string parentId = $"level-{level - 1}";
            regions.Add(Region(
                $"level-{level}",
                0,
                capacity - level,
                kind: FirmwareRegionKind.Image,
                parentRegionId: parentId));
            regions.Add(Region(
                $"tail-{level}",
                capacity - level,
                1,
                parentRegionId: parentId));
        }

        FirmwareImageMap map = Create(
            capacityBytes: capacity,
            regionSets: [Set("deep-regions", regions)]);

        Assert.Equal(1 + (2 * depth), map.Regions.Count);
    }
}
