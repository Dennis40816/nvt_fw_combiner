using System.Buffers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Hashes the closed candidate-root entry array using its RFC 8785 projection.</summary>
internal static class CandidateEvidenceEntryArrayHasher
{
    internal static string CalculateContentHash(IEnumerable<CandidateEvidenceEntryHashInput> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        CandidateEvidenceEntryHashInput[] sorted = [.. entries];
        if (sorted.Any(static entry => entry is null))
        {
            throw new ArgumentException("Candidate entry hash inputs cannot contain null.", nameof(entries));
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
            foreach (CandidateEvidenceEntryHashInput entry in sorted)
            {
                writer.WriteStartObject();
                writer.WriteString("contentHash", RequireValue(entry.ContentHash, nameof(entry.ContentHash)));
                writer.WriteString("entryId", RequireValue(entry.EntryId, nameof(entry.EntryId)));
                writer.WriteString("kind", RequireValue(entry.Kind, nameof(entry.Kind)));
                writer.WriteString("path", RequireValue(entry.Path, nameof(entry.Path)));
                writer.WriteNumber("sizeBytes", entry.SizeBytes);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
    }

    private static int CompareEntries(CandidateEvidenceEntryHashInput left, CandidateEvidenceEntryHashInput right)
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
            comparison = StringComparer.Ordinal.Compare(left.ContentHash, right.ContentHash);
        }

        return comparison != 0 ? comparison : left.SizeBytes.CompareTo(right.SizeBytes);
    }

    private static string RequireValue(string? value, string name)
    {
        return value ?? throw new ArgumentException("Candidate entry hash fields cannot be null.", name);
    }
}

/// <summary>One candidate-root entry projected into the canonical content hash.</summary>
internal sealed record CandidateEvidenceEntryHashInput(
    string EntryId,
    string Kind,
    string Path,
    string ContentHash,
    int SizeBytes);
