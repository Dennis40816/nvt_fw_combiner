using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>A neutral provider supplies unchanged shared facts while retained DP runtime remains independent.</summary>
public sealed class SharedDpcmiMetadataProviderTests
{
    private const string Provider = "nt51919-nt51929-nt51932-shared-facts";
    private const string FamilyPath = "families/nt51929-nt51932.json";
    private const string FamilyHash = "6cd257c38e4c9ecb4e44c14d12027e44a6d484b8176112dceccb7328d153b617";
    private const string ProviderHash = "f2fc92f624db2945789072d4dcff8c55e1bf45b239c57f7f8453359b2ce6464c";
    private const string DpHash = "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd";

    /// <summary>Production exact-identity resolution comes from the sole neutral metadata-only provider.</summary>
    [Fact]
    public void ExactDpcmiReferenceResolvesFromUniqueMetadataOnlyProvider()
    {
        ProfileBundlePackageTrustIndex index = BuiltInV2BundleRegistry.TrustIndex;
        Assert.Equal("1.1.10.1", index.TrustIndexVersion);
        ProfileBundlePackageTrustEntry provider = Assert.Single(index.Bundles, bundle =>
            bundle.MetadataProviderFamilies.Any(family => family.FamilyId == "nt51929-nt51932" && family.FamilyVersion == "1.3.0"));
        Assert.Equal(Provider, provider.BundleDirectory);
        Assert.Equal("1.1.10-shared-facts.1", provider.BundleVersion);
        Assert.Equal(ProviderHash, provider.ContentHash);
        Assert.Empty(provider.RuntimeRegistrations);
        Assert.Empty(index.Bundles.Single(bundle => bundle.BundleDirectory == "nt51929-dp-replace").MetadataProviderFamilies);
        var reference = new FirmwareMetadataStructureDefinitionReferenceDocument(
            "nt51929-nt51932", "1.3.0", FamilyHash, DpcmiMetadataContract.StructureId);
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
    public void SourceAndDeployedProvidersPreserveCompleteFamilyBytesAndRetainedDpManifest()
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
        Assert.Equal("1.3.0", Assert.Single(sourceCatalog.Families).Family.FamilyVersion);

        using var dpWorkspace = TempWorkspace.Create("shared-dpcmi-retained-dp");
        TrustedProfileBundleCatalog dpCatalog = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(
            dpWorkspace, "nt51929-dp-replace", DpHash);
        Assert.Equal(3, dpCatalog.Profiles.Count);
        Assert.Equal("0.10.1-shared-facts.1", dpCatalog.BundleIdentity.BundleVersion);
        Assert.Equal(DpHash, dpCatalog.BundleIdentity.ContentHash);
        Assert.Equal(family, File.ReadAllBytes(dpWorkspace.PathFor(FamilyPath)));
        AssertClosedInventory(providerWorkspace.Root);
        AssertClosedInventory(dpWorkspace.Root);
        foreach (string bundle in new[] { Provider, "nt51929-dp-replace" })
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

    /// <summary>All three retained DP registrations still compile against the same Perfect map and trusted runtime identity.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-dp-replace-gen-flash-alias", "0.2.0")]
    [InlineData("NT51929", "nt51929-dp-replace-gen-flash", "0.3.0")]
    [InlineData("NT51932", "nt51932-dp-replace-gen-flash", "0.2.0")]
    public void RetainedDpRegistrationCompilesUnchangedPerfectMap(string ic, string profileId, string profileVersion)
    {
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[ic];
        registration.TryCompile(0x40000, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.Equal(profileId, composition.V2Details.ProfileId);
        Assert.Equal(profileVersion, composition.V2Details.ProfileVersion);
        Assert.Equal("nt51919-nt51929-nt51932-perfect-map-256k", composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(DpHash, composition.V2Details.Provenance.Bundle.ContentHash);
        Assert.Equal("0.10.1-shared-facts.1", composition.V2Details.Provenance.Bundle.BundleVersion);
        if (ic == "NT51929")
        {
            Assert.Equal("3d937f93a0cf0714b8d13ab5480d7f65a27da04a5c78aaab7a53ba25fb8a200c", composition.CompilationFingerprint);
        }
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
