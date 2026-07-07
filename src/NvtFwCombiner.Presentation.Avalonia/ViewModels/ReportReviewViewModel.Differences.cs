using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static string CreateByteDifferenceTitle(
        string compositionKind,
        IReadOnlyList<ReportLineViewModel> outputDifferences)
    {
        return outputDifferences.Count == 0
            ? IsReplaceComposition(compositionKind)
                ? "No output changes"
                : "No Replace diff check"
            : HasReviewRequiredOutputDifference(outputDifferences)
                ? "Review required"
                : "Accepted changes";
    }

    private static string CreateByteDifferenceDetail(
        string compositionKind,
        IReadOnlyList<ReportLineViewModel> outputDifferences)
    {
        return outputDifferences.Count == 0
            ? IsReplaceComposition(compositionKind)
                ? "No final output changes were reported against the reference/base comparison scope."
                : "This workflow is not reference-based; use Evidence when you need operation-order details."
            : HasReviewRequiredOutputDifference(outputDifferences)
                ? "Unexpected or unaccepted changes are present. Review the detailed ranges before using the output."
                : "Changes are limited to declared replacement and CRC/header refresh ranges.";
    }

    private static string CreateByteDifferenceMeta(IReadOnlyList<ReportLineViewModel> outputDifferences)
    {
        if (outputDifferences.Count == 0)
        {
            return "0 differences";
        }

        int accepted = outputDifferences.Count(IsAcceptedOutputDifference);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{accepted}/{outputDifferences.Count} accepted");
    }

    private static List<ReportDifferenceSummaryRowViewModel> CreateOutputDifferenceSummaryRows(
        IReadOnlyList<ReportLineViewModel> outputDifferences)
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
                "Declared replacements",
                declared,
                "profile-authorized copied ranges"),
            CreateDifferenceSummaryRow(
                "CRC/header refresh",
                refresh,
                "approved postbuild write ranges"),
            CreateDifferenceSummaryRow(
                "Unexpected differences",
                unexpected,
                "must be zero before release"),
        ];

        if (other > 0)
        {
            rows.Add(CreateDifferenceSummaryRow(
                "Other differences",
                other,
                "review the detailed classification"));
        }

        return rows;
    }

    private static ReportDifferenceSummaryRowViewModel CreateDifferenceSummaryRow(
        string label,
        int count,
        string detail)
    {
        return new ReportDifferenceSummaryRowViewModel(
            label,
            count.ToString(CultureInfo.InvariantCulture),
            count == 0 ? "none" : "present",
            detail);
    }

    private static string CreateAuditSummary(
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{inputs.Count} input(s), {operations.Count} step(s), {commandCount} refresh command(s), {mutations.Count} changed range(s), {outputDifferences.Count} diff row(s), {issues.Count} issue row(s)");
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
