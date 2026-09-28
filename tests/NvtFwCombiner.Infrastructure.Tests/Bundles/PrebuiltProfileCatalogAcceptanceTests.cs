using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Build-bound acceptance cannot be granted by repaired delivery checksums.</summary>
public sealed class PrebuiltProfileCatalogAcceptanceTests
{
    /// <summary>Every carried byte is bound to compiled reviewed identities before a token is minted.</summary>
    [Fact]
    public void ReviewedPackIsAcceptedWithoutAnyDiskBundle()
    {
        using var workspace = TempWorkspace.Create("prebuilt-acceptance");
        ProfileBundlePackageTrustIndex index = ReviewedIndex();
        _ = workspace.Write("profiles/built-in/prebuilt-profile-catalog.pack", ReviewedPack(index));
        AcceptedPrebuiltProfileCatalog? accepted = AcceptedPrebuiltProfileCatalog.TryAccept(workspace.Root, index, out BuiltInProfileAdmissionRejectionReason? reason);
        Assert.Null(reason);
        Assert.NotNull(accepted);
        foreach (ProfileBundlePackageTrustEntry entry in index.Bundles)
        {
            TrustedProfileBundle bundle = ProfileBundleLoader.Load(accepted, entry.BundleDirectory);
            Assert.Equal(entry.ContentHash, bundle.Manifest.ContentHash);
            Assert.NotEmpty(bundle.CreateDocumentProjection().Documents);
        }
    }

    /// <summary>Mutations repair the container around the intended failing check.</summary>
    [Theory]
    [InlineData("missing", "missing")]
    [InlineData("format", "format")]
    [InlineData("body", "body-integrity")]
    [InlineData("index", "trust-index-mismatch")]
    [InlineData("bundle", "bundle-set-mismatch")]
    [InlineData("manifest-range", "manifest-set-mismatch")]
    [InlineData("bundle-id", "manifest-set-mismatch")]
    [InlineData("document-range", "document-integrity")]
    [InlineData("document-path", "document-set-mismatch")]
    [InlineData("document-schema", "document-set-mismatch")]
    [InlineData("document-kind", "document-set-mismatch")]
    public void RepairedContainerCannotReplaceBuildAuthority(string mutation, string expected)
    {
        using var workspace = TempWorkspace.Create("prebuilt-rejection");
        ProfileBundlePackageTrustIndex index = ReviewedIndex();
        byte[] pack = ReviewedPack(index);
        int headerLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(pack.AsSpan(8, 4));
        JsonNode header = JsonNode.Parse(pack.AsSpan(12, headerLength))!;
        byte[] body = pack[(12 + headerLength)..];
        JsonNode bundle = header["bundles"]![0]!;
        switch (mutation)
        {
            case "format": header["formatVersion"] = 2; break;
            case "body": body[0] ^= 1; break;
            case "index": header["trustIndex"]!["sha256"] = new string('a', 64); break;
            case "bundle": bundle["bundleVersion"] = "0.0.0"; break;
            case "manifest-range": bundle["manifest"]!["sha256"] = new string('a', 64); break;
            case "document-range": bundle["documents"]![0]!["contentHash"] = new string('a', 64); break;
            case "document-path": bundle["documents"]![0]!["path"] = "families/changed.json"; break;
            case "document-schema": bundle["documents"]![0]!["schemaId"] = "https://example.invalid/changed"; break;
            case "document-kind": bundle["documents"]![0]!["kind"] = bundle["documents"]![0]!["kind"]!.GetValue<string>() == "composition-profile" ? "firmware-family" : "composition-profile"; break;
            case "bundle-id":
                JsonNode range = bundle["manifest"]!;
                int offset = range["offset"]!.GetValue<int>(), length = range["length"]!.GetValue<int>();
                byte[] manifest = body.AsSpan(offset, length).ToArray();
                byte[] marker = "\"bundleId\": \""u8.ToArray();
                int start = manifest.AsSpan().IndexOf(marker) + marker.Length;
                Assert.True(start >= marker.Length);
                manifest[start] = manifest[start] == (byte)'x' ? (byte)'y' : (byte)'x';
                manifest.CopyTo(body.AsSpan(offset));
                range["sha256"] = Hash(manifest);
                header["body"]!["sha256"] = Hash(body);
                break;
            case "missing": break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        if (mutation != "missing") { _ = workspace.Write("profiles/built-in/prebuilt-profile-catalog.pack", Repack(header, body)); }
        Assert.Null(AcceptedPrebuiltProfileCatalog.TryAccept(workspace.Root, index, out BuiltInProfileAdmissionRejectionReason? reason));
        Assert.Equal(expected, new BuiltInProfileAdmission(BuiltInProfileAdmissionSource.Json, reason).RejectionCode);
    }

    /// <summary>Field identity, rather than exception text, selects the stable observation category.</summary>
    [Theory]
    [InlineData(0, "Missing", "trust-index-mismatch")]
    [InlineData(0, "Duplicate", "trust-index-mismatch")]
    [InlineData(0, "Invalid", "trust-index-mismatch")]
    [InlineData(1, "Missing", "manifest-set-mismatch")]
    [InlineData(1, "Duplicate", "manifest-set-mismatch")]
    [InlineData(1, "Invalid", "manifest-set-mismatch")]
    public void MetadataFailureMapsByTypedField(int field, string kind, string expected)
    {
        var failure = new BuildAdmissionIdentityFailure((BuildAdmissionIdentityField)field,
            Enum.Parse<BuildAdmissionIdentityFailureKind>(kind));
        Assert.Equal(expected, new BuiltInProfileAdmission(BuiltInProfileAdmissionSource.Json,
            AcceptedPrebuiltProfileCatalog.MapIdentityFailure(failure)).RejectionCode);
    }

    internal static ProfileBundlePackageTrustIndex ReviewedIndex()
    {
        return ProfileBundlePackageTrustIndexLoader.Load(
        RepositoryPaths.FromRepositoryRoot("profiles", "built-in", "package-trust-index.json"));
    }

    internal static byte[] ReviewedPack(ProfileBundlePackageTrustIndex index)
    {
        string root = RepositoryPaths.FromRepositoryRoot("profiles", "built-in");
        var inputs = new List<PrebuiltProfileCatalogBundleInput>();
        foreach (ProfileBundlePackageTrustEntry entry in index.Bundles)
        {
            string directory = Path.Combine(root, entry.BundleDirectory);
            ProfileBundleFileSnapshot manifest = ProfileBundleFileSnapshot.ReadManifest(directory, "profile-bundle.json", 16384);
            ProfileBundleManifest admitted = ProfileBundleLoader.AdmitManifest(manifest,
                new(entry.ContentHash, index.TrustAnchorBindingId), BuiltInProfileBundleAdmissionSettings.Limits);
            var documents = new List<ProfileBundleEntrySnapshot>();
            foreach (ProfileBundleEntry document in admitted.Entries.Where(e => e.Kind is ProfileBundleEntryKind.FirmwareFamily or ProfileBundleEntryKind.CompositionProfile))
            {
                string path = Path.Combine(directory, document.Path);
                if (entry.Materialization.CanonicalFirmwareFamily is { } canonical && document.Path == canonical.Destination)
                { path = Path.Combine(root, canonical.Source); }
                documents.Add(new(document, ProfileBundleFileSnapshot.Copy(document.Path, File.ReadAllBytes(path), 131072)));
            }
            inputs.Add(new(entry.BundleDirectory, entry.BundleVersion, entry.ContentHash, manifest, documents));
        }
        return PrebuiltProfileCatalogCodec.Encode(new(index.ActualSha256, index.TrustIndexId, index.TrustIndexVersion, index.TrustAnchorBindingId), inputs);
    }

    internal static byte[] Repack(JsonNode header, byte[] body)
    {
        using JsonDocument json = JsonDocument.Parse(header.ToJsonString());
        byte[] encoded = PrebuiltProfileCatalogCanonicalJson.Encode(json.RootElement);
        byte[] pack = new byte[12 + encoded.Length + body.Length];
        "NFCPBCAT"u8.CopyTo(pack);
        BinaryPrimitives.WriteUInt32LittleEndian(pack.AsSpan(8), (uint)encoded.Length);
        encoded.CopyTo(pack.AsSpan(12));
        body.CopyTo(pack.AsSpan(12 + encoded.Length));
        return pack;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
