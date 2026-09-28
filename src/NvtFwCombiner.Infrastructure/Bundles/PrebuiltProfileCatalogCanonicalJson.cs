using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>RFC 8785 subset for the container and build identity: strings and nonnegative integers only.</summary>
internal static class PrebuiltProfileCatalogCanonicalJson
{
    internal static byte[] Encode(JsonElement value)
    {
        var text = new StringBuilder();
        Append(value, text, 0);
        return new UTF8Encoding(false, true).GetBytes(text.ToString());
    }

    private static void Append(JsonElement value, StringBuilder text, int depth)
    {
        if (depth > 16) { throw new InvalidDataException("Canonical JSON exceeds depth 16."); }
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                _ = text.Append('{');
                bool first = true;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!keys.Add(property.Name)) { throw new InvalidDataException("Duplicate canonical JSON key."); }
                    if (!first) { _ = text.Append(','); }
                    first = false;
                    Quote(property.Name, text);
                    _ = text.Append(':');
                    Append(property.Value, text, depth + 1);
                }
                _ = text.Append('}');
                break;
            case JsonValueKind.Array:
                _ = text.Append('[');
                for (int i = 0; i < value.GetArrayLength(); i++)
                {
                    if (i != 0) { _ = text.Append(','); }
                    Append(value[i], text, depth + 1);
                }
                _ = text.Append(']');
                break;
            case JsonValueKind.String:
                Quote(value.GetString()!, text);
                break;
            case JsonValueKind.Number:
                string raw = value.GetRawText();
                if (raw.Length == 0 || raw.Any(c => c is < '0' or > '9') ||
                    (raw.Length > 1 && raw[0] == '0') || !value.TryGetInt64(out long number))
                { throw new InvalidDataException("Canonical JSON requires bounded nonnegative integers."); }
                _ = text.Append(number.ToString(CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                throw new InvalidDataException("Unsupported canonical JSON scalar.");
        }
    }

    private static void Quote(string value, StringBuilder text)
    {
        _ = text.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            _ = text.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\b' => "\\b",
                '\t' => "\\t",
                '\n' => "\\n",
                '\f' => "\\f",
                '\r' => "\\r",
                < (char)0x20 => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }
        _ = text.Append('"');
    }
}
