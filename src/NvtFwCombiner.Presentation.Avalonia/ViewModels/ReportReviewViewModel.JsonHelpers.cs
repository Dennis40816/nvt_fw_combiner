using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportReviewViewModel
{
    private static string? GetRangeOrNull(JsonElement element, string propertyName)
    {
        return TryGetRange(element, propertyName) is { } range
            ? FormatRange(range)
            : null;
    }

    private static JsonElement? TryGetRange(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement range) && range.ValueKind == JsonValueKind.Object
            ? range
            : null;
    }

    private static string FormatRange(JsonElement range)
    {
        long start = GetLong(range, "Start");
        long end = GetLong(range, "EndExclusive");
        long length = GetLong(range, "Length");
        return string.Create(CultureInfo.InvariantCulture, $"0x{start:X}-0x{end - 1:X} (len 0x{length:X})");
    }

    private static string FormatRange(ByteRange range)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})");
    }

    private static string? GetStringOrNull(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return GetStringOrNull(element, propertyName) ?? string.Empty;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long number)
            ? number
            : 0;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }
}
