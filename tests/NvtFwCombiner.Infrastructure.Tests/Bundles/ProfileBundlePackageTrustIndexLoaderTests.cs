using System.Globalization;
using System.Text;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests the package-owned exact bundle admission boundary.</summary>
public sealed class ProfileBundlePackageTrustIndexLoaderTests
{
    /// <summary>The file-size gate rejects an oversized index before allocating its snapshot.</summary>
    [Fact]
    public void LoadRejectsOversizedIndexAtFileBoundary()
    {
        using var workspace = TempWorkspace.Create("package-trust-index-oversized");
        string path = workspace.Write(
            "package-trust-index.json",
            new byte[ProfileBundlePackageTrustIndexLoader.MaximumBytes + 1]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(path));

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ProfileBundlePackageTrustIndexLoader.MaximumBytes.ToString(CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>The checked-in index admits every reviewed bundle through immutable hash pins.</summary>
    [Fact]
    public void LoadReturnsVersionedHashClosedIndex()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "profiles",
            "built-in",
            "package-trust-index.json");

        ProfileBundlePackageTrustIndex index =
            ProfileBundlePackageTrustIndexLoader.Load(path);

        Assert.Equal("1.0", index.SchemaVersion);
        Assert.Equal("built-in-profile-bundles", index.TrustIndexId);
        Assert.Equal("0.10.5.1", index.TrustIndexVersion);
        Assert.Equal("built-in-profile-bundle-v2", index.TrustAnchorBindingId);
        Assert.Equal(26, index.Bundles.Count);
        Assert.Equal(
            61,
            index.Bundles.Sum(static bundle => bundle.RuntimeRegistrations.Count));
        ProfileBundleRuntimeRegistration generalReplace = index.Bundles
            .SelectMany(static bundle => bundle.RuntimeRegistrations)
            .Single(static registration => registration.WorkflowId == "general-replace");
        Assert.Equal("NT51926", generalReplace.IcId);
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            generalReplace.ProfileId);
        Assert.Equal("0.1.0", generalReplace.ProfileVersion);
        Assert.Equal(
            ["nt51928-dual-capacity-256k-512k", "nt51928-dual-capacity-256k-512k"],
            index.Bundles.SelectMany(static bundle => bundle.RuntimeRegistrations)
                .Where(static registration => registration.MapVariantSetId is not null)
                .Select(static registration => registration.MapVariantSetId));
        Assert.Equal(
            4,
            index.Bundles.Sum(static bundle => bundle.MetadataProviderFamilies.Count));
        Assert.Equal(
            index.Bundles.OrderBy(static bundle => bundle.BundleDirectory, StringComparer.Ordinal),
            index.Bundles);
        Assert.All(index.Bundles, static bundle =>
            Assert.Matches("^[0-9a-f]{64}$", bundle.ContentHash));
    }

    /// <summary>Duplicate bundle roots never acquire authority through array order.</summary>
    [Fact]
    public void LoadRejectsDuplicateBundleDirectory()
    {
        using TempWorkspace workspace = WriteIndex(
            Bundle(),
            Bundle());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(workspace.Root, "package-trust-index.json")));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Executable extension declarations are outside the closed trust-index schema.</summary>
    [Theory]
    [InlineData("script")]
    [InlineData("plugin")]
    [InlineData("dynamicAssembly")]
    [InlineData("watchPath")]
    public void LoadRejectsExecutableOrHotReloadExtensionFields(string propertyName)
    {
        string bundle = Bundle()[..^1] + $",\"{propertyName}\":\"forbidden\"}}";
        using TempWorkspace workspace = WriteIndex(bundle);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(workspace.Root, "package-trust-index.json")));

        Assert.Contains("does not satisfy schema", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Materialization sources cannot escape the reviewed built-in data root.</summary>
    [Fact]
    public void LoadRejectsParentTraversalMaterializationSource()
    {
        string bundle = Bundle(canonicalSource: "source/../private.json");
        using TempWorkspace workspace = WriteIndex(bundle);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(workspace.Root, "package-trust-index.json")));

        Assert.Contains("closed relative JSON path", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Existing workflow and selection-group vocabulary accepts a new IC/version as data.</summary>
    [Fact]
    public void LoadProjectsSyntheticExistingVocabularyRegistration()
    {
        string bundle = Bundle().Replace(
            "\"runtimeRegistrations\": []",
            "\"runtimeRegistrations\":[{" +
            "\"workflowId\":\"standard-merge\"," +
            "\"icId\":\"NT12345\"," +
            "\"profileId\":\"synthetic-standard-merge\"," +
            "\"profileVersion\":\"9.8.7\"," +
            "\"mapVariantSetId\":\"synthetic-selection-map-set\"}]",
            StringComparison.Ordinal);
        using TempWorkspace workspace = WriteIndex(bundle);

        ProfileBundleRuntimeRegistration registration = Assert.Single(
            Assert.Single(ProfileBundlePackageTrustIndexLoader.Load(
                    Path.Combine(workspace.Root, "package-trust-index.json"))
                .Bundles)
            .RuntimeRegistrations);

        Assert.Equal("NT12345", registration.IcId);
        Assert.Equal("9.8.7", registration.ProfileVersion);
        Assert.Equal("synthetic-selection-map-set", registration.MapVariantSetId);
    }

    private static TempWorkspace WriteIndex(params string[] bundles)
    {
        var workspace = TempWorkspace.Create("package-trust-index");
        string json = $$"""
            {
              "schemaVersion": "1.0",
              "trustIndexId": "test-profile-bundles",
              "trustIndexVersion": "1.0.0",
              "trustAnchorBindingId": "test-profile-bundle-v2",
              "bundles": [{{string.Join(',', bundles)}}]
            }
            """;
        _ = workspace.Write("package-trust-index.json", Encoding.UTF8.GetBytes(json));
        return workspace;
    }

    private static string Bundle(string canonicalSource = "source/families/family.json")
    {
        return $$"""
            {
              "bundleDirectory": "test-bundle",
              "bundleSchemaVersion": "1.0",
              "bundleVersion": "1.0.0",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "materialization": {
                "compositionProfileSchemaFile": "composition-profile-v2.schema.json",
                "firmwareFamilySchemaFile": "firmware-family-v1.schema.json",
                "canonicalFirmwareFamily": {
                  "source": "{{canonicalSource}}",
                  "destination": "families/family.json"
                }
              },
              "runtimeRegistrations": []
            }
            """;
    }
}
