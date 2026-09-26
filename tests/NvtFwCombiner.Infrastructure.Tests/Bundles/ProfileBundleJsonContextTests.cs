using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests source-generated strict binding for the canonical bundle manifest root.</summary>
public sealed class ProfileBundleJsonContextTests
{
    /// <summary>Verifies one bundle manifest binds through generated metadata.</summary>
    [Fact]
    public void ContextDeserializesBundleManifest()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "trustAnchorBindingId": "release-manifest",
              "entries": []
            }
            """;

        ProfileBundleDocument bundle = Assert.IsType<ProfileBundleDocument>(
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.ProfileBundleDocument));

        Assert.Equal("bundle", bundle.BundleId);
        Assert.Empty(bundle.Entries);
    }

    /// <summary>Verifies generated metadata rejects unknown and case-mismatched members.</summary>
    [Theory]
    [InlineData("unexpected")]
    [InlineData("BundleId")]
    public void ContextRejectsUnmappedMembers(string propertyName)
    {
        string json = $$"""
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "trustAnchorBindingId": "release-manifest",
              "entries": [],
              "{{propertyName}}": true
            }
            """;

        _ = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.ProfileBundleDocument));
    }

    /// <summary>
    /// Verifies the manifest context generates no second family or profile graph; those roots bind only through
    /// the Profiles-owned metadata that the trusted projection shares with the catalog factory.
    /// </summary>
    [Fact]
    public void ContextOwnsOnlyTheBundleManifestRoot()
    {
        Assert.NotNull(ProfileBundleJsonContext.Default.GetTypeInfo(typeof(ProfileBundleDocument)));
        Assert.Null(ProfileBundleJsonContext.Default.GetTypeInfo(typeof(FirmwareFamilyDocument)));
        Assert.Null(ProfileBundleJsonContext.Default.GetTypeInfo(typeof(CompositionProfileDocument)));
    }
}
