using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Infrastructure;

internal static class PinnedJsonCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static T Load<T>(
        ReadOnlySpan<byte> bytes,
        string expectedSha256,
        string catalogName,
        string emptyMessage)
        where T : class
    {
        string actualHash = ComputeCanonicalSha256(bytes);
        if (!StringComparer.Ordinal.Equals(actualHash, expectedSha256))
        {
            throw new InvalidDataException($"{catalogName} hash mismatch: {actualHash}.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions) ??
                throw new InvalidDataException(emptyMessage);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{catalogName} JSON is invalid.", exception);
        }
    }

    internal static string ComputeCanonicalSha256(ReadOnlySpan<byte> bytes)
    {
        byte[]? normalizedBytes = null;
        ReadOnlySpan<byte> canonicalBytes = bytes;
        if (!Ascii.IsValid(bytes) || bytes.IndexOfAny((byte)'\r', (byte)'\f') >= 0)
        {
            normalizedBytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n"));
            canonicalBytes = normalizedBytes;
        }

        Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(canonicalBytes, hashBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
