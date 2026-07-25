using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        VerifyHash(bytes, expectedSha256, catalogName);

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

    /// <summary>Loads one hash-pinned catalog through generated JSON metadata.</summary>
    internal static T Load<T>(
        ReadOnlySpan<byte> bytes,
        string expectedSha256,
        string catalogName,
        string emptyMessage,
        JsonTypeInfo<T> typeInfo)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        VerifyHash(bytes, expectedSha256, catalogName);
        try
        {
            return JsonSerializer.Deserialize(bytes, typeInfo) ??
                throw new InvalidDataException(emptyMessage);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{catalogName} JSON is invalid.", exception);
        }
    }

    private static void VerifyHash(
        ReadOnlySpan<byte> bytes,
        string expectedSha256,
        string catalogName)
    {
        string actualHash = ComputeCanonicalSha256(bytes);
        if (!StringComparer.Ordinal.Equals(actualHash, expectedSha256))
        {
            throw new InvalidDataException($"{catalogName} hash mismatch: {actualHash}.");
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
