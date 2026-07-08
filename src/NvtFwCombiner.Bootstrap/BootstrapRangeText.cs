using System.Globalization;

namespace NvtFwCombiner.Bootstrap;

internal static class BootstrapRangeText
{
    internal static bool TryParseNonNegativeLong(string text, out long value)
    {
        value = 0;
        string trimmed = text.Trim();
        bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        return parsed && value >= 0;
    }

    internal static string FormatHex(long value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{value:X}");
    }
}
