using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>A neutral provider supplies unchanged shared facts without a retired runtime bundle.</summary>
public sealed class SharedDpcmiMetadataProviderTests
{
    private const string Provider = "nt51919-nt51929-nt51932-shared-facts";
    private const string FamilyPath = "families/nt51929-nt51932.json";
    private const string FamilyHash = "d2499758dd19908422f857e5b7a68c24c47ac57961418da82d10dec2f039f3e8";
    private const string ProviderHash = "48c96d29b93cf7d78efc5266d6ead73e171f0012ddc5f7807242323327a5373f";

    /// <summary>Production exact-identity resolution comes from the sole neutral metadata-only provider.</summary>
    [Fact]
    public void ExactDpcmiReferenceResolvesFromUniqueMetadataOnlyProvider()
    {
        ProfileBundlePackageTrustIndex index = BuiltInV2BundleRegistry.TrustIndex;
        Assert.Equal("1.1.10.4", index.TrustIndexVersion);
        ProfileBundlePackageTrustEntry provider = Assert.Single(index.Bundles, bundle =>
            bundle.MetadataProviderFamilies.Any(family => family.FamilyId == "nt51929-nt51932" && family.FamilyVersion == "1.3.1"));
        Assert.Equal(Provider, provider.BundleDirectory);
        Assert.Equal("1.1.10-full-image-metadata.1", provider.BundleVersion);
        Assert.Equal(ProviderHash, provider.ContentHash);
        Assert.Empty(provider.RuntimeRegistrations);
        Assert.DoesNotContain(index.Bundles, static bundle => bundle.BundleDirectory == "nt51929-dp-replace");
        var reference = new FirmwareMetadataStructureDefinitionReferenceDocument(
            "nt51929-nt51932", "1.3.1", FamilyHash, DpcmiMetadataContract.StructureId);
        Assert.True(BuiltInCanonicalMetadataDefinitionResolver.Instance.TryResolve(reference, out FirmwareMetadataStructureDefinition? definition));
        Assert.NotNull(definition);
        Assert.False(BuiltInCanonicalMetadataDefinitionResolver.Instance.TryResolve(
            reference with { FamilyContentHash = new string('0', 64) }, out FirmwareMetadataStructureDefinition? rejected));
        Assert.Null(rejected);

        TrustedProfileBundleCatalog deployed = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(Provider, ProviderHash);
        Assert.Empty(deployed.Profiles);
        Assert.Equal("nt51929-nt51932", Assert.Single(deployed.Families).Family.FamilyId);
    }

    /// <summary>Source and deployed catalogs consume identical family bytes and the existing manifest hash algorithm.</summary>
    [Fact]
    public void SourceAndDeployedProvidersPreserveCompleteFamilyBytesWithoutDpRuntime()
    {
        string source = RepositoryPaths.FromRepositoryRoot("profiles", "built-in", Provider);
        byte[] family = File.ReadAllBytes(Path.Combine(source, FamilyPath));
        Assert.Equal(FamilyHash, Convert.ToHexString(SHA256.HashData(family)).ToLowerInvariant());
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(source, "profile-bundle.json")));
        ProfileBundleEntryDocument[] entries = [.. manifest.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => new ProfileBundleEntryDocument(entry.GetProperty("entryId").GetString()!,
                entry.GetProperty("kind").GetString()!, entry.GetProperty("path").GetString()!,
                entry.GetProperty("schemaId").GetString()!, entry.GetProperty("contentHash").GetString()!))];
        Assert.Equal(ProviderHash, ProfileBundleEntryArrayHasher.CalculateContentHash(entries));
        _ = Assert.Single(entries, entry => entry.Kind == "schema");
        _ = Assert.Single(entries, entry => entry.Kind == "firmware-family");
        Assert.DoesNotContain(entries, entry => entry.Kind == "composition-profile");
        using var providerWorkspace = TempWorkspace.Create("shared-dpcmi-provider");
        TrustedProfileBundleCatalog sourceCatalog = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(
            providerWorkspace, Provider, ProviderHash);
        Assert.Empty(sourceCatalog.Profiles);
        Assert.Equal("1.3.1", Assert.Single(sourceCatalog.Families).Family.FamilyVersion);

        AssertClosedInventory(providerWorkspace.Root);
        foreach (string bundle in new[] { Provider })
        {
            string deployedRoot = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", bundle);
            var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
            string materializedRoot = RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Bootstrap", "obj",
                testOutput.Parent!.Name, testOutput.Name, "materialized-profiles", "built-in", bundle);
            Assert.Equal(family, File.ReadAllBytes(Path.Combine(deployedRoot, FamilyPath)));
            AssertClosedInventory(deployedRoot);
            AssertClosedInventory(materializedRoot);
            Assert.Equal(bundle != Provider, File.Exists(Path.Combine(deployedRoot, "schemas", "composition-profile-v2.schema.json")));
        }
        Assert.False(File.Exists(RepositoryPaths.FromRepositoryRoot("profiles", "built-in", "nt51929-dp-replace", FamilyPath)));
    }

    private static void AssertClosedInventory(string root)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "profile-bundle.json")));
        string[] expected = ["profile-bundle.json", .. manifest.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("path").GetString()!)];
        string[] actual = [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))];
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }
}
