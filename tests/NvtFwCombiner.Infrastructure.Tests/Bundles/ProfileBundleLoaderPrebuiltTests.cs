using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>The accepted-token gateway preserves immutable bytes and complete document closure.</summary>
public sealed class ProfileBundleLoaderPrebuiltTests
{
    /// <summary>Neither disk replacement nor mutation of exported copies changes accepted bytes.</summary>
    [Fact]
    public void AcceptedMemorySourceRetainsExactBytesAfterPackReplacement()
    {
        using var workspace = TempWorkspace.Create("accepted-memory");
        ProfileBundlePackageTrustIndex index = PrebuiltProfileCatalogAcceptanceTests.ReviewedIndex();
        string path = workspace.Write("profiles/built-in/prebuilt-profile-catalog.pack", PrebuiltProfileCatalogAcceptanceTests.ReviewedPack(index));
        AcceptedPrebuiltProfileCatalog accepted = Assert.IsType<AcceptedPrebuiltProfileCatalog>(
            AcceptedPrebuiltProfileCatalog.TryAccept(workspace.Root, index, out _));
        string directory = index.Bundles[0].BundleDirectory;
        TrustedProfileBundleDocumentProjection first = ProfileBundleLoader.Load(accepted, directory).CreateDocumentProjection();
        byte[] expected = first.ManifestSnapshot.Content.ToArray();
        byte[] copy = first.ManifestSnapshot.Content.ToArray();
        Array.Fill(copy, (byte)0);
        File.WriteAllText(path, "replaced");
        TrustedProfileBundleDocumentProjection second = ProfileBundleLoader.Load(accepted, directory).CreateDocumentProjection();
        Assert.Equal(expected, second.ManifestSnapshot.Content.ToArray());
        Assert.Equal(first.Documents.Select(d => d.FileSnapshot.Content.ToArray()), second.Documents.Select(d => d.FileSnapshot.Content.ToArray()));
        _ = Assert.Throws<KeyNotFoundException>(() => ProfileBundleLoader.Load(accepted, "unlisted"));
        Assert.All(typeof(AcceptedPrebuiltProfileCatalog).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), c => Assert.True(c.IsPrivate));
    }

    /// <summary>Coherent value fixtures reach checks behind the cryptographic binding without minting tokens.</summary>
    [Theory]
    [InlineData("missing-document", "DocumentSetMismatch")]
    [InlineData("extra-document", "DocumentSetMismatch")]
    [InlineData("schema-reference", "DocumentSetMismatch")]
    [InlineData("document-hash", "DocumentIntegrity")]
    [InlineData("entry-count", "EntryLimits")]
    [InlineData("entry-size", "EntryLimits")]
    public void DocumentClosureAndLimitsAreCheckedWithoutGrantingTrust(string mutation, string expected)
    {
        var schema = new ProfileBundleEntry("schema", ProfileBundleEntryKind.Schema, "schemas/f.json", "schema", new string('a', 64));
        var entry = new ProfileBundleEntry("family", ProfileBundleEntryKind.FirmwareFamily, "families/f.json", "schema", new string('b', 64));
        var manifest = new ProfileBundleManifest("bundle", "1.0.0", new string('c', 64), "anchor", mutation == "schema-reference" ? [entry] : [schema, entry]);
        var documents = new JsonArray(new JsonObject
        {
            ["entryId"] = "family",
            ["kind"] = "firmware-family",
            ["path"] = "families/f.json",
            ["schemaId"] = "schema",
            ["contentHash"] = new string('b', 64),
            ["length"] = 2,
        });
        ProfileBundleLoadLimits limits = BuiltInProfileBundleAdmissionSettings.Limits;
        switch (mutation)
        {
            case "missing-document": documents.Clear(); break;
            case "extra-document": documents.Add(documents[0]!.DeepClone()); break;
            case "document-hash": documents[0]!["contentHash"] = new string('d', 64); break;
            case "entry-count": limits = new(16384, 32, new(1, 131072, 262144, 8)); break;
            case "entry-size": documents[0]!["length"] = 131073; break;
            case "schema-reference": break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        using JsonDocument json = JsonDocument.Parse(documents.ToJsonString());
        BuiltInProfileAdmissionRejectionReason? reason = AcceptedPrebuiltProfileCatalog.ValidateDocuments(manifest, json.RootElement, limits);
        Assert.Equal(expected, reason.ToString());
    }
}
