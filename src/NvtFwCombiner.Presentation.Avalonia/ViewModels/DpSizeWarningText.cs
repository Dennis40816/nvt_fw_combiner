using System.Globalization;
using NvtFwCombiner.Application.InputInspection;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Localized wording for the inspector's typed nonstandard DP length advisory.</summary>
internal static class DpSizeWarningText
{
    internal static (string Title, string Detail) Get(ShellLanguage language,
        CompiledInputArtifactInspectionResult? inspection)
    {
        string title = language == ShellLanguage.ChineseTraditional
            ? "DP 大小與標準容量不同" : "DP size differs from standard capacity";
        if (inspection?.ExpectedOuterLengths.Count is not > 0)
        {
            return (title, language == ShellLanguage.ChineseTraditional
                ? "可能是客製的 OSD 應用。請確認選擇的 BIN。"
                : "This may be a customized OSD application. Confirm the selected BIN.");
        }
        string actual = inspection.ActualLength.ToString("N0", CultureInfo.InvariantCulture);
        string expected = string.Join(" / ", inspection.ExpectedOuterLengths.Select(length =>
            length.ToString("N0", CultureInfo.InvariantCulture)));
        return (title, language == ShellLanguage.ChineseTraditional
            ? $"DP BIN 大小 {actual} bytes，預期 {expected} bytes；可能是客製的 OSD 應用。請確認選擇的 BIN。"
            : $"DP BIN size is {actual} bytes; expected {expected} bytes. This may be a customized OSD application. Confirm the selected BIN.");
    }
}
