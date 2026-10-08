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

    /// <summary>A sole map accepts unreadable metadata and earlier effective boundaries, except below minimum.</summary>
    [Theory]
    [InlineData("invalid", "2.0.0")]
    [InlineData(null, "2.0.0")]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("255.255.255", "2.0.0")]
    [InlineData("0.9.9", null)]
    public void SoleEntryUsesTheSameFallbackAsPostbuildSelection(string? version, string? expectedVersion)
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        AssertSelection(catalog, profiles, version, expectedVersion);
    }

    /// <summary>Readable versions with no eligible map fail with a diagnostic rather than throwing.</summary>
    [Fact]
    public void VersionBeforeEveryEffectiveBoundaryIsRejected()
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"),
            TpFlashMapCatalogTestData.Profile(version: "2.5.0"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(profiles);

        AssertSelection(catalog, profiles, "1.9.9", null);
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
}
