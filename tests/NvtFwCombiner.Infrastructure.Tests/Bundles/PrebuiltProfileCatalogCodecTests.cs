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

    /// <summary>The complete pack retains the independent documents-then-manifest vector.</summary>
    [Fact]
    public void CompletePackRetainsRawOrderAndClosedCanonicalHeader()
    {
        using var workspace = TempWorkspace.Create("prebuilt-pack-vector");
        byte[] pack = PrebuiltProfileCatalogCodec.Encode(new(new string('b', 64), "index", "1.0.0", "anchor"),
            [new("bundle", "1.0.0", new string('a', 64),
                Snapshot(workspace, "manifest.json", "{}"u8.ToArray()),
                [Document(workspace, "a", ProfileBundleEntryKind.FirmwareFamily, "[]"u8.ToArray())])]);
        Assert.Equal("NFCPBCAT"u8.ToArray(), pack[..8]);
        int length = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(pack.AsSpan(8, 4)));
        string header = System.Text.Encoding.UTF8.GetString(pack[12..(12 + length)]);
        Assert.StartsWith("{\"body\":{\"length\":4,\"sha256\":\"", header, StringComparison.Ordinal);
        Assert.Contains("\"manifest\":{\"length\":2,\"offset\":2,\"sha256\":\"44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a\"}", header, StringComparison.Ordinal);
        Assert.Equal("[]{}"u8.ToArray(), pack[(12 + length)..]);
        Assert.Equal("[]{}"u8.ToArray(), PrebuiltProfileCatalogCodec.Decode(pack).Body.ToArray());
    }

    /// <summary>Independent RFC 8785 string escaping and ordinal property-order vector.</summary>
    [Fact]
    public void CanonicalJsonMatchesIndependentUnicodeAndControlVector()
    {
        using var json = System.Text.Json.JsonDocument.Parse("{\"z\":1,\"\u00e9\":\"/<>&\\u000f\\b\\t\\n\\f\\r\\\"\\\\\",\"a\":0}");
        Assert.Equal("{\"a\":0,\"z\":1,\"\u00e9\":\"/<>&\\u000f\\b\\t\\n\\f\\r\\\"\\\\\"}",
            System.Text.Encoding.UTF8.GetString(PrebuiltProfileCatalogCanonicalJson.Encode(json.RootElement)));
    }

    /// <summary>Adversarial headers cannot smuggle unbounded, ambiguous, or misordered ranges.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("bom")]
    [InlineData("whitespace")]
    [InlineData("utf8")]
    [InlineData("fraction")]
    [InlineData("negative-zero")]
    [InlineData("overflow")]
    [InlineData("depth")]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("range-hash")]
    [InlineData("body-hash")]
    [InlineData("trailing")]
    [InlineData("magic")]
    [InlineData("header-bound")]
    [InlineData("empty-header")]
    [InlineData("truncated")]
    public void DecoderRejectsMalformedPack(string mutation)
    {
        using var workspace = TempWorkspace.Create("prebuilt-pack-rejection");
        byte[] original = PrebuiltProfileCatalogCodec.Encode(new(new string('b', 64), "index", "1.0.0", "anchor"),
            [new("bundle", "1.0.0", new string('a', 64), Snapshot(workspace, "manifest.json", "{}"u8.ToArray()),
                [Document(workspace, "a", ProfileBundleEntryKind.FirmwareFamily, "[]"u8.ToArray())])]);
        int length = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(8, 4));
        string header = System.Text.Encoding.UTF8.GetString(original, 12, length);
        header = mutation switch
        {
            "unknown" => header.Replace("\"formatVersion\":1", "\"extra\":0,\"formatVersion\":1", StringComparison.Ordinal),
            "duplicate" => header.Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1", StringComparison.Ordinal),
            "version" => header.Replace("\"formatVersion\":1", "\"formatVersion\":2", StringComparison.Ordinal),
            "fraction" => header.Replace("\"formatVersion\":1", "\"formatVersion\":1.0", StringComparison.Ordinal),
            "negative-zero" => header.Replace("\"offset\":0", "\"offset\":-0", StringComparison.Ordinal),
            "overflow" => header.Replace("\"offset\":0", "\"offset\":9223372036854775808", StringComparison.Ordinal),
            "depth" => header.Replace("\"formatVersion\":1", "\"formatVersion\":" + new string('[', 17) + "1" + new string(']', 17), StringComparison.Ordinal),
            "gap" => header.Replace("\"offset\":2", "\"offset\":3", StringComparison.Ordinal),
            "overlap" => header.Replace("\"offset\":2", "\"offset\":0", StringComparison.Ordinal),
            "range-hash" => header.Replace("44136fa3", "54136fa3", StringComparison.Ordinal),
            "whitespace" => " " + header,
            "bom" => "\ufeff" + header,
            _ => header,
        };
        byte[] changedHeader = System.Text.Encoding.UTF8.GetBytes(header);
        byte[] pack = new byte[12 + changedHeader.Length + 4 + (mutation == "trailing" ? 1 : 0)];
        original.AsSpan(0, 8).CopyTo(pack);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(pack.AsSpan(8, 4), (uint)changedHeader.Length);
        changedHeader.CopyTo(pack.AsSpan(12));
        "[]{}"u8.CopyTo(pack.AsSpan(12 + changedHeader.Length));
        switch (mutation)
        {
            case "utf8": pack[13] = 0xff; break;
            case "body-hash": pack[^1] = 0; break;
            case "magic": pack[0] = 0; break;
            case "header-bound": System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(pack.AsSpan(8, 4), 1_048_577); break;
            case "empty-header": Array.Clear(pack, 8, 4); break;
            case "truncated": pack = pack[..^1]; break;
            default: break;
        }
        _ = Assert.Throws<InvalidDataException>(() => PrebuiltProfileCatalogCodec.Decode(pack));
    }

    /// <summary>Length is checked before any snapshot read, and the complete encoder budgets its header.</summary>
    [Fact]
    public void CompleteFileBoundIsCheckedBeforeAllocationOrPromotion()
    {
        using var oversized = new MemoryStream(new byte[4_194_305]);
        _ = Assert.Throws<InvalidDataException>(() => PrebuiltProfileCatalogCodec.Read(oversized));
        Assert.Equal(0, oversized.Position);
        using var workspace = TempWorkspace.Create("prebuilt-pack-bound");
        _ = Assert.Throws<InvalidDataException>(() => PrebuiltProfileCatalogCodec.Encode(
            new(new string('b', 64), "index", "1.0.0", "anchor"),
            [new("bundle", "1.0.0", new string('a', 64), Snapshot(workspace, "manifest.json", new byte[4_194_290]), [])]));
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
