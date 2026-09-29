using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Localized wording for the inspector's typed nonstandard DP length advisory.</summary>
internal static class DpSizeWarningText
{
    internal static (string Title, string Detail) Get(ShellLanguage language,
        long? actualLength, IReadOnlyList<long>? expectedOuterLengths)
    {
        string actual = actualLength?.ToString("N0", CultureInfo.CurrentCulture) ?? string.Empty;
        if (expectedOuterLengths?.Count is not > 0)
        {
            string title = language == ShellLanguage.ChineseTraditional
                ? actualLength is null ? "DP BIN 大小與此 IC 的預期大小不同" : $"DP BIN 大小 {actual} bytes 與此 IC 的預期大小不同"
                : actualLength is null ? "DP BIN size differs from the expected size for this IC" : $"DP BIN size {actual} bytes differs from the expected size for this IC";
            return (title, language == ShellLanguage.ChineseTraditional
                ? $"{title}；可能是客製的 OSD 應用。請確認選擇的 BIN。"
                : $"{title}. This may be a customized OSD application. Confirm the selected BIN.");
        }
        string expected = string.Join(" / ", expectedOuterLengths.Select(length =>
            length.ToString("N0", CultureInfo.CurrentCulture)));
        string summary = language == ShellLanguage.ChineseTraditional
            ? actualLength is null ? $"DP BIN 預期 {expected} bytes" : $"DP BIN 大小 {actual} bytes，預期 {expected} bytes"
            : actualLength is null ? $"DP BIN expected {expected} bytes" : $"DP BIN size is {actual} bytes; expected {expected} bytes";
        return (summary, language == ShellLanguage.ChineseTraditional
            ? $"{summary}；可能是客製的 OSD 應用。請確認選擇的 BIN。"
            : $"{summary}. This may be a customized OSD application. Confirm the selected BIN.");
    }
}
