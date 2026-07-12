using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests the Bootstrap-only bridge from trusted bundle JSON to the normalized V2 catalog.</summary>
public sealed class TrustedProfileBundleCatalogProjectionTests
{
    private const string FirmwareFamilySchemaId =
        "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";

    private const string CompositionProfileSchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    /// <summary>Verifies the bridge preserves trusted entry identity while Profiles owns semantic normalization.</summary>
    [Fact]
    public void CreateProjectsOneTrustedBundleIntoAnExactlyBoundNormalizedCatalog()
    {
        using var workspace = TempWorkspace.Create("nfc-bootstrap-trusted-catalog");
        byte[] familySchema = ReadSchema("firmware-family-v1.schema.json");
        byte[] profileSchema = ReadSchema("composition-profile-v2.schema.json");
        byte[] family = Encoding.UTF8.GetBytes(TrustedV2BundleTestDocuments.FamilyJson());
        string familyHash = Hash(family);
        byte[] profile = Encoding.UTF8.GetBytes(TrustedV2BundleTestDocuments.ProfileJson(familyHash));
        var entries = new List<ProfileBundleEntryDocument>
        {
            new("family-schema", "schema", "schemas/family.schema.json", FirmwareFamilySchemaId, Hash(familySchema)),
            new("profile-schema", "schema", "schemas/profile.schema.json", CompositionProfileSchemaId, Hash(profileSchema)),
            new("family-entry", "firmware-family", "families/family.json", FirmwareFamilySchemaId, familyHash),
            new("profile-entry", "composition-profile", "profiles/profile.json", CompositionProfileSchemaId, Hash(profile)),
        };
        string bundleContentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        _ = workspace.Write("schemas/family.schema.json", familySchema);
        _ = workspace.Write("schemas/profile.schema.json", profileSchema);
        _ = workspace.Write("families/family.json", family);
        _ = workspace.Write("profiles/profile.json", profile);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes(Manifest(entries, bundleContentHash)));

        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(bundleContentHash, "release-manifest"),
            new ProfileBundleLoadLimits(
                16384,
                32,
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
        TrustedProfileBundleCatalog catalog = TrustedProfileBundleCatalogProjection.Create(
            bundle.CreateDocumentProjection());

        Assert.Equal("bundle", catalog.BundleIdentity.BundleId);
        Assert.Equal(bundleContentHash, catalog.BundleIdentity.ContentHash);
        TrustedFirmwareFamilyCatalogEntry familyEntry = Assert.Single(catalog.Families);
        Assert.Equal("family-entry", familyEntry.Identity.EntryId);
        Assert.Equal(familyHash, familyEntry.Family.FamilyContentHash);
        TrustedCompositionProfileCatalogEntry profileEntry = Assert.Single(catalog.Profiles);
        Assert.Equal("profile-entry", profileEntry.Identity.EntryId);
        Assert.Same(familyEntry, profileEntry.Family);
        Assert.Equal("map", Assert.Single(profileEntry.Profile.MapBinding.MapIds));

        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);
        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            new V2CompositionPreparationRequest(
                selection,
                new FirmwareMapResolutionInputs("NT00001", "standard", 16, requestedTopology: null, [])));

        Assert.True(preparation.IsAdmitted);
        Assert.Equal(V2CompositionPreparationStatus.Admitted, preparation.Status);
        Assert.Equal(FirmwareMapResolutionStatus.Unique, preparation.MapResolution?.Status);
        Assert.Same(profileEntry.Profile, preparation.Admission?.Profile);
        Assert.Equal("map", preparation.Admission?.ResolvedMap.ImageMap.MapId);
    }

    private static byte[] ReadSchema(string fileName)
    {
        return Encoding.UTF8.GetBytes(File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName)));
    }

    private static string Manifest(IEnumerable<ProfileBundleEntryDocument> entries, string contentHash)
    {
        string entryJson = string.Join(',', entries.Select(static entry => $$"""
            {
              "entryId": "{{entry.EntryId}}",
              "kind": "{{entry.Kind}}",
              "path": "{{entry.Path}}",
              "schemaId": "{{entry.SchemaId}}",
              "contentHash": "{{entry.ContentHash}}"
            }
            """));
        return $$"""
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "{{contentHash}}",
              "trustAnchorBindingId": "release-manifest",
              "entries": [{{entryJson}}]
            }
            """;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
