using System.Globalization;
using System.Security.Cryptography;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Independent ADR 0077 body vectors; these are transport bytes, not admitted firmware documents.</summary>
public sealed class PrebuiltProfileCatalogCodecTests
{
    /// <summary>Pins bundle order and combined document order, followed by each manifest, byte for byte.</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void BodyMatchesIndependentDocumentsThenManifestVector(string culture)
    {
        using var workspace = TempWorkspace.Create("prebuilt-body-vector");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var first = new PrebuiltProfileCatalogBodyInput("I-bundle",
                Snapshot(workspace, "first-manifest.json", " {}\r\n"u8.ToArray()),
                [Document(workspace, "z", ProfileBundleEntryKind.FirmwareFamily, "[3]"u8.ToArray()),
                 Document(workspace, "a", ProfileBundleEntryKind.FirmwareFamily, "[2]"u8.ToArray()),
                 Document(workspace, "A", ProfileBundleEntryKind.CompositionProfile, "[1]"u8.ToArray())]);
            var second = new PrebuiltProfileCatalogBodyInput("i-bundle",
                Snapshot(workspace, "second-manifest.json", " { }\n"u8.ToArray()),
                [Document(workspace, "b", ProfileBundleEntryKind.CompositionProfile, "\"\\u0061\""u8.ToArray())]);

            PrebuiltProfileCatalogBody body = PrebuiltProfileCatalogCodec.EncodeBody([second, first]);

            // Hand-transcribed UTF-8 hex, independent of the encoder: [1][2][3] {} CR LF
            // then the literal JSON escape "\u0061", then space { space } LF. No separator is inserted.
            byte[] expected = Convert.FromHexString("5B315D5B325D5B335D207B7D0D0A225C753030363122207B207D0A");
            Assert.Equal(expected, body.Content.ToArray());
            Assert.Equal(
                [("I-bundle", "A", 0, 3), ("I-bundle", "a", 3, 3), ("I-bundle", "z", 6, 3),
                 ("I-bundle", null, 9, 5), ("i-bundle", "b", 14, 8), ("i-bundle", null, 22, 5)],
                body.Ranges.Select(x => (x.BundleDirectory, x.EntryId, x.Offset, x.Length)));
            Assert.True(((IList<PrebuiltProfileCatalogBodyRange>)body.Ranges).IsReadOnly);
            byte[] copy = body.Content.ToArray();
            Array.Fill(copy, (byte)0);
            Assert.Equal(expected, body.Content.ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>The transport rejects duplicate identities instead of creating ambiguous header ranges.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BodyRejectsDuplicateBundleOrDocumentIdentity(bool duplicateBundle)
    {
        using var workspace = TempWorkspace.Create("prebuilt-body-duplicate");
        ProfileBundleEntrySnapshot document = Document(workspace, "a", ProfileBundleEntryKind.FirmwareFamily, "{}"u8.ToArray());
        var bundle = new PrebuiltProfileCatalogBodyInput("bundle",
            Snapshot(workspace, "manifest.json", "{}"u8.ToArray()), duplicateBundle ? [document] : [document, document]);

        _ = Assert.Throws<InvalidDataException>(() =>
            PrebuiltProfileCatalogCodec.EncodeBody(duplicateBundle ? [bundle, bundle] : [bundle]));
    }

    /// <summary>Noncarried entries cannot enter the body even if supplied directly to the transport.</summary>
    [Theory]
    [InlineData((int)ProfileBundleEntryKind.Schema)]
    [InlineData((int)ProfileBundleEntryKind.EvidenceManifest)]
    [InlineData((int)ProfileBundleEntryKind.SavedCompositionRule)]
    public void BodyRejectsNonDocumentEntries(int kind)
    {
        using var workspace = TempWorkspace.Create("prebuilt-body-kind");
        var bundle = new PrebuiltProfileCatalogBodyInput("bundle",
            Snapshot(workspace, "manifest.json", "{}"u8.ToArray()),
            [Document(workspace, "entry", (ProfileBundleEntryKind)kind, "{}"u8.ToArray())]);

        _ = Assert.Throws<InvalidDataException>(() => PrebuiltProfileCatalogCodec.EncodeBody([bundle]));
    }

    /// <summary>Body budget reserves at least the prefix and one header byte before output allocation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BodyEnforcesTheFileBudget(int extraByte)
    {
        using var workspace = TempWorkspace.Create("prebuilt-body-bound");
        byte[] content = new byte[4_194_304 - 12 - 1 - 2 + extraByte];
        Array.Fill(content, (byte)' ');
        var bundle = new PrebuiltProfileCatalogBodyInput("bundle",
            Snapshot(workspace, "manifest.json", "{}"u8.ToArray()),
            [Document(workspace, "entry", ProfileBundleEntryKind.FirmwareFamily, content)]);

        if (extraByte == 0)
        {
            Assert.Equal(4_194_291, PrebuiltProfileCatalogCodec.EncodeBody([bundle]).Content.Length);
        }
        else
        {
            _ = Assert.Throws<InvalidDataException>(() => PrebuiltProfileCatalogCodec.EncodeBody([bundle]));
        }
    }

    private static ProfileBundleEntrySnapshot Document(
        TempWorkspace workspace, string entryId, ProfileBundleEntryKind kind, byte[] bytes)
    {
        ProfileBundleFileSnapshot snapshot = Snapshot(workspace, entryId + ".json", bytes);
        var entry = new ProfileBundleEntry(entryId, kind, snapshot.ManifestPath,
            "https://example.invalid/vector.schema.json", Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return new ProfileBundleEntrySnapshot(entry, snapshot);
    }

    private static ProfileBundleFileSnapshot Snapshot(TempWorkspace workspace, string path, byte[] bytes)
    {
        _ = workspace.Write(path, bytes);
        return ProfileBundleFileSnapshot.ReadManifest(workspace.Root, path, 4_194_304);
    }
}
