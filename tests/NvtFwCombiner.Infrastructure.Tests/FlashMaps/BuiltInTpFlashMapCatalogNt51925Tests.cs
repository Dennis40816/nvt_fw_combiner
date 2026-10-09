using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Composition;

namespace NvtFwCombiner.Infrastructure.Tests.FlashMaps;

/// <summary>Independent owner-fact assertions for the four NT51925 catalog layouts.</summary>
public sealed class BuiltInTpFlashMapCatalogNt51925Tests
{
    private static readonly (string, long, long, TpFlashMapRegionVisibility)[] Common1Single =
    [
        ("normal", 0x22780, 10240, TpFlashMapRegionVisibility.Always),
        ("mp", 0x24F80, 8192, TpFlashMapRegionVisibility.Always),
        ("vn-single", 0x26F80, 824, TpFlashMapRegionVisibility.SingleChipOnly),
        ("nf-single", 0x272B8, 3328, TpFlashMapRegionVisibility.SingleChipOnly),
        ("fw-config-backup", 0x2F000, 1920, TpFlashMapRegionVisibility.Always),
        ("customer-info", 0x3D000, 4096, TpFlashMapRegionVisibility.Always),
        ("dp", 0x3E000, 8192, TpFlashMapRegionVisibility.Always),
    ];
    private static readonly (string, long, long, TpFlashMapRegionVisibility)[] Common1Cascade =
    [
        ("normal", 0x22780, 10240, TpFlashMapRegionVisibility.Always),
        ("mp", 0x24F80, 8192, TpFlashMapRegionVisibility.Always),
        ("normal-slave", 0x26F80, 10240, TpFlashMapRegionVisibility.MultiChipOnly),
        ("mp-slave", 0x29780, 8192, TpFlashMapRegionVisibility.MultiChipOnly),
        ("vn", 0x2B780, 824, TpFlashMapRegionVisibility.MultiChipOnly),
        ("nf", 0x2BAB8, 7856, TpFlashMapRegionVisibility.MultiChipOnly),
        ("fw-config-backup", 0x2F000, 1920, TpFlashMapRegionVisibility.Always),
        ("customer-info", 0x3D000, 4096, TpFlashMapRegionVisibility.Always),
        ("dp", 0x3E000, 8192, TpFlashMapRegionVisibility.Always),
    ];
    private static readonly (string, long, long, TpFlashMapRegionVisibility)[] Common2Single =
    [
        ("normal", 0x22A00, 11264, TpFlashMapRegionVisibility.Always),
        ("mp", 0x25600, 9216, TpFlashMapRegionVisibility.Always),
        ("vn", 0x2D000, 5728, TpFlashMapRegionVisibility.Always),
        ("nf", 0x2A200, 11776, TpFlashMapRegionVisibility.Always),
        ("header-backup", 0x2E980, 256, TpFlashMapRegionVisibility.Always),
        ("fw-config-backup", 0x2F000, 2048, TpFlashMapRegionVisibility.Always),
        ("customer-info", 0x3D000, 4096, TpFlashMapRegionVisibility.Always),
        ("dp", 0x3E000, 8192, TpFlashMapRegionVisibility.Always),
    ];
    private static readonly (string, long, long, TpFlashMapRegionVisibility)[] Common2Cascade =
    [
        ("normal", 0x22A00, 11264, TpFlashMapRegionVisibility.Always),
        ("mp", 0x25600, 9216, TpFlashMapRegionVisibility.Always),
        ("diff", 0x27A00, 10240, TpFlashMapRegionVisibility.MultiChipOnly),
        ("vn", 0x2D000, 5728, TpFlashMapRegionVisibility.Always),
        ("nf", 0x2A200, 11776, TpFlashMapRegionVisibility.Always),
        ("header-backup", 0x2E980, 256, TpFlashMapRegionVisibility.Always),
        ("fw-config-backup", 0x2F000, 2048, TpFlashMapRegionVisibility.Always),
        ("customer-info", 0x3D000, 4096, TpFlashMapRegionVisibility.Always),
        ("dp", 0x3E000, 8192, TpFlashMapRegionVisibility.Always),
    ];

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.9.9")]
    public void GetRegions_Common1Single_ReturnsExactOwnerRegions(string version)
    {
        AssertRegions(version, "1", Common1Single);
    }

    [Theory]
    [InlineData("1.0.0", "2")]
    [InlineData("1.0.0", "3")]
    [InlineData("1.9.9", "2")]
    [InlineData("1.9.9", "3")]
    public void GetRegions_Common1Cascade_ReturnsExactOwnerRegions(string version, string count)
    {
        AssertRegions(version, count, Common1Cascade);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("2.9.9")]
    public void GetRegions_Common2Single_ReturnsExactOwnerRegions(string version)
    {
        AssertRegions(version, "1", Common2Single);
    }

    [Theory]
    [InlineData("2.0.0", "2")]
    [InlineData("2.0.0", "3")]
    [InlineData("2.9.9", "2")]
    [InlineData("2.9.9", "3")]
    public void GetRegions_Common2Cascade_ReturnsExactOwnerRegions(string version, string count)
    {
        AssertRegions(version, count, Common2Cascade);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.0.0")]
    public void TrySelect_Nt51925_DeclaresSeparateTpAndFullFlashShapes(string version)
    {
        Assert.True(BuiltInTpFlashMapCatalog.TrySelect("NT51925", version, out TpFlashMapProfile? map, out _));
        Assert.Equal(0x22000, map!.FirmwareConfigPrimaryStart);
        Assert.Equal(0x30000, map.TpPrefixLength);
        Assert.Equal([0x40000L], map.FullFlashCapacities);
    }

    [Fact]
    public void TrySelect_BelowMinimum_RefusesMap()
    {
        Assert.False(BuiltInTpFlashMapCatalog.TrySelect("NT51925", "0.9.9", out TpFlashMapProfile? map, out string? issue));
        Assert.Null(map);
        Assert.Contains("below the minimum", issue, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("single-chip-only", "1", true)]
    [InlineData("single-chip-only", "2", false)]
    [InlineData("single-chip-only", "3", false)]
    [InlineData("multi-chip-only", "1", false)]
    [InlineData("multi-chip-only", "2", true)]
    [InlineData("multi-chip-only", "3", true)]
    public void GetRegions_ChipVisibility_ShowsOnlyDeclaredTopology(string visibility, string count, bool visible)
    {
        var document = TpFlashMapCatalogTestData.Profile();
        document["regions"]![0]!["visibility"] = visibility;
        TpFlashMapProfile map = Assert.Single(TpFlashMapCatalogTestData.Load(document));
        Assert.Equal(visible, BuiltInTpFlashMapCatalog.GetRegionsOf(map, IcNumberSelection.FromToken(count), null).Count == 1);
    }

    [Theory]
    [InlineData("1.0.0", "1")]
    [InlineData("1.0.0", "2")]
    [InlineData("1.0.0", "3")]
    [InlineData("2.0.0", "1")]
    [InlineData("2.0.0", "2")]
    [InlineData("2.0.0", "3")]
    public void GetRegions_ProvisionalPostbuildPlan_KeepsAuthoritativeCatalogRanges(string version, string count)
    {
        Assert.True(BuiltInCommonFwSelector.TrySelect("NT51925", true, version, out BuiltInCommonFwSelection? selected, out _));
        IcNumberSelection topology = IcNumberSelection.FromToken(count);
        Assert.Equal(
            Tuples(BuiltInTpFlashMapCatalog.GetRegionsOf(selected!.TpFlashMap, topology, null)),
            Tuples(BuiltInTpFlashMapCatalog.GetRegionsOf(selected.TpFlashMap, topology, selected.PostbuildProfile)));
    }

    [Theory]
    [InlineData("1.0.0", "1", "normal,mp,vn-single,nf-single")]
    [InlineData("1.0.0", "2", "normal,mp,normal-slave,mp-slave,vn,nf")]
    [InlineData("1.0.0", "3", "normal,mp,normal-slave,mp-slave,vn,nf")]
    [InlineData("2.0.0", "1", "normal,mp,vn,nf")]
    [InlineData("2.0.0", "2", "normal,mp,diff,vn,nf")]
    [InlineData("2.0.0", "3", "normal,mp,diff,vn,nf")]
    public void CreateDisplay_ShippedProvisionalPlan_ProjectsDeclaredRegions(string version, string count, string expectedIds)
    {
        Assert.True(BuiltInCommonFwSelector.TrySelect("NT51925", true, version, out BuiltInCommonFwSelection? selected, out _));
        var display = BuiltInCtrlRamAuthoringAdapter.CreateDisplay("NT51925", count, selected!.PostbuildProfile, version, true);
        Assert.Equal(expectedIds.Split(','), display.Regions.Select(static region => region.RegionId));
    }

    [Theory]
    [InlineData("1.0.0", "1")]
    [InlineData("1.0.0", "2")]
    [InlineData("1.0.0", "3")]
    [InlineData("2.0.0", "1")]
    [InlineData("2.0.0", "2")]
    [InlineData("2.0.0", "3")]
    public void CreateDisplay_UnconfirmedProvisionalSources_ProducesNoInputSlots(string version, string count)
    {
        Assert.True(BuiltInCommonFwSelector.TrySelect("NT51925", true, version, out BuiltInCommonFwSelection? selected, out _));
        var display = BuiltInCtrlRamAuthoringAdapter.CreateDisplay("NT51925", count, selected!.PostbuildProfile, version, true);
        Assert.Empty(display.InputSlots);
    }

    private static void AssertRegions(string version, string count, (string, long, long, TpFlashMapRegionVisibility)[] expected)
    {
        Assert.True(BuiltInTpFlashMapCatalog.TrySelect("NT51925", version, out TpFlashMapProfile? map, out string? issue), issue);
        Assert.Null(issue);
        Assert.Equal(expected, Tuples(BuiltInTpFlashMapCatalog.GetRegionsOf(map, IcNumberSelection.FromToken(count), null)));
    }

    private static IEnumerable<(string, long, long, TpFlashMapRegionVisibility)> Tuples(IEnumerable<TpFlashMapRegion> regions)
    {
        return regions.Select(static region => (region.RegionId, region.Range.Start, region.Range.Length, region.Visibility));
    }
}
