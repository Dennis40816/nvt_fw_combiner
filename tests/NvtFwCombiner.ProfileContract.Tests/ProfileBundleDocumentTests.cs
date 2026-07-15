using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests strict JSON transport mapping for profile-bundle-v1 manifests.</summary>
public sealed class ProfileBundleDocumentTests
{
    /// <summary>Verifies every manifest and entry field maps without inventing trust semantics.</summary>
    [Fact]
    public void CompleteBundleJsonMapsToTransportDocument()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "bundleId": "production-bundle",
              "bundleVersion": "0.9.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "trustAnchorBindingId": "release-manifest",
              "entries": [
                {
                  "entryId": "composition-profile-v2-schema",
                  "kind": "schema",
                  "path": "schemas/composition-profile-v2.schema.json",
                  "schemaId": "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json",
                  "contentHash": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                },
                {
                  "entryId": "synthetic-merge",
                  "kind": "composition-profile",
                  "path": "profiles/synthetic-merge.json",
                  "schemaId": "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json",
                  "contentHash": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                }
              ]
            }
            """;

        ProfileBundleDocument bundle = Assert.IsType<ProfileBundleDocument>(
            JsonSerializer.Deserialize<ProfileBundleDocument>(json, CompositionProfileDocumentTests.StrictOptions()));

        Assert.Equal("1.0", bundle.SchemaVersion);
        Assert.Equal("production-bundle", bundle.BundleId);
        Assert.Equal("0.9.0", bundle.BundleVersion);
        Assert.Equal("sha256-rfc8785-entry-array-v1", bundle.HashAlgorithm);
        Assert.Equal("release-manifest", bundle.TrustAnchorBindingId);
        Assert.Equal(2, bundle.Entries.Count);
        Assert.Equal("schema", bundle.Entries[0].Kind);
        Assert.Equal("profiles/synthetic-merge.json", bundle.Entries[1].Path);
    }

    /// <summary>Verifies strict transport settings reject unknown manifest and entry members.</summary>
    [Fact]
    public void StrictTransportRejectsUnknownMembers()
    {
        const string manifest = """
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "trustAnchorBindingId": "release",
              "entries": [],
              "unexpected": true
            }
            """;
        const string entry = """
            {
              "entryId": "profile",
              "kind": "composition-profile",
              "path": "profiles/profile.json",
              "schemaId": "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "command": "forbidden"
            }
            """;

        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProfileBundleDocument>(
            manifest,
            CompositionProfileDocumentTests.StrictOptions()));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProfileBundleEntryDocument>(
            entry,
            CompositionProfileDocumentTests.StrictOptions()));
    }
}
