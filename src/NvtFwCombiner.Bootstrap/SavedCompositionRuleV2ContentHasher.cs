using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Calculates the path- and display-name-independent Saved Rule v2 semantic hash.</summary>
internal static class SavedCompositionRuleV2ContentHasher
{
    internal static string Calculate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Saved Rule v2 canonical hashing requires an object root.",
                nameof(root));
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
            SkipValidation = false,
        }))
        {
            WriteCanonical(root, writer, isRoot: true);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan))
            .ToLowerInvariant();
    }

    private static void WriteCanonical(
        JsonElement element,
        Utf8JsonWriter writer,
        bool isRoot = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element
                             .EnumerateObject()
                             .Where(property =>
                                 !isRoot ||
                                 !StringComparer.Ordinal.Equals(
                                     property.Name,
                                     "displayName"))
                             .OrderBy(static property =>
                                 property.Name,
                                 StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                WriteCanonicalNumber(element, writer);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            case JsonValueKind.Undefined:
                throw new ArgumentException(
                    $"Saved Rule v2 contains unsupported JSON kind '{element.ValueKind}'.",
                    nameof(element));
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(element),
                    element.ValueKind,
                    "Unknown JSON value kind.");
        }
    }

    private static void WriteCanonicalNumber(
        JsonElement element,
        Utf8JsonWriter writer)
    {
        if (element.TryGetInt64(out long signed))
        {
            writer.WriteNumberValue(signed);
            return;
        }

        if (element.TryGetUInt64(out ulong unsigned))
        {
            writer.WriteNumberValue(unsigned);
            return;
        }

        if (element.TryGetDecimal(out decimal exact))
        {
            writer.WriteRawValue(
                exact.ToString("G29", CultureInfo.InvariantCulture),
                skipInputValidation: false);
            return;
        }

        writer.WriteNumberValue(element.GetDouble());
    }
}
