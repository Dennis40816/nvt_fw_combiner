using System.Text.Json.Nodes;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Tests.FlashMaps;

/// <summary>Tests version-keyed loading with made-up ICs and synthetic maps.</summary>
public sealed class BuiltInTpFlashMapCatalogVersionLoaderTests
{
    /// <summary>Distinct effective versions allow multiple map entries for the same IC.</summary>
    [Fact]
    public void DifferentEffectiveVersionsForOneIcLoad()
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "1.0.0"),
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"));

        Assert.Equal(2, profiles.Count);
        Assert.All(profiles, profile => Assert.Equal("TEST-IC-A", profile.IcId));
        Assert.Equal([new LegacyCombinerCommonFwVersion(1, 0, 0), new LegacyCombinerCommonFwVersion(2, 0, 0)],
            profiles.Select(profile => profile.EffectiveCommonFwVersion));
    }

    /// <summary>Omitted versions retain the legacy 1.0.0 effective boundary.</summary>
    [Fact]
    public void MissingEffectiveVersionDefaultsToMinimum()
    {
        TpFlashMapProfile profile = Assert.Single(TpFlashMapCatalogTestData.Load(TpFlashMapCatalogTestData.Profile()));

        Assert.Equal(LegacyCombinerCommonFwVersion.MinimumSupported, profile.EffectiveCommonFwVersion);
    }

    /// <summary>Existing constructor calls need not supply the new optional version.</summary>
    [Fact]
    public void ConstructorWithoutEffectiveVersionDefaultsToMinimum()
    {
        TpFlashMapProfile source = Assert.Single(TpFlashMapCatalogTestData.Load(TpFlashMapCatalogTestData.Profile()));
        var profile = new TpFlashMapProfile(source.IcId, source.OverviewSource, source.FirmwareConfigPrimaryStart,
            source.TpPrefixLength, source.FullFlashCapacities, source.BaseShapeEvidence, source.Regions, source.Evidence);

        Assert.Equal(LegacyCombinerCommonFwVersion.MinimumSupported, profile.EffectiveCommonFwVersion);
    }

    /// <summary>Version strings must use exactly three unsigned byte components.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2.0")]
    [InlineData("2.0.0.0")]
    [InlineData("256.0.0")]
    [InlineData("0.256.0")]
    [InlineData("0.0.256")]
    [InlineData("-1.0.0")]
    [InlineData("2.a.0")]
    public void InvalidEffectiveVersionThrowsInvalidData(string version)
    {
        _ = Assert.Throws<InvalidDataException>(() =>
            TpFlashMapCatalogTestData.Load(TpFlashMapCatalogTestData.Profile(version: version)));
    }

    /// <summary>Non-string version fields are malformed schema 1.0 data.</summary>
    [Theory]
    [InlineData("2")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("null")]
    public void NonStringEffectiveVersionThrowsInvalidData(string jsonValue)
    {
        JsonObject profile = TpFlashMapCatalogTestData.Profile();
        profile["effectiveCommonFwVersion"] = JsonNode.Parse(jsonValue);

        _ = Assert.Throws<InvalidDataException>(() => TpFlashMapCatalogTestData.Load(profile));
    }

    /// <summary>Parsed version identity, including the missing-field default, rejects duplicate IC keys.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "1.0.0")]
    [InlineData("2.0.0", "2.0.0")]
    [InlineData("2.0.0", " 2.0.0 ")]
    public void DuplicateEffectiveVersionForOneIcThrowsInvalidData(string? firstVersion, string? secondVersion)
    {
        _ = Assert.Throws<InvalidDataException>(() => TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: firstVersion),
            TpFlashMapCatalogTestData.Profile(version: secondVersion)));
    }

    /// <summary>Every IC needs an entry at the minimum supported version, so no readable version falls outside.</summary>
    [Fact]
    public void IcWithoutTheMinimumVersionEntryThrowsInvalidData()
    {
        _ = Assert.Throws<InvalidDataException>(() => TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.0.0")));
        _ = Assert.Throws<InvalidDataException>(() => TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile(version: "2.0.0"),
            TpFlashMapCatalogTestData.Profile(version: "2.5.0")));
        _ = Assert.Throws<InvalidDataException>(() => TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "1.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-B", "2.0.0")));
    }

    /// <summary>Different ICs can share boundaries, and each IC's entries are ordered numerically.</summary>
    [Fact]
    public void EntriesAreOrderedByEffectiveVersionWithinEachIc()
    {
        IReadOnlyList<TpFlashMapProfile> profiles = TpFlashMapCatalogTestData.Load(
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "2.5.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-B", "2.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "1.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-B", "1.0.0"),
            TpFlashMapCatalogTestData.Profile("TEST-IC-A", "2.0.0"));

        Assert.Equal([new LegacyCombinerCommonFwVersion(1, 0, 0), new LegacyCombinerCommonFwVersion(2, 0, 0),
                new LegacyCombinerCommonFwVersion(2, 5, 0)],
            profiles.Where(profile => profile.IcId == "TEST-IC-A").Select(profile => profile.EffectiveCommonFwVersion));
        Assert.Equal([new LegacyCombinerCommonFwVersion(1, 0, 0), new LegacyCombinerCommonFwVersion(2, 0, 0)],
            profiles.Where(profile => profile.IcId == "TEST-IC-B").Select(profile => profile.EffectiveCommonFwVersion));
    }
}
