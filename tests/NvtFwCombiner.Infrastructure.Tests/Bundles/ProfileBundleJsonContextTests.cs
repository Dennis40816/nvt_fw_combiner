using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests source-generated strict binding for canonical bundle DTO roots.</summary>
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

    /// <summary>Verifies family and profile roots are included without reflection fallback.</summary>
    [Fact]
    public void ContextIncludesCanonicalContentRoots()
    {
        Assert.Equal(
            "NvtFwCombiner.Contracts.Firmware.FirmwareFamilyDocument",
            ProfileBundleJsonContext.Default.FirmwareFamilyDocument.Type.FullName);
        Assert.Equal(
            "NvtFwCombiner.Contracts.Profiles.CompositionProfileDocument",
            ProfileBundleJsonContext.Default.CompositionProfileDocument.Type.FullName);
    }
}
