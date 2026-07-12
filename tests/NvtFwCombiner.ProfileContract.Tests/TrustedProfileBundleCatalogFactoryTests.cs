using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests atomic normalization and exact family binding for trusted V2 bundle catalog sources.</summary>
public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    private const string FirmwareFamilySchemaId =
        "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";

    private const string CompositionProfileSchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    /// <summary>Verifies every trusted hash survives and the profile retains its exact normalized family instance.</summary>
    [Fact]
    public void CreatePreservesTrustedHashesAndBindsTheExactNormalizedFamily()
    {
        JsonElement family = TrustedV2BundleTestDocuments.Family();
        string familyHash = Hash(TrustedV2BundleTestDocuments.FamilyJson());
        JsonElement profile = TrustedV2BundleTestDocuments.Profile(familyHash);
        string profileHash = Hash(TrustedV2BundleTestDocuments.ProfileJson(familyHash));

        TrustedProfileBundleCatalog catalog = TrustedProfileBundleCatalogFactory.Create(Source(
            [Family("family-entry", familyHash, family)],
            [Profile("profile-entry", profileHash, profile)]));

        Assert.Equal("bundle", catalog.BundleIdentity.BundleId);
        Assert.Equal(BundleHash, catalog.BundleIdentity.ContentHash);
        Assert.Equal(ManifestHash, catalog.ManifestSha256);
        TrustedFirmwareFamilyCatalogEntry familyEntry = Assert.Single(catalog.Families);
        Assert.Equal(familyHash, familyEntry.Identity.ContentHash);
        Assert.Equal(familyHash, familyEntry.Family.FamilyContentHash);
        TrustedCompositionProfileCatalogEntry profileEntry = Assert.Single(catalog.Profiles);
        Assert.Equal(profileHash, profileEntry.Identity.ContentHash);
        Assert.Same(familyEntry, profileEntry.Family);
        Assert.Equal("map", Assert.Single(profileEntry.Profile.MapBinding.MapIds));
        Assert.DoesNotContain(
            typeof(JsonElement),
            catalog.GetType().GetProperties().Select(static property => property.PropertyType));
    }

    /// <summary>Verifies duplicate family id/version declarations fail atomically without declaration-order selection.</summary>
    [Fact]
    public void CreateRejectsDuplicateFamilyIdAndVersionBeforeReturningAnyCatalog()
    {
        JsonElement first = TrustedV2BundleTestDocuments.Family();
        string firstHash = Hash(TrustedV2BundleTestDocuments.FamilyJson());
        JsonElement second = TrustedV2BundleTestDocuments.Family(mapId: "other-map");
        string secondHash = Hash(TrustedV2BundleTestDocuments.FamilyJson(mapId: "other-map"));

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            TrustedProfileBundleCatalogFactory.Create(Source(
                [Family("z-family", firstHash, first), Family("a-family", secondHash, second)],
                [])));

        Assert.Equal("profile-bundle.catalog.family-identity-duplicate", exception.Code);
        Assert.Equal("a-family", exception.EntryId);
    }

    /// <summary>Verifies duplicate profile id/version declarations fail after exact family binding.</summary>
    [Fact]
    public void CreateRejectsDuplicateProfileIdAndVersion()
    {
        JsonElement family = TrustedV2BundleTestDocuments.Family();
        string familyHash = Hash(TrustedV2BundleTestDocuments.FamilyJson());
        JsonElement first = TrustedV2BundleTestDocuments.Profile(familyHash);
        JsonElement second = TrustedV2BundleTestDocuments.Profile(familyHash);

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            TrustedProfileBundleCatalogFactory.Create(Source(
                [Family("family-entry", familyHash, family)],
                [
                    Profile("z-profile", Hash(TrustedV2BundleTestDocuments.ProfileJson(familyHash)), first),
                    Profile("a-profile", Hash(TrustedV2BundleTestDocuments.ProfileJson(familyHash)), second),
                ])));

        Assert.Equal("profile-bundle.catalog.profile-identity-duplicate", exception.Code);
        Assert.Equal("a-profile", exception.EntryId);
    }

    /// <summary>Verifies a profile cannot bind a family merely by id and version when its entry hash differs.</summary>
    [Fact]
    public void CreateRejectsProfileBoundToTheWrongFamilyContentHash()
    {
        JsonElement family = TrustedV2BundleTestDocuments.Family();
        string familyHash = Hash(TrustedV2BundleTestDocuments.FamilyJson());
        string wrongHash = new('a', 64);
        JsonElement profile = TrustedV2BundleTestDocuments.Profile(wrongHash);

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            TrustedProfileBundleCatalogFactory.Create(Source(
                [Family("family-entry", familyHash, family)],
                [Profile("profile-entry", Hash(TrustedV2BundleTestDocuments.ProfileJson(wrongHash)), profile)])));

        Assert.Equal("profile-bundle.catalog.profile-family-missing", exception.Code);
        Assert.Equal("profile-entry", exception.EntryId);
    }

    /// <summary>Verifies every declared profile map must belong to its exact trusted family.</summary>
    [Fact]
    public void CreateRejectsProfileMapNotOwnedByItsExactFamily()
    {
        JsonElement family = TrustedV2BundleTestDocuments.Family();
        string familyHash = Hash(TrustedV2BundleTestDocuments.FamilyJson());
        JsonElement profile = TrustedV2BundleTestDocuments.Profile(familyHash, mapId: "missing-map");

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            TrustedProfileBundleCatalogFactory.Create(Source(
                [Family("family-entry", familyHash, family)],
                [Profile("profile-entry", Hash(TrustedV2BundleTestDocuments.ProfileJson(familyHash, mapId: "missing-map")), profile)])));

        Assert.Equal("profile-bundle.catalog.profile-map-missing", exception.Code);
        Assert.Equal("profile-entry", exception.EntryId);
    }

    private static TrustedProfileBundleCatalogSource Source(
        IEnumerable<TrustedFirmwareFamilyJsonSource> families,
        IEnumerable<TrustedCompositionProfileJsonSource> profiles,
        string bundleContentHash = BundleHash)
    {
        return new TrustedProfileBundleCatalogSource(
            ManifestHash,
            "bundle",
            "1.0.0",
            bundleContentHash,
            "release-binding",
            families,
            profiles);
    }

    private static TrustedFirmwareFamilyJsonSource Family(string entryId, string contentHash, JsonElement document)
    {
        return new TrustedFirmwareFamilyJsonSource(
            new TrustedProfileBundleCatalogEntryIdentity(
                entryId,
                $"families/{entryId}.json",
                FirmwareFamilySchemaId,
                contentHash),
            document);
    }

    private static TrustedCompositionProfileJsonSource Profile(string entryId, string contentHash, JsonElement document)
    {
        return new TrustedCompositionProfileJsonSource(
            new TrustedProfileBundleCatalogEntryIdentity(
                entryId,
                $"profiles/{entryId}.json",
                CompositionProfileSchemaId,
                contentHash),
            document);
    }

    private static string Hash(string json)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private const string ManifestHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string BundleHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
}
