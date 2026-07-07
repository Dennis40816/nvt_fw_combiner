using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static string CreateByteDifferenceTitle(
        string compositionKind,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count == 0
            ? IsReplaceComposition(compositionKind)
                ? T(language, "No output changes", "沒有輸出差異")
                : T(language, "No Replace diff check", "非 Replace diff 檢查")
            : HasReviewRequiredOutputDifference(outputDifferences)
                ? T(language, "Review required", "需要審查")
                : T(language, "Accepted changes", "可接受變更");
    }

    private static string CreateByteDifferenceDetail(
        string compositionKind,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count == 0
            ? IsReplaceComposition(compositionKind)
                ? T(language, "No final output changes were reported against the reference/base comparison scope.", "與 reference/base 比對範圍相比，沒有回報 final output 差異。")
                : T(language, "This workflow is not reference-based; use Evidence when you need operation-order details.", "此流程不是 reference-based；需要操作順序時請查看 Evidence。")
            : HasReviewRequiredOutputDifference(outputDifferences)
                ? T(language, "Unexpected or unaccepted changes are present. Review the detailed ranges before using the output.", "存在意外或未接受的變更；使用輸出前請先審查詳細範圍。")
                : T(language, "Changes are limited to declared replacement and CRC/header refresh ranges.", "差異只落在已宣告 replacement 與 CRC/header refresh 範圍內。");
    }

    private static string CreateByteDifferenceMeta(
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        if (outputDifferences.Count == 0)
        {
            return T(language, "0 differences", "0 個差異");
        }

        int accepted = outputDifferences.Count(IsAcceptedOutputDifference);
        return T(
            language,
            string.Create(CultureInfo.InvariantCulture, $"{accepted}/{outputDifferences.Count} accepted"),
            string.Create(CultureInfo.InvariantCulture, $"{accepted}/{outputDifferences.Count} 可接受"));
    }

    private static List<ReportDifferenceSummaryRowViewModel> CreateOutputDifferenceSummaryRows(
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        int declared = CountDifference(outputDifferences, "DeclaredReplacement");
        int refresh = CountDifference(outputDifferences, "PostbuildCrcHeader");
        int unexpected = outputDifferences.Count(IsReviewRequiredOutputDifference);
        int known = declared + refresh + outputDifferences.Count(difference =>
            string.Equals(difference.Classification, "Unexpected", StringComparison.Ordinal));
        int other = Math.Max(0, outputDifferences.Count - known);

        List<ReportDifferenceSummaryRowViewModel> rows =
        [
            CreateDifferenceSummaryRow(
                T(language, "Declared replacements", "宣告 replacement"),
                declared,
                T(language, "profile-authorized copied ranges", "profile 核准的 copy 範圍"),
                language),
            CreateDifferenceSummaryRow(
                "CRC/header refresh",
                refresh,
                T(language, "approved postbuild write ranges", "核准的 postbuild 寫入範圍"),
                language),
            CreateDifferenceSummaryRow(
                T(language, "Unexpected differences", "意外差異"),
                unexpected,
                T(language, "must be zero before release", "release 前必須為 0"),
                language),
        ];

        if (other > 0)
        {
            rows.Add(CreateDifferenceSummaryRow(
                T(language, "Other differences", "其他差異"),
                other,
                T(language, "review the detailed classification", "請審查詳細分類"),
                language));
        }

        return rows;
    }

    private static ReportDifferenceSummaryRowViewModel CreateDifferenceSummaryRow(
        string label,
        int count,
        string detail,
        ShellLanguage language)
    {
        return new ReportDifferenceSummaryRowViewModel(
            label,
            count.ToString(CultureInfo.InvariantCulture),
            count == 0 ? T(language, "none", "無") : T(language, "present", "存在"),
            detail);
    }

    private static string CreateAuditSummary(
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        return T(
            language,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{inputs.Count} input(s), {operations.Count} step(s), {commandCount} refresh command(s), {mutations.Count} changed range(s), {outputDifferences.Count} diff row(s), {issues.Count} issue row(s)"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{inputs.Count} 個輸入、{operations.Count} 個步驟、{commandCount} 個 refresh command、{mutations.Count} 個 changed range、{outputDifferences.Count} 筆 diff、{issues.Count} 筆問題"));
    }

    private static int CountDifference(
        IEnumerable<ReportLineViewModel> outputDifferences,
        string classification)
    {
        return outputDifferences.Count(difference =>
            string.Equals(difference.Classification, classification, StringComparison.Ordinal));
    }

    private static bool HasReviewRequiredOutputDifference(IEnumerable<ReportLineViewModel> outputDifferences)
    {
        return outputDifferences.Any(IsReviewRequiredOutputDifference);
    }

    private static bool IsReviewRequiredOutputDifference(ReportLineViewModel difference)
    {
        return !IsAcceptedOutputDifference(difference) ||
            string.Equals(difference.Classification, "Unexpected", StringComparison.Ordinal);
    }

    private static bool IsAcceptedOutputDifference(ReportLineViewModel difference)
    {
        return difference.IsAccepted &&
            !string.Equals(difference.Classification, "Unexpected", StringComparison.Ordinal);
    }

    private static bool IsReplaceComposition(string compositionKind)
    {
        return string.Equals(compositionKind, "Replace", StringComparison.OrdinalIgnoreCase);
    }
}
