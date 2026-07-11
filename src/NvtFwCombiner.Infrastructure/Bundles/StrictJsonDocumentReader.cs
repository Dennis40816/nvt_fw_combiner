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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        if (utf8Json.Length > maximumBytes)
        {
            throw new JsonException($"JSON input exceeds the {maximumBytes}-byte limit.");
        }

        byte[] snapshot = utf8Json.ToArray();
        if (snapshot.AsSpan().StartsWith(Utf8Bom))
        {
            throw new JsonException("UTF-8 JSON input must not contain a byte-order mark.");
        }

        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = maximumDepth,
        };
        RejectDuplicateKeys(snapshot, readerOptions);
        return JsonDocument.Parse(snapshot, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = maximumDepth,
        });
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
