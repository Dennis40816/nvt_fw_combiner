using System.Text.Json;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseOutputDifferences(
        JsonElement root,
        ShellLanguage language)
    {
        return !root.TryGetProperty(nameof(OutputDifferences), out JsonElement differences) ||
               differences.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. differences.EnumerateArray().Select(difference =>
                {
                    string classification = GetString(difference, "Classification");
                    bool accepted = GetBool(difference, "IsAccepted");
                    string beforeFullHex = GetStringOrNull(difference, "BeforeHex") ?? string.Empty;
                    string afterFullHex = GetStringOrNull(difference, "AfterHex") ?? string.Empty;
                    string beforePreviewHex = GetStringOrNull(difference, "BeforeHexPreview") ?? string.Empty;
                    string afterPreviewHex = GetStringOrNull(difference, "AfterHexPreview") ?? string.Empty;
                    bool hasFullHex = !string.IsNullOrWhiteSpace(beforeFullHex) || !string.IsNullOrWhiteSpace(afterFullHex);
                    bool isHexComplete = hasFullHex || GetBool(difference, "IsHexPreviewComplete");
                    long previewByteCount = GetLong(difference, "HexPreviewByteCount");
                    string beforeHex = hasFullHex ? beforeFullHex : beforePreviewHex;
                    string afterHex = hasFullHex ? afterFullHex : afterPreviewHex;
                    bool hasHex = !string.IsNullOrWhiteSpace(beforeHex) || !string.IsNullOrWhiteSpace(afterHex);
                    string before = hasHex ? FormatBytePreview(beforeHex) : GetString(difference, "BeforeSha256");
                    string after = hasHex ? FormatBytePreview(afterHex) : GetString(difference, "AfterSha256");
                    string range = GetRangeOrNull(difference, "Range") ?? string.Empty;
                    string semanticCategoryLabel = GetSemanticString(difference, "CategoryLabel");
                    string semanticParentLabel = GetSemanticString(difference, "ParentLabel");
                    string semanticSubjectLabel = GetSemanticString(difference, "SubjectLabel");
                    string semanticExplanation = GetSemanticString(difference, "Explanation");
                    string semanticSubjectId = GetSemanticString(difference, "SubjectId");
                    string sectionLabel = !string.IsNullOrWhiteSpace(semanticParentLabel)
                        ? semanticParentLabel
                        : GetStringOrNull(difference, "SectionLabel") ??
                        (!string.IsNullOrWhiteSpace(semanticCategoryLabel)
                            ? semanticCategoryLabel
                            : FormatDifferenceSectionLabel(classification, language));
                    string reason = !string.IsNullOrWhiteSpace(semanticExplanation)
                        ? semanticExplanation
                        : FormatDifferenceReason(classification, accepted, sectionLabel, language);
                    string title = !string.IsNullOrWhiteSpace(semanticSubjectLabel)
                        ? semanticSubjectLabel
                        : GetString(difference, "DifferenceId");
                    long changedByteCount = GetLong(difference, "ChangedByteCount");
                    return new ReportLineViewModel(
                        title,
                        reason,
                        GetString(difference, "DifferenceId"),
                        badges:
                        [
                            new ReportLineBadgeViewModel(accepted ? T(language, "expected", "預期") : T(language, "review", "待審查")),
                            new ReportLineBadgeViewModel(FormatDifferenceClassification(classification, language)),
                        ],
                        facts: CreateOutputDifferenceFacts(
                            language,
                            reason,
                            semanticSubjectId,
                            GetString(difference, "Evidence")),
                        classification: classification,
                        isAccepted: accepted,
                        range: range,
                        changedSummary: FormatChangedBytes(changedByteCount, language),
                        reason: reason,
                        sectionLabel: sectionLabel,
                        beforeLabel: hasHex
                            ? FormatByteValueLabel(isBefore: true, isComplete: isHexComplete, previewByteCount, language)
                            : T(language, "Before range hash", "變更前 range hash"),
                        beforeValue: before,
                        afterLabel: hasHex
                            ? FormatByteValueLabel(isBefore: false, isComplete: isHexComplete, previewByteCount, language)
                            : T(language, "After range hash", "變更後 range hash"),
                        afterValue: after);
                }),
            ];
    }

    private static string FormatDifferenceClassification(string classification, ShellLanguage language)
    {
        return classification switch
        {
            WorkbenchOutputDifferenceClassifications.DeclaredReplacement => T(language, "replacement", "替換"),
            WorkbenchOutputDifferenceClassifications.PostbuildCrcHeader => T(language, "CRC/header", "CRC/header"),
            WorkbenchOutputDifferenceClassifications.Unexpected => T(language, "unexpected", "意外"),
            _ => classification,
        };
    }

    private static string FormatByteValueLabel(
        bool isBefore,
        bool isComplete,
        long previewByteCount,
        ShellLanguage language)
    {
        if (isComplete)
        {
            return isBefore
                ? T(language, "Before bytes", "變更前 bytes")
                : T(language, "After bytes", "變更後 bytes");
        }

        string count = previewByteCount > 0
            ? previewByteCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "?";
        return isBefore
            ? T(language, $"Before preview, first {count} bytes", $"變更前 preview，前 {count} bytes")
            : T(language, $"After preview, first {count} bytes", $"變更後 preview，前 {count} bytes");
    }

    private static IReadOnlyList<ReportDifferenceGroupViewModel> CreateOutputDifferenceGroups(
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        return outputDifferences.Count == 0
            ? []
            :
            [
            .. outputDifferences
                .GroupBy(difference => string.IsNullOrWhiteSpace(difference.SectionLabel)
                    ? FormatDifferenceSectionLabel(difference.Classification, language)
                    : difference.SectionLabel)
                .Select(group =>
                {
                    ReportLineViewModel[] rows = [.. group];
                    int accepted = rows.Count(IsAcceptedOutputDifference);
                    int review = rows.Length - accepted;
                    bool isAccepted = review == 0;
                    string status = review == 0
                        ? T(
                            language,
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} expected",
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} 預期")
                        : T(
                            language,
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} expected / {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} review",
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} 預期 / {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} 待審查");
                    string detail = FormatDifferenceGroupDetail(rows.Length, accepted, review, language);
                    return new ReportDifferenceGroupViewModel(group.Key, detail, status, rows, isAccepted);
                }),
            ];
    }

    private static string FormatDifferenceGroupDetail(int count, int accepted, int review, ShellLanguage language)
    {
        return review == 0
            ? count == 1
                ? T(language, "1 expected field update", "1 筆預期欄位更新")
                : T(
                    language,
                    $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} expected field updates",
                    $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} 筆預期欄位更新")
            : T(
                language,
                $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} expected, {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} review required",
                $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} 筆預期，{review.ToString(System.Globalization.CultureInfo.InvariantCulture)} 筆需審查");
    }

    private static string FormatDifferenceSectionLabel(string classification, ShellLanguage language)
    {
        return classification switch
        {
            WorkbenchOutputDifferenceClassifications.DeclaredReplacement => T(language, "Declared replacement", "宣告替換區段"),
            WorkbenchOutputDifferenceClassifications.PostbuildCrcHeader => T(language, "Header / CRC refresh", "Header / CRC refresh"),
            WorkbenchOutputDifferenceClassifications.Unexpected => T(language, "Unexpected range", "非預期區段"),
            _ => classification,
        };
    }

    private static string FormatDifferenceReason(
        string classification,
        bool accepted,
        string sectionLabel,
        ShellLanguage language)
    {
        return !accepted
            ? T(language, "Not accepted by the selected profile; review before release.", "所選 profile 未接受此差異；release 前必須審查。")
            : classification switch
            {
                WorkbenchOutputDifferenceClassifications.DeclaredReplacement => T(language, "Expected replacement bytes copied by this run.", "本次執行預期複製的 replacement bytes。"),
                WorkbenchOutputDifferenceClassifications.PostbuildCrcHeader => T(
                    language,
                    $"Expected {NormalizePostbuildSectionForReason(sectionLabel)} update written by postbuild.",
                    $"Postbuild 預期更新 {NormalizePostbuildSectionForReason(sectionLabel)}。"),
                _ => T(language, "Accepted by report policy.", "Report policy 判定可接受。"),
            };
    }

    private static string NormalizePostbuildSectionForReason(string sectionLabel)
    {
        return string.IsNullOrWhiteSpace(sectionLabel) ? "CRC/header" : sectionLabel;
    }

    private static string FormatChangedBytes(long changedByteCount, ShellLanguage language)
    {
        return changedByteCount == 1
            ? T(language, "1 byte changed", "1 byte 變更")
            : T(
                language,
                $"{changedByteCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes changed",
                $"{changedByteCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes 變更");
    }

    private static string GetSemanticString(JsonElement difference, string propertyName)
    {
        return difference.TryGetProperty("Semantic", out JsonElement semantic) &&
               semantic.ValueKind == JsonValueKind.Object
            ? GetStringOrNull(semantic, propertyName) ?? string.Empty
            : string.Empty;
    }

    private static List<ReportLineFactViewModel> CreateOutputDifferenceFacts(
        ShellLanguage language,
        string reason,
        string semanticSubjectId,
        string evidence)
    {
        List<ReportLineFactViewModel> facts =
        [
            new ReportLineFactViewModel(T(language, "Reason", "原因"), reason),
        ];
        if (!string.IsNullOrWhiteSpace(semanticSubjectId))
        {
            facts.Add(new ReportLineFactViewModel(T(language, "Subject id", "欄位 ID"), semanticSubjectId, isTechnical: true));
        }

        facts.Add(new ReportLineFactViewModel(T(language, "Evidence id", "證據 ID"), evidence, isTechnical: true));
        return facts;
    }

    private static string FormatBytePreview(string hex)
    {
        string compact = hex.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        if (compact.Length == 0)
        {
            return string.Empty;
        }

        if (compact.Length % 2 != 0)
        {
            return compact.ToUpperInvariant();
        }

        List<string> bytes = [];
        for (int index = 0; index < compact.Length; index += 2)
        {
            bytes.Add(compact.Substring(index, 2).ToUpperInvariant());
        }

        return string.Join(" ", bytes);
    }
}
