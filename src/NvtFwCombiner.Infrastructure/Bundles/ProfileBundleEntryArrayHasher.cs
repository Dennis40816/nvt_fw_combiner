using System.Buffers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Hashes the schema-validated bundle entry array using its RFC 8785 projection.</summary>
internal static class ProfileBundleEntryArrayHasher
{
    internal static string CalculateContentHash(IEnumerable<ProfileBundleEntryDocument> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ProfileBundleEntryDocument[] sorted = [.. entries];
        if (sorted.Any(static entry => entry is null))
        {
            throw new ArgumentException("Bundle hash entries cannot contain null.", nameof(entries));
        }

        Array.Sort(sorted, CompareEntries);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
            SkipValidation = false,
        }))
        {
            writer.WriteStartArray();
            foreach (ProfileBundleEntryDocument entry in sorted)
            {
                writer.WriteStartObject();
                writer.WriteString("contentHash", RequireValue(entry.ContentHash, nameof(entry.ContentHash)));
                writer.WriteString("entryId", RequireValue(entry.EntryId, nameof(entry.EntryId)));
                writer.WriteString("kind", RequireValue(entry.Kind, nameof(entry.Kind)));
                writer.WriteString("path", RequireValue(entry.Path, nameof(entry.Path)));
                writer.WriteString("schemaId", RequireValue(entry.SchemaId, nameof(entry.SchemaId)));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
    }

    private static int CompareEntries(ProfileBundleEntryDocument left, ProfileBundleEntryDocument right)
    {
        int comparison = StringComparer.Ordinal.Compare(left.EntryId, right.EntryId);
        if (comparison == 0)
        {
            comparison = StringComparer.Ordinal.Compare(left.Kind, right.Kind);
        }

        if (comparison == 0)
        {
            comparison = StringComparer.Ordinal.Compare(left.Path, right.Path);
        }

        if (comparison == 0)
        {
            comparison = StringComparer.Ordinal.Compare(left.SchemaId, right.SchemaId);
        }

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(left.ContentHash, right.ContentHash);
    }

    private static string RequireValue(string? value, string name)
    {
        return value ?? throw new ArgumentException("Bundle hash fields cannot be null.", name);
    }
}
