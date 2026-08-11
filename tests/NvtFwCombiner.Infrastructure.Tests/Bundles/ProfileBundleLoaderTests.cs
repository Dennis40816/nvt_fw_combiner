using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests the trusted manifest-to-snapshot bundle loading boundary.</summary>
public sealed class ProfileBundleLoaderTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/synthetic-profile.schema.json";

    /// <summary>Verifies a release-bound manifest returns only validated immutable snapshots.</summary>
    [Fact]
    public void LoadReturnsAnchorVerifiedSchemaValidatedBundle()
    {
        using TempWorkspace workspace = PrepareBundle(out ProfileBundleTrustAnchor trustAnchor, out string contentHash);

        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            trustAnchor,
            Limits());

        Assert.Equal(contentHash, bundle.Manifest.ContentHash);
        Assert.Equal("release-manifest", bundle.Manifest.TrustAnchorBindingId);
        Assert.Equal(2, bundle.Manifest.Entries.Count);
        Assert.Matches("^[0-9a-f]{64}$", bundle.ManifestSha256);
    }

    /// <summary>Verifies a caller cannot accept a manifest that names the wrong external trust binding.</summary>
    [Fact]
    public void LoadRejectsMismatchedExternalTrustAnchorBinding()
    {
        using TempWorkspace workspace = PrepareBundle(out _, out string contentHash);
        var wrongBinding = new ProfileBundleTrustAnchor(contentHash, "other-release-manifest");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            wrongBinding,
            Limits()));

        Assert.Contains("trust-anchor binding", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a caller cannot accept a manifest with content not named by its external trust authority.</summary>
    [Fact]
    public void LoadRejectsMismatchedExternalContentHash()
    {
        using TempWorkspace workspace = PrepareBundle(out _, out _);
        var wrongContentHash = new ProfileBundleTrustAnchor(new string('0', 64), "release-manifest");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            wrongContentHash,
            Limits()));

        Assert.Contains("content hash", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the compiled bootstrap schema rejects manifest values that source generation alone permits.</summary>
    [Fact]
    public void LoadRejectsManifestOutsideBootstrapSchema()
    {
        using TempWorkspace workspace = PrepareBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            out _,
            bundleVersion: "not-semver");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            trustAnchor,
            Limits()));

        Assert.Contains("profile-bundle.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("profile-bundle-v1.schema.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies content is rejected after its hash and manifest trust checks when it violates its listed schema.</summary>
    [Fact]
    public void LoadRejectsEntryOutsideListedSchema()
    {
        using TempWorkspace workspace = PrepareBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            out _,
            profileJson: /*lang=json,strict*/ "{\"value\":\"wrong\"}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            trustAnchor,
            Limits()));

        Assert.Contains("profiles/synthetic.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a manifest changed after entry capture is rejected by the final raw-hash re-read.</summary>
    [Fact]
    public void LoadRejectsManifestMutationAfterEntryCapture()
    {
        using TempWorkspace workspace = PrepareBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            out _);
        string manifestPath = workspace.PathFor("profile-bundle.json");
        var source = new MutatingProfileBundleSnapshotSource(
            workspace.Root,
            "profile-bundle.json",
            manifestPath);

        IOException exception = Assert.Throws<IOException>(() => ProfileBundleLoader.Load(
            source,
            trustAnchor,
            Limits()));

        Assert.Equal(1, source.CaptureCount);
        Assert.Contains("manifest changed", exception.Message, StringComparison.Ordinal);
    }

    private static ProfileBundleLoadLimits Limits()
    {
        return new ProfileBundleLoadLimits(
            4096,
            32,
            new ProfileBundleEntrySnapshotLimits(8, 4096, 8192, 8));
    }

    private static TempWorkspace PrepareBundle(
        out ProfileBundleTrustAnchor trustAnchor,
        out string contentHash,
        string bundleVersion = "1.0.0",
        string? profileJson = null)
    {
        string profile = profileJson ?? /*lang=json,strict*/ "{\"value\":1}";
        byte[] schemaBytes = Encoding.UTF8.GetBytes(Schema());
        byte[] profileBytes = Encoding.UTF8.GetBytes(profile);
        ProfileBundleEntryDocument[] entries =
        [
            new ProfileBundleEntryDocument(
                "schema",
                "schema",
                "schemas/synthetic-profile.schema.json",
                SchemaId,
                Hash(schemaBytes)),
            new ProfileBundleEntryDocument(
                "profile",
                "composition-profile",
                "profiles/synthetic.json",
                SchemaId,
                Hash(profileBytes)),
        ];
        contentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        trustAnchor = new ProfileBundleTrustAnchor(contentHash, "release-manifest");

        var workspace = TempWorkspace.Create("nfc-profile-bundle-loader");
        _ = workspace.Write("schemas/synthetic-profile.schema.json", schemaBytes);
        _ = workspace.Write("profiles/synthetic.json", profileBytes);
        _ = workspace.Write(
            "profile-bundle.json",
            Encoding.UTF8.GetBytes(Manifest(entries, contentHash, bundleVersion)));
        return workspace;
    }

    private static string Manifest(
        IEnumerable<ProfileBundleEntryDocument> entries,
        string contentHash,
        string bundleVersion)
    {
        string entryJson = string.Join(
            ',',
            entries.Select(static entry => $$"""
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
              "bundleVersion": "{{bundleVersion}}",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "{{contentHash}}",
              "trustAnchorBindingId": "release-manifest",
              "entries": [{{entryJson}}]
            }
            """;
    }

    private static string Schema()
    {
        return /*lang=json,strict*/ $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaId}}",
              "type": "object",
              "additionalProperties": false,
              "required": ["value"],
              "properties": {
                "value": { "type": "integer" }
              }
            }
            """;
    }

    private sealed class MutatingProfileBundleSnapshotSource : IProfileBundleSnapshotSource
    {
        private readonly DirectoryProfileBundleSnapshotSource _inner;
        private readonly string _manifestPath;
        private int _manifestReadCount;

        internal int CaptureCount { get; private set; }

        internal MutatingProfileBundleSnapshotSource(
            string bundleRoot,
            string manifestPath,
            string manifestFilePath)
        {
            _inner = new DirectoryProfileBundleSnapshotSource(bundleRoot, manifestPath);
            _manifestPath = manifestFilePath;
        }

        public ProfileBundleFileSnapshot ReadManifest(int maximumBytes)
        {
            _manifestReadCount++;
            if (_manifestReadCount == 2)
            {
                if (CaptureCount != 1)
                {
                    throw new InvalidOperationException(
                        "The loader must capture entries before it verifies the manifest snapshot.");
                }

                File.AppendAllText(_manifestPath, Environment.NewLine);
            }

            return _inner.ReadManifest(maximumBytes);
        }

        public ProfileBundleEntrySnapshotCollection CaptureEntries(
            ProfileBundleManifest manifest,
            ProfileBundleEntrySnapshotLimits limits)
        {
            CaptureCount++;
            return _inner.CaptureEntries(manifest, limits);
        }
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
