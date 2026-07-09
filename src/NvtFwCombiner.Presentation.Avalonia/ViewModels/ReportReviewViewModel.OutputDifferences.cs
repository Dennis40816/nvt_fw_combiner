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
                    string sectionLabel = GetStringOrNull(difference, "SectionLabel") ??
                        FormatDifferenceSectionLabel(classification, language);
                    string reason = FormatDifferenceReason(classification, accepted, sectionLabel, language);
                    long changedByteCount = GetLong(difference, "ChangedByteCount");
                    return new ReportLineViewModel(
                        GetString(difference, "DifferenceId"),
                        range,
                        reason,
                        badges:
                        [
                            new ReportLineBadgeViewModel(accepted ? T(language, "accepted", "可接受") : T(language, "review", "待審查")),
                            new ReportLineBadgeViewModel(FormatDifferenceClassification(classification, language)),
                        ],
                        facts:
                        [
                            new ReportLineFactViewModel(T(language, "Reason", "原因"), reason),
                            new ReportLineFactViewModel(T(language, "Evidence id", "證據 ID"), GetString(difference, "Evidence"), isTechnical: true),
                        ],
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
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} accepted",
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} 可接受")
                        : T(
                            language,
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} accepted / {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} review",
                            $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} 可接受 / {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} 待審查");
                    bool hasSharedReason = rows
                        .Select(row => row.Reason)
                        .Distinct(StringComparer.Ordinal)
                        .Count() == 1 &&
                        rows.Select(IsAcceptedOutputDifference).Distinct().Count() == 1;
                    string reason = hasSharedReason
                        ? rows.FirstOrDefault(row => row.HasReason)?.Reason ?? string.Empty
                        : string.Empty;
                    return new ReportDifferenceGroupViewModel(group.Key, reason, status, rows, hasSharedReason, isAccepted);
                }),
            ];
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

    private static string CreateOutputDifferenceMeta(
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        ShellLanguage language)
    {
        if (outputDifferences.Count == 0)
        {
            return T(language, "same as base or non-Replace", "與 base 相同或非 Replace");
        }

        int acceptedCount = outputDifferences.Count(difference => difference.IsAccepted);
        int reviewCount = outputDifferences.Count - acceptedCount;
        return reviewCount == 0
            ? T(language, "all accepted", "全部可接受")
            : T(
                language,
                $"{acceptedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} accepted / {reviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} review",
                $"{acceptedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} 可接受 / {reviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} 待審查");
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
