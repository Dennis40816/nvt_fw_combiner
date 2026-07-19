using System.Text.Json;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Parses one bounded UTF-8 JSON value after rejecting duplicate object keys.</summary>
internal static class StrictJsonDocumentReader
{
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    internal static JsonDocument Parse(
        ReadOnlyMemory<byte> utf8Json,
        int maximumBytes,
        int maximumDepth)
    {
        ValidateBounds(utf8Json, maximumBytes, maximumDepth);
        byte[] snapshot = utf8Json.ToArray();
        return ParseValidatedImmutableSnapshot(snapshot, maximumDepth);
    }

    /// <summary>
    /// Parses bytes already held by a private immutable snapshot without making another complete copy.
    /// The caller must keep the memory unchanged for the returned document's lifetime.
    /// </summary>
    internal static JsonDocument ParseOwnedSnapshot(
        ReadOnlyMemory<byte> utf8Json,
        int maximumBytes,
        int maximumDepth)
    {
        ValidateBounds(utf8Json, maximumBytes, maximumDepth);
        return ParseValidatedImmutableSnapshot(utf8Json, maximumDepth);
    }

    private static JsonDocument ParseValidatedImmutableSnapshot(
        ReadOnlyMemory<byte> utf8Json,
        int maximumDepth)
    {
        if (utf8Json.Span.StartsWith(Utf8Bom))
        {
            throw new JsonException("UTF-8 JSON input must not contain a byte-order mark.");
        }

        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = maximumDepth,
        };
        RejectDuplicateKeys(utf8Json.Span, readerOptions);
        return JsonDocument.Parse(utf8Json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = maximumDepth,
        });
    }

    private static void ValidateBounds(
        ReadOnlyMemory<byte> utf8Json,
        int maximumBytes,
        int maximumDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        if (utf8Json.Length > maximumBytes)
        {
            throw new JsonException($"JSON input exceeds the {maximumBytes}-byte limit.");
        }
    }

    private static void RejectDuplicateKeys(ReadOnlySpan<byte> utf8Json, JsonReaderOptions options)
    {
        var reader = new Utf8JsonReader(utf8Json, options);
        var objectKeys = new Stack<HashSet<string>?>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectKeys.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.StartArray:
                    objectKeys.Push(null);
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    _ = objectKeys.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    HashSet<string>? keys = objectKeys.Peek();
                    string key = reader.GetString() ?? throw new JsonException("JSON property name cannot be null.");
                    if (keys is null || !keys.Add(key))
                    {
                        throw new JsonException($"Duplicate JSON property '{key}'.");
                    }

                    break;
                case JsonTokenType.None:
                    break;
                case JsonTokenType.Comment:
                    break;
                case JsonTokenType.String:
                    break;
                case JsonTokenType.Number:
                    break;
                case JsonTokenType.True:
                    break;
                case JsonTokenType.False:
                    break;
                case JsonTokenType.Null:
                    break;
                default:
                    break;
            }
        }
    }
}
