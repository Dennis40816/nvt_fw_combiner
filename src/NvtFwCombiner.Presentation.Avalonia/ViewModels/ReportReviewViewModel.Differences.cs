using System.Globalization;
using NvtFwCombiner.Bootstrap;

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
                : T(language, "Expected changes", "預期變更");
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
                : T(language, "Every reported change belongs to a profile-approved section. Open a section to inspect its fields and evidence.", "所有回報差異都屬於 profile 核准的區段；展開區段可查看欄位與證據。");
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
            string.Create(CultureInfo.InvariantCulture, $"{accepted}/{outputDifferences.Count} expected"),
            string.Create(CultureInfo.InvariantCulture, $"{accepted}/{outputDifferences.Count} 預期"));
    }

    private static List<ReportDifferenceSummaryRowViewModel> CreateOutputDifferenceSummaryRows(
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        return
        [
            .. outputDifferences
                .GroupBy(difference => string.IsNullOrWhiteSpace(difference.SectionLabel)
                    ? T(language, "Unclassified changes", "未分類差異")
                    : difference.SectionLabel)
                .OrderBy(group => group.All(IsAcceptedOutputDifference))
                .Select(group =>
                {
                    int reviewCount = group.Count(IsReviewRequiredOutputDifference);
                    bool expected = reviewCount == 0;
                    return new ReportDifferenceSummaryRowViewModel(
                        group.Key,
                        group.Count().ToString(CultureInfo.InvariantCulture),
                        expected ? T(language, "expected", "預期") : T(language, "review", "待審查"),
                        expected
                            ? T(language, "profile-approved changes", "profile 核准的差異")
                            : T(language, "contains a difference that needs review", "包含需要審查的差異"));
                }),
        ];
    }

    private static string CreateAuditSummary(
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues,
        ShellLanguage language)
    {
        int commandCount = CountRuntimeInvocations(operations);
        return T(
            language,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{inputs.Count} input(s), {operations.Count} step(s), {commandCount} refresh command(s), {mutations.Count} changed range(s), {outputDifferences.Count} diff row(s), {issues.Count} issue row(s)"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{inputs.Count} 個輸入、{operations.Count} 個步驟、{commandCount} 個 refresh command、{mutations.Count} 個 changed range、{outputDifferences.Count} 筆 diff、{issues.Count} 筆問題"));
    }

    private static bool HasReviewRequiredOutputDifference(IEnumerable<ReportLineViewModel> outputDifferences)
    {
        return outputDifferences.Any(IsReviewRequiredOutputDifference);
    }

    private static bool IsReviewRequiredOutputDifference(ReportLineViewModel difference)
    {
        return !IsAcceptedOutputDifference(difference) ||
            string.Equals(difference.Classification, WorkbenchOutputDifferenceClassifications.Unexpected, StringComparison.Ordinal);
    }

    private static bool IsAcceptedOutputDifference(ReportLineViewModel difference)
    {
        return difference.IsAccepted &&
            !string.Equals(difference.Classification, WorkbenchOutputDifferenceClassifications.Unexpected, StringComparison.Ordinal);
    }

    private static bool IsReplaceComposition(string compositionKind)
    {
        return string.Equals(compositionKind, "Replace", StringComparison.OrdinalIgnoreCase);
    }
}
