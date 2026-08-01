using System.Globalization;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Common human label projection for canonical metadata fields.</summary>
public static partial class FirmwareMetadataFieldDisplayName
{
    /// <summary>Formats one stable field id without introducing firmware geometry or authority.</summary>
    public static string Format(string fieldId, string? sourceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            return sourceName;
        }

        Match indexedHeader = IndexedHeaderField().Match(fieldId);
        if (indexedHeader.Success)
        {
            return $"{FormatWords(indexedHeader.Groups[2].Value)} {indexedHeader.Groups[1].Value}";
        }

        Match trailingIndex = TrailingIndexField().Match(fieldId);
        return trailingIndex.Success
            ? $"{FormatWords(trailingIndex.Groups[1].Value)} {trailingIndex.Groups[2].Value}"
            : fieldId.StartsWith("command-", StringComparison.Ordinal)
                ? $"Command header {LowercaseFirst(FormatWords(fieldId["command-".Length..]))}"
                : StringComparer.Ordinal.Equals(fieldId, "common-header-crc")
                    ? "Common Header CRC"
                    : FormatWords(fieldId);
    }

    private static string FormatWords(string fieldId)
    {
        string[] tokens = fieldId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        string text = string.Join(' ', tokens.Select(FormatToken));
        text = text.Replace("auto build", "auto-build", StringComparison.Ordinal);
        return UppercaseFirst(text);
    }

    private static string FormatToken(string token)
    {
        return token switch
        {
            "bin" => "BIN",
            "crc" => "CRC",
            "ctrlram" => "CtrlRAM",
            "diff" => "DIFF",
            "dlm" => "DLM",
            "fw" => "FW",
            "ic" => "IC",
            "ilm" => "ILM",
            "mp" => "MP",
            "ov" => "OV",
            "spi" => "SPI",
            "sram" => "SRAM",
            "svn" => "SVN",
            _ when token.Length > 1 && token[0] == 't' &&
                int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out _) =>
                token.ToUpperInvariant(),
            _ => token,
        };
    }

    private static string UppercaseFirst(string value)
    {
        return value.Length == 0 || char.IsUpper(value[0])
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string LowercaseFirst(string value)
    {
        return value.Length == 0 || char.IsLower(value[0])
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

    [GeneratedRegex("^header-(\\d+)-(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex IndexedHeaderField();

    [GeneratedRegex("^(.+)-(\\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingIndexField();
}
