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
        byte[] canonicalBytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n"));
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(canonicalBytes));
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
}
