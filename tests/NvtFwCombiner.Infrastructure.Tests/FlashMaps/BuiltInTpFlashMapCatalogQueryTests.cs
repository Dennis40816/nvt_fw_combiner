using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Tests.FlashMaps;

/// <summary>Tests versionless query safety and unchanged shipped-catalog behavior.</summary>
public sealed class BuiltInTpFlashMapCatalogQueryTests
{
    /// <summary>An exact synthetic plan still cannot choose a map without its Common FW version.</summary>
    [Fact]
    public void PlanAdjustedQueriesRejectMultipleEntries()
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "1.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "2.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-B"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);
        var command = new LegacyCombinerPostbuildCommand(
            "test-command", LegacyCombinerCommandFamily.CrcOnlyMode, "synthetic-mode", "synthetic-crc", []);
        var postbuildProfile = new LegacyCombinerPostbuildProfile(
            "synthetic-processor", "TEST-IC-A", "synthetic-tool", "synthetic-image",
            [command], [command], "synthetic");
        LegacyCombinerPostbuildCommandPlan plan = postbuildProfile.ResolvePlan(icNumberSelection: null);

        Assert.Empty(catalog.GetRegionsForPlan("TEST-IC-A", plan));
        Assert.Empty(catalog.GetRegionsForPlan("TEST-IC-MISSING", plan));
        Assert.Equal(profiles[2].Regions, catalog.GetRegionsForPlan("TEST-IC-B", plan));
        _ = Assert.Throws<ArgumentNullException>(() => catalog.GetRegionsForPlan("TEST-IC-A", null!));
    }

    /// <summary>Versionless lookup and region queries never select one of several maps, while ids remain unique.</summary>
    [Fact]
    public void VersionlessQueriesRejectMultipleEntriesAndListEachIcOnce()
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile("TEST-IC-B"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "2.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "1.0.0"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        Assert.Collection(catalog.IcIds,
            icId => Assert.Equal("TEST-IC-A", icId),
            icId => Assert.Equal("TEST-IC-B", icId));
        Assert.False(catalog.TryFind("TEST-IC-A", out TpFlashMapProfile? multiple));
        Assert.Null(multiple);
        Assert.Empty(catalog.GetRegions("TEST-IC-A", null, null));
        Assert.Empty(catalog.GetRegions("TEST-IC-A", null, null, TpFlashMapRegionKind.Other));
        Assert.False(catalog.TryFind("TEST-IC-MISSING", out TpFlashMapProfile? missing));
        Assert.Null(missing);
        Assert.Empty(catalog.GetRegions("TEST-IC-MISSING", null, null));
        Assert.True(catalog.TryFind("TEST-IC-B", out TpFlashMapProfile? sole));
        Assert.Same(profiles[0], sole);
        Assert.Equal(sole.Regions, catalog.GetRegions("TEST-IC-B", null, null));
    }

    /// <summary>The shipped bytes keep one entry per IC. Only a declared pending map changes null-version lookup.</summary>
    [Fact]
    public void ShippedCatalogRetainsPinnedHashAndSingleEntrySelection()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "profiles/built-in/ctrlram-postbuild-v2/flash-map.json".Replace('/', Path.DirectorySeparatorChar)));
        BuiltInTpFlashMapCatalog.LoadedCatalog loaded = BuiltInTpFlashMapCatalog.LoadCatalog(bytes,
            "3531245afbf71b751e095fb0aee3b2f65e67b348fbf2289303520caf75aea9c6");
        IReadOnlyList<TpFlashMapProfile> profiles = loaded.Profiles;

        Assert.Equal(BuiltInTpFlashMapCatalog.IcIds, profiles.Select(profile => profile.IcId).Order(StringComparer.Ordinal));
        Assert.All(profiles.GroupBy(profile => profile.IcId), group => Assert.Single(group));
        Assert.Equal(["NT51925"], loaded.PendingMaps.Select(map => map.IcId));
        foreach (string icId in BuiltInTpFlashMapCatalog.IcIds)
        {
            if (loaded.PendingMaps.Any(map => map.IcId == icId))
            {
                Assert.False(BuiltInTpFlashMapCatalog.TryFind(icId, out _));
                Assert.False(BuiltInTpFlashMapCatalog.TrySelect(icId, null, out _, out string? pendingIssue));
                Assert.False(string.IsNullOrWhiteSpace(pendingIssue));
                continue;
            }

            Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? found));
            Assert.Equal(LegacyCombinerCommonFwVersion.MinimumSupported, found!.EffectiveCommonFwVersion);
            Assert.True(BuiltInTpFlashMapCatalog.TrySelect(icId, null, out TpFlashMapProfile? selected, out string? issue));
            Assert.Null(issue);
            Assert.Same(found, selected);
        }
    }
}
