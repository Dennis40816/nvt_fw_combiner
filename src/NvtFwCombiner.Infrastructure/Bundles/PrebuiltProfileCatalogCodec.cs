using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Pure byte transport for ADR 0077. Encoding never admits or normalizes a bundle.</summary>
internal static class PrebuiltProfileCatalogCodec
{
    /// <summary>
    /// Encodes canonical-header bundle order, each bundle's documents ordinal by entryId, then its manifest.
    /// The complete pack encoder must additionally budget its actual header against MaximumFileBytes.
    /// </summary>
    internal static PrebuiltProfileCatalogBody EncodeBody(IReadOnlyList<PrebuiltProfileCatalogBodyInput> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        if (bundles.Count is 0 or > PrebuiltProfileCatalogFormat.MaximumBodyBytes)
        {
            throw new InvalidDataException("Prebuilt catalog requires a bounded nonempty bundle set.");
        }

        // Bound the carried bytes before sorting inputs or allocating the output buffer/range table.
        int length = 0;
        foreach (PrebuiltProfileCatalogBodyInput bundle in bundles)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentException.ThrowIfNullOrWhiteSpace(bundle.BundleDirectory);
            ArgumentNullException.ThrowIfNull(bundle.Documents);
            length = AddLength(length, bundle.Manifest);
            foreach (ProfileBundleEntrySnapshot document in bundle.Documents)
            {
                ArgumentNullException.ThrowIfNull(document);
                if (document.Entry.Kind is not (ProfileBundleEntryKind.FirmwareFamily or ProfileBundleEntryKind.CompositionProfile))
                {
                    throw new InvalidDataException("Prebuilt catalog carries only family and profile documents.");
                }

                length = AddLength(length, document.FileSnapshot);
            }
        }

        var ordered = new List<(PrebuiltProfileCatalogBodyRange Range, ProfileBundleFileSnapshot Snapshot)>();
        int offset = 0;
        string? previousBundle = null;
        foreach (PrebuiltProfileCatalogBodyInput bundle in bundles.OrderBy(
                     static bundle => bundle.BundleDirectory, StringComparer.Ordinal))
        {
            if (StringComparer.Ordinal.Equals(previousBundle, bundle.BundleDirectory))
            {
                throw new InvalidDataException("Prebuilt catalog bundle directories must be unique.");
            }

            previousBundle = bundle.BundleDirectory;
            string? previousEntry = null;
            foreach (ProfileBundleEntrySnapshot document in bundle.Documents.OrderBy(
                         static document => document.Entry.EntryId, StringComparer.Ordinal))
            {
                if (StringComparer.Ordinal.Equals(previousEntry, document.Entry.EntryId))
                {
                    throw new InvalidDataException("Prebuilt catalog document entryIds must be unique within a bundle.");
                }

                previousEntry = document.Entry.EntryId;
                Append(bundle.BundleDirectory, document.Entry.EntryId, document.FileSnapshot);
            }

            Append(bundle.BundleDirectory, null, bundle.Manifest);
        }

        byte[] content = new byte[length];
        foreach ((PrebuiltProfileCatalogBodyRange range, ProfileBundleFileSnapshot snapshot) in ordered)
        {
            snapshot.Content.CopyTo(content.AsSpan(range.Offset, range.Length));
        }

        return new PrebuiltProfileCatalogBody(content, ordered.Select(static item => item.Range));

        void Append(string directory, string? entryId, ProfileBundleFileSnapshot snapshot)
        {
            ordered.Add((new PrebuiltProfileCatalogBodyRange(directory, entryId, offset, snapshot.Length), snapshot));
            offset = checked(offset + snapshot.Length);
        }
    }

    internal static byte[] Encode(PrebuiltProfileCatalogTrustIdentity trustIndex,
        IReadOnlyList<PrebuiltProfileCatalogBundleInput> bundles)
    {
        ArgumentNullException.ThrowIfNull(trustIndex);
        ArgumentNullException.ThrowIfNull(bundles);
        PrebuiltProfileCatalogBody body = EncodeBody([.. bundles.Select(b =>
            new PrebuiltProfileCatalogBodyInput(b.BundleDirectory, b.Manifest, b.Documents))]);
        var headers = new JsonArray();
        foreach (PrebuiltProfileCatalogBundleInput bundle in bundles.OrderBy(b => b.BundleDirectory, StringComparer.Ordinal))
        {
            var documents = new JsonArray();
            foreach (ProfileBundleEntrySnapshot document in bundle.Documents.OrderBy(d => d.Entry.EntryId, StringComparer.Ordinal))
            {
                PrebuiltProfileCatalogBodyRange range = body.Ranges.Single(r =>
                    r.BundleDirectory == bundle.BundleDirectory && r.EntryId == document.Entry.EntryId);
                documents.Add(new JsonObject
                {
                    ["entryId"] = document.Entry.EntryId,
                    ["kind"] = document.Entry.Kind == ProfileBundleEntryKind.FirmwareFamily ? "firmware-family" : "composition-profile",
                    ["path"] = document.Entry.Path,
                    ["schemaId"] = document.Entry.SchemaId,
                    ["contentHash"] = document.Entry.ContentHash,
                    ["offset"] = range.Offset,
                    ["length"] = range.Length,
                });
            }
            PrebuiltProfileCatalogBodyRange manifest = body.Ranges.Single(r =>
                r.BundleDirectory == bundle.BundleDirectory && r.EntryId is null);
            headers.Add(new JsonObject
            {
                ["bundleDirectory"] = bundle.BundleDirectory,
                ["bundleVersion"] = bundle.BundleVersion,
                ["contentHash"] = bundle.ContentHash,
                ["documents"] = documents,
                ["manifest"] = new JsonObject
                {
                    ["offset"] = manifest.Offset,
                    ["length"] = manifest.Length,
                    ["sha256"] = bundle.Manifest.ActualSha256
                },
            });
        }
        var root = new JsonObject
        {
            ["formatVersion"] = 1,
            ["trustIndex"] = new JsonObject
            {
                ["sha256"] = trustIndex.Sha256,
                ["trustIndexId"] = trustIndex.TrustIndexId,
                ["trustIndexVersion"] = trustIndex.TrustIndexVersion,
                ["trustAnchorBindingId"] = trustIndex.TrustAnchorBindingId
            },
            ["bundles"] = headers,
            ["body"] = new JsonObject { ["length"] = body.Content.Length, ["sha256"] = Hash(body.Content) },
        };
        using var documentHeader = JsonDocument.Parse(root.ToJsonString());
        byte[] header = PrebuiltProfileCatalogCanonicalJson.Encode(documentHeader.RootElement);
        if (header.Length > PrebuiltProfileCatalogFormat.MaximumHeaderBytes ||
            header.Length + body.Content.Length > PrebuiltProfileCatalogFormat.MaximumFileBytes - 12)
        { throw new InvalidDataException("Prebuilt catalog exceeds its header or file bound."); }
        byte[] result = new byte[12 + header.Length + body.Content.Length];
        "NFCPBCAT"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)header.Length);
        header.CopyTo(result.AsSpan(12));
        body.Content.CopyTo(result.AsSpan(12 + header.Length));
        _ = Decode(result); // One transport validator for encoder and reader; never runtime admission.
        return result;
    }

    internal static PrebuiltProfileCatalogSnapshot Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek || stream.Length is < 13 or > PrebuiltProfileCatalogFormat.MaximumFileBytes)
        { throw new InvalidDataException("Prebuilt catalog requires a bounded seekable stream."); }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return stream.ReadByte() != -1 ? throw new InvalidDataException("Prebuilt catalog changed during capture.") : Decode(bytes);
    }

    internal static PrebuiltProfileCatalogSnapshot Decode(ReadOnlyMemory<byte> bytes)
    {
        return DecodeCore(bytes, out _);
    }

    internal static PrebuiltProfileCatalogSnapshot? TryDecode(ReadOnlyMemory<byte> bytes,
        out PrebuiltProfileCatalogDecodeFailure failure)
    {
        failure = PrebuiltProfileCatalogDecodeFailure.Format;
        try { return DecodeCore(bytes, out failure); }
        catch (InvalidDataException) { return null; }
    }

    private static PrebuiltProfileCatalogSnapshot DecodeCore(ReadOnlyMemory<byte> bytes,
        out PrebuiltProfileCatalogDecodeFailure failure)
    {
        failure = PrebuiltProfileCatalogDecodeFailure.Format;
        if (bytes.Length is < 13 or > PrebuiltProfileCatalogFormat.MaximumFileBytes ||
            !bytes.Span[..8].SequenceEqual("NFCPBCAT"u8))
        { throw new InvalidDataException("Invalid prebuilt catalog prefix or file bound."); }
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span.Slice(8, 4));
        if (length is < 1 or > PrebuiltProfileCatalogFormat.MaximumHeaderBytes || length > bytes.Length - 12)
        { throw new InvalidDataException("Invalid prebuilt catalog header bound."); }
        try
        {
            ReadOnlyMemory<byte> headerBytes = bytes.Slice(12, (int)length);
            // Strict decoding rejects invalid UTF-8 before JsonElement string replacement could conceal it.
            _ = new System.Text.UTF8Encoding(false, true).GetString(headerBytes.Span);
            using JsonDocument parsed = StrictJsonDocumentReader.Parse(headerBytes, PrebuiltProfileCatalogFormat.MaximumHeaderBytes, 16);
            JsonElement header = parsed.RootElement;
            if (!headerBytes.Span.SequenceEqual(PrebuiltProfileCatalogCanonicalJson.Encode(header)))
            { throw new InvalidDataException("Prebuilt catalog header is not canonical JSON."); }
            ReadOnlySpan<byte> body = bytes.Span[(12 + (int)length)..];
            ValidateHeader(header, body, ref failure);
            return new PrebuiltProfileCatalogSnapshot(header, body.ToArray());
        }
        catch (Exception error) when (error is JsonException or System.Text.DecoderFallbackException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("Malformed prebuilt catalog header.", error); }
    }

    private static void ValidateHeader(JsonElement header, ReadOnlySpan<byte> body,
        ref PrebuiltProfileCatalogDecodeFailure failure)
    {
        Shape(header, "formatVersion", "trustIndex", "bundles", "body");
        if (Number(header, "formatVersion") != 1) { throw new InvalidDataException("Unknown prebuilt catalog version."); }
        JsonElement trust = header.GetProperty("trustIndex");
        Shape(trust, "sha256", "trustIndexId", "trustIndexVersion", "trustAnchorBindingId");
        _ = Digest(trust, "sha256");
        _ = Text(trust, "trustIndexId"); _ = Text(trust, "trustIndexVersion"); _ = Text(trust, "trustAnchorBindingId");
        JsonElement bodyHeader = header.GetProperty("body");
        Shape(bodyHeader, "length", "sha256");
        if (Number(bodyHeader, "length") != body.Length || Digest(bodyHeader, "sha256") != Hash(body))
        { failure = PrebuiltProfileCatalogDecodeFailure.BodyIntegrity; throw new InvalidDataException("Prebuilt catalog body integrity differs."); }
        JsonElement bundles = header.GetProperty("bundles");
        if (bundles.ValueKind != JsonValueKind.Array || bundles.GetArrayLength() == 0)
        { throw new InvalidDataException("Prebuilt catalog requires bundles."); }
        long offset = 0;
        string? previousBundle = null;
        foreach (JsonElement bundle in bundles.EnumerateArray())
        {
            Shape(bundle, "bundleDirectory", "bundleVersion", "contentHash", "manifest", "documents");
            string directory = Text(bundle, "bundleDirectory");
            Ordered(previousBundle, directory); previousBundle = directory;
            _ = Text(bundle, "bundleVersion"); _ = Digest(bundle, "contentHash");
            JsonElement documents = bundle.GetProperty("documents");
            if (documents.ValueKind != JsonValueKind.Array) { throw new InvalidDataException("Invalid document array."); }
            string? previousEntry = null;
            foreach (JsonElement document in documents.EnumerateArray())
            {
                Shape(document, "entryId", "kind", "path", "schemaId", "contentHash", "offset", "length");
                string entry = Text(document, "entryId");
                Ordered(previousEntry, entry); previousEntry = entry;
                if (Text(document, "kind") is not ("firmware-family" or "composition-profile"))
                { throw new InvalidDataException("Invalid carried document kind."); }
                _ = Text(document, "path"); _ = Text(document, "schemaId");
                ValidateRange(document, "contentHash", body, ref offset, ref failure);
            }
            JsonElement manifest = bundle.GetProperty("manifest");
            Shape(manifest, "offset", "length", "sha256");
            ValidateRange(manifest, "sha256", body, ref offset, ref failure);
        }
        if (offset != body.Length) { throw new InvalidDataException("Prebuilt catalog has trailing body bytes."); }
    }

    private static void ValidateRange(JsonElement value, string hashKey, ReadOnlySpan<byte> body, ref long offset,
        ref PrebuiltProfileCatalogDecodeFailure failure)
    {
        long start = Number(value, "offset"), length = Number(value, "length");
        if (start != offset || length == 0 || start > body.Length || length > body.Length - start)
        { throw new InvalidDataException("Prebuilt catalog ranges are not contiguous and bounded."); }
        if (Digest(value, hashKey) != Hash(body.Slice((int)start, (int)length)))
        {
            failure = hashKey == "sha256" ? PrebuiltProfileCatalogDecodeFailure.ManifestIntegrity : PrebuiltProfileCatalogDecodeFailure.DocumentIntegrity;
            throw new InvalidDataException("Prebuilt catalog range integrity differs.");
        }
        offset = start + length;
    }

    private static void Shape(JsonElement value, params string[] properties)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(properties.Order(StringComparer.Ordinal)))
        { throw new InvalidDataException("Prebuilt catalog object shape is not closed."); }
    }

    private static string Text(JsonElement value, string key)
    {
        return value.GetProperty(key).ValueKind == JsonValueKind.String && value.GetProperty(key).GetString() is { Length: > 0 } text
            ? text : throw new InvalidDataException("Prebuilt catalog requires a nonempty string.");
    }

    private static long Number(JsonElement value, string key)
    {
        return value.GetProperty(key).TryGetInt64(out long number) && number >= 0
            ? number : throw new InvalidDataException("Prebuilt catalog requires a bounded nonnegative integer.");
    }

    private static string Digest(JsonElement value, string key)
    {
        string digest = Text(value, key);
        return digest.Length == 64 && digest.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? digest : throw new InvalidDataException("Prebuilt catalog requires canonical SHA-256.");
    }

    private static void Ordered(string? previous, string current)
    {
        if (previous is not null && StringComparer.Ordinal.Compare(previous, current) >= 0)
        { throw new InvalidDataException("Prebuilt catalog identities must be unique and ordinally ordered."); }
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static int AddLength(int current, ProfileBundleFileSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Length == 0 || snapshot.Length > PrebuiltProfileCatalogFormat.MaximumBodyBytes - current
            ? throw new InvalidDataException("Prebuilt catalog body is empty or exceeds the file byte budget.")
            : checked(current + snapshot.Length);
    }
}
