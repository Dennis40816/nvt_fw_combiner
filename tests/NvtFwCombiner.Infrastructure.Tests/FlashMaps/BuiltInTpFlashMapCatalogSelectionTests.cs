using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Tests.FlashMaps;

/// <summary>Tests the shared interval selection rule through synthetic TP catalog entries.</summary>
public sealed class BuiltInTpFlashMapCatalogSelectionTests
{
    /// <summary>Two maps cover exact lower bounds and future versions without guessing on unreadable metadata.</summary>
    [Theory]
    [InlineData("0.9.9", null)]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.9.9", "1.0.0")]
    [InlineData("2.0.0", "2.0.0")]
    [InlineData("2.0.1", "2.0.0")]
    [InlineData("255.255.255", "2.0.0")]
    [InlineData("invalid", null)]
    [InlineData("2.0", null)]
    [InlineData("256.0.0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TwoEntrySelectionRespectsVersionBoundaries(string? version, string? expectedVersion)
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"),
            TpFlashMapCatalogTestData.Profile(version: "1.0.0"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        AssertSelection(catalog, profiles, version, expectedVersion);
    }

    /// <summary>Another data-only boundary creates a third interval without new catalog branches.</summary>
    [Theory]
    [InlineData("2.4.99", "2.0.0")]
    [InlineData("2.5.0", "2.5.0")]
    [InlineData("2.5.1", "2.5.0")]
    public void ThirdEntrySelectionUsesTheLatestApplicableBoundary(string version, string expectedVersion)
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.5.0"),
            TpFlashMapCatalogTestData.Profile(version: "1.0.0"),
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        AssertSelection(catalog, profiles, version, expectedVersion);
    }

    /// <summary>A sole map accepts unreadable metadata and any readable version, except below minimum.</summary>
    [Theory]
    [InlineData("invalid", "1.0.0")]
    [InlineData(null, "1.0.0")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("255.255.255", "1.0.0")]
    [InlineData("0.9.9", null)]
    public void SoleEntryUsesTheSameFallbackAsPostbuildSelection(string? version, string? expectedVersion)
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile());
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        AssertSelection(catalog, profiles, version, expectedVersion);
    }

    /// <summary>Missing ICs and empty catalogs always fail with a diagnostic, even with unreadable versions.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0.9.9")]
    [InlineData("2.0.0")]
    public void MissingIcIsRejected(string? version)
    {
        foreach (IReadOnlyList<TpFlashMapProfile> profiles in new[]
                 {
                     TpFlashMapCatalogTestData.Load(),
                     TpFlashMapCatalogTestData.Load(TpFlashMapCatalogTestData.Profile()),
                 })
        {
            var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);
            Assert.False(catalog.TrySelect("TEST-IC-MISSING", version, out TpFlashMapProfile? profile, out string? issue));
            Assert.Null(profile);
            Assert.False(string.IsNullOrWhiteSpace(issue));
        }
    }

    private static void AssertSelection(
        BuiltInTpFlashMapCatalog.Catalog catalog,
        IReadOnlyList<TpFlashMapProfile> profiles,
        string? version,
        string? expectedVersion)
    {
        Assert.Equal(expectedVersion is not null,
            catalog.TrySelect("TEST-IC-A", version, out TpFlashMapProfile? profile, out string? issue));
        if (expectedVersion is null)
        {
            Assert.Null(profile);
            Assert.False(string.IsNullOrWhiteSpace(issue));
        }
        else
        {
            Assert.Null(issue);
            Assert.Same(Assert.Single(profiles, candidate =>
                candidate.EffectiveCommonFwVersion.ToString() == expectedVersion), profile);
        }
    }

    /// <summary>A pending map refuses every version from its boundary on and keeps lower versions on the old entry.</summary>
    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.9.9", "1.0.0")]
    [InlineData("2.0.0", null)]
    [InlineData("2.4.99", null)]
    [InlineData("255.255.255", null)]
    [InlineData("0.9.9", null)]
    public void PendingMapRefusesFromItsBoundary(string version, string? expectedVersion)
    {
        BuiltInTpFlashMapCatalog.Catalog catalog = CreatePendingCatalog(out IReadOnlyList<TpFlashMapProfile> profiles);

        AssertSelection(catalog, profiles, version, expectedVersion);
        if (expectedVersion is null && version != "0.9.9")
        {
            Assert.False(catalog.TrySelect("TEST-IC-A", version, out _, out string? issue));
            Assert.Equal("the 2.0.0 map is missing", issue);
        }
    }

    /// <summary>A pending map makes the sole entry version-dependent, so an unreadable version is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2.0")]
    public void PendingMapRefusesUnreadableVersions(string? version)
    {
        BuiltInTpFlashMapCatalog.Catalog catalog = CreatePendingCatalog(out IReadOnlyList<TpFlashMapProfile> profiles);

        AssertSelection(catalog, profiles, version, null);
    }

    /// <summary>A pending map above several entries leaves the interval rule for lower versions unchanged.</summary>
    [Theory]
    [InlineData("1.9.9", "1.0.0")]
    [InlineData("2.0.0", "2.0.0")]
    [InlineData("2.9.9", "2.0.0")]
    [InlineData("3.0.0", null)]
    public void PendingMapAboveSeveralEntriesOnlyBlocksItsOwnRange(string version, string? expectedVersion)
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"),
            TpFlashMapCatalogTestData.Profile(version: "1.0.0"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles,
            [new BuiltInTpFlashMapCatalog.PendingTpFlashMap("TEST-IC-A", new(3, 0, 0), "the 3.0.0 map is missing")]);

        AssertSelection(catalog, profiles, version, expectedVersion);
    }

    /// <summary>The by-IC lookups never serve an IC that has a pending map, because they cannot see a version.</summary>
    [Fact]
    public void ByIcLookupsReturnNothingForAPendingIc()
    {
        BuiltInTpFlashMapCatalog.Catalog catalog = CreatePendingCatalog(out _);

        Assert.False(catalog.TryFind("TEST-IC-A", out _));
        Assert.Empty(catalog.GetRegions("TEST-IC-A", null, null));
    }

    /// <summary>
    /// An IC with several entries has regions for a chosen entry even though the by-IC lookup returns nothing.
    /// </summary>
    [Fact]
    public void ChosenEntryOfAMultiEntryIcHasItsOwnRegions()
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "1.0.0", regionId: "region-v1"),
            TpFlashMapCatalogTestData.Profile(version: "2.0.0", regionId: "region-v2"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        Assert.Empty(catalog.GetRegions("TEST-IC-A", null, null));
        Assert.True(catalog.TrySelect("TEST-IC-A", "1.9.9", out TpFlashMapProfile? v1, out _));
        Assert.True(catalog.TrySelect("TEST-IC-A", "2.0.0", out TpFlashMapProfile? v2, out _));
        Assert.Equal(["region-v1"], BuiltInTpFlashMapCatalog.GetRegionsOf(v1, null, null).Select(region => region.RegionId));
        Assert.Equal(["region-v2"], BuiltInTpFlashMapCatalog.GetRegionsOf(v2, null, null).Select(region => region.RegionId));
        Assert.Empty(BuiltInTpFlashMapCatalog.GetRegionsOf(null, null, null));
    }

    private static BuiltInTpFlashMapCatalog.Catalog CreatePendingCatalog(out IReadOnlyList<TpFlashMapProfile> profiles)
    {
        profiles = TpFlashMapCatalogTestData.Load(TpFlashMapCatalogTestData.Profile());
        return new BuiltInTpFlashMapCatalog.Catalog(profiles,
            [new BuiltInTpFlashMapCatalog.PendingTpFlashMap("TEST-IC-A", new(2, 0, 0), "the 2.0.0 map is missing")]);
    }
}
