using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static string CreateByteDifferenceTitle(
        string compositionKind,
        OutputDifferenceProjection outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count == 0
            ? IsReplaceComposition(compositionKind)
                ? T(language, "No output changes", "沒有輸出差異")
                : T(language, "No Replace diff check", "非 Replace diff 檢查")
            : outputDifferences.HasReviewRequired
                ? T(language, "Review required", "需要審查")
                : T(language, "Expected changes", "預期變更");
    }

    private static string CreateByteDifferenceDetail(
        string compositionKind,
        OutputDifferenceProjection outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count == 0
            ? IsReplaceComposition(compositionKind)
                ? T(language, "No final output changes were reported against the reference/base comparison scope.", "與 reference/base 比對範圍相比，沒有回報 final output 差異。")
                : T(language, "This workflow is not reference-based; use Evidence when you need operation-order details.", "此流程不是 reference-based；需要操作順序時請查看 Evidence。")
            : outputDifferences.HasReviewRequired
                ? T(language, "Unexpected or unaccepted changes are present. Review the detailed ranges before using the output.", "存在意外或未接受的變更；使用輸出前請先審查詳細範圍。")
                : T(language, "Every reported change belongs to a profile-approved section. Open a section to inspect its fields and evidence.", "所有回報差異都屬於 profile 核准的區段；展開區段可查看欄位與證據。");
    }

    private static string CreateByteDifferenceMeta(
        OutputDifferenceProjection outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count == 0
            ? T(language, "0 differences", "0 個差異")
            : T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"{outputDifferences.AcceptedCount}/{outputDifferences.Count} expected"),
                string.Create(CultureInfo.InvariantCulture, $"{outputDifferences.AcceptedCount}/{outputDifferences.Count} 預期"));
    }

    private static bool IsReplaceComposition(string compositionKind)
    {
        return string.Equals(compositionKind, "Replace", StringComparison.OrdinalIgnoreCase);
    }
}
