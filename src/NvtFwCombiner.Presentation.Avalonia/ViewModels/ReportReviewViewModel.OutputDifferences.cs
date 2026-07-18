using System.Text;
using System.Text.Json;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private const int MaximumRenderedHexPreviewBytes = 64;

    private static OutputDifferenceProjection ParseOutputDifferences(
        JsonElement root,
        byte[] reportUtf8,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty(nameof(OutputDifferences), out JsonElement differences) ||
            differences.ValueKind != JsonValueKind.Array)
        {
            return OutputDifferenceProjection.Empty;
        }

        int count = differences.GetArrayLength();
        if (count == 0)
        {
            return OutputDifferenceProjection.Empty;
        }

        JsonValueSlice[] slices = IndexOutputDifferences(reportUtf8, cancellationToken);
        if (slices.Length != count)
        {
            throw new JsonException("OutputDifferences JSON indexing did not preserve every report entry.");
        }

        var rows = new MemoizedIndexedReadOnlyList<ReportLineViewModel>(
            count,
            index => ParseOutputDifference(reportUtf8, slices[index], language));
        var groupBySection = new Dictionary<string, DifferenceGroupBuilder>(StringComparer.Ordinal);
        var groupOrder = new List<DifferenceGroupBuilder>();
        var summaryBySection = new Dictionary<string, DifferenceSummaryBuilder>(StringComparer.Ordinal);
        var summaryOrder = new List<DifferenceSummaryBuilder>();
        int acceptedCount = 0;
        int index = 0;
        foreach (JsonElement difference in differences.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string classification = GetString(difference, "Classification");
            string sectionLabel = GetOutputDifferenceSectionLabel(difference, classification, language);
            string groupLabel = string.IsNullOrWhiteSpace(sectionLabel)
                ? FormatDifferenceSectionLabel(classification, language)
                : sectionLabel;
            if (!groupBySection.TryGetValue(groupLabel, out DifferenceGroupBuilder? group))
            {
                group = new DifferenceGroupBuilder(groupLabel);
                groupBySection.Add(groupLabel, group);
                groupOrder.Add(group);
            }

            string summaryLabel = string.IsNullOrWhiteSpace(sectionLabel)
                ? T(language, "Unclassified changes", "未分類差異")
                : sectionLabel;
            if (!summaryBySection.TryGetValue(summaryLabel, out DifferenceSummaryBuilder? summary))
            {
                summary = new DifferenceSummaryBuilder(summaryLabel);
                summaryBySection.Add(summaryLabel, summary);
                summaryOrder.Add(summary);
            }

            bool isAccepted = IsAcceptedOutputDifference(difference, classification);
            if (isAccepted)
            {
                group.AcceptedIndices.Add(index);
                summary.AcceptedCount++;
                acceptedCount++;
            }
            else
            {
                group.ReviewIndices.Add(index);
                summary.ReviewCount++;
            }

            index++;
        }

        DifferenceGroupBuilder[] orderedGroups = [.. groupOrder.OrderBy(static group => group.IsAccepted)];
        var groups = new List<ReportDifferenceGroupViewModel>(orderedGroups.Length);
        foreach (DifferenceGroupBuilder group in orderedGroups)
        {
            int accepted = group.AcceptedIndices.Count;
            int review = group.ReviewIndices.Count;
            int groupCount = checked(accepted + review);
            int[] sourceIndices = [.. group.ReviewIndices, .. group.AcceptedIndices];
            string status = review == 0
                ? T(
                    language,
                    $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{groupCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} expected",
                    $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{groupCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} 預期")
                : T(
                    language,
                    $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} expected / {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} review",
                    $"{accepted.ToString(System.Globalization.CultureInfo.InvariantCulture)} 預期 / {review.ToString(System.Globalization.CultureInfo.InvariantCulture)} 待審查");
            groups.Add(new ReportDifferenceGroupViewModel(
                group.SectionLabel,
                FormatDifferenceGroupDetail(groupCount, accepted, review, language),
                status,
                new IndexedReadOnlyList<ReportLineViewModel>(rows, sourceIndices),
                group.IsAccepted,
                language));
        }

        DifferenceSummaryBuilder[] orderedSummaries = [.. summaryOrder.OrderBy(static summary => summary.IsAccepted)];
        var summaryRows = new List<ReportDifferenceSummaryRowViewModel>(orderedSummaries.Length);
        foreach (DifferenceSummaryBuilder summary in orderedSummaries)
        {
            int summaryCount = checked(summary.AcceptedCount + summary.ReviewCount);
            summaryRows.Add(new ReportDifferenceSummaryRowViewModel(
                summary.SectionLabel,
                summaryCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                summary.IsAccepted ? T(language, "expected", "預期") : T(language, "review", "待審查"),
                summary.IsAccepted
                    ? T(language, "profile-approved changes", "profile 核准的差異")
                    : T(language, "contains a difference that needs review", "包含需要審查的差異")));
        }

        return new OutputDifferenceProjection(rows, groups, summaryRows, acceptedCount);
    }

    private static ReportLineViewModel ParseOutputDifference(
        ReadOnlyMemory<byte> reportUtf8,
        JsonValueSlice slice,
        ShellLanguage language)
    {
        using JsonDocument document = JsonDocument.Parse(reportUtf8.Slice(slice.Start, slice.Length));
        return ParseOutputDifference(document.RootElement, language);
    }

    private static ReportLineViewModel ParseOutputDifference(JsonElement difference, ShellLanguage language)
    {
        string classification = GetString(difference, "Classification");
        bool accepted = GetBool(difference, "IsAccepted");
        string beforeFullHex = GetStringOrNull(difference, "BeforeHex") ?? string.Empty;
        string afterFullHex = GetStringOrNull(difference, "AfterHex") ?? string.Empty;
        string beforePreviewHex = GetStringOrNull(difference, "BeforeHexPreview") ?? string.Empty;
        string afterPreviewHex = GetStringOrNull(difference, "AfterHexPreview") ?? string.Empty;
        bool hasFullHex = !string.IsNullOrWhiteSpace(beforeFullHex) || !string.IsNullOrWhiteSpace(afterFullHex);
        long previewByteCount = GetLong(difference, "HexPreviewByteCount");
        bool useBeforeFullHex = string.IsNullOrWhiteSpace(beforePreviewHex) && !string.IsNullOrWhiteSpace(beforeFullHex);
        bool useAfterFullHex = string.IsNullOrWhiteSpace(afterPreviewHex) && !string.IsNullOrWhiteSpace(afterFullHex);
        bool usesContractHexPreview = !string.IsNullOrWhiteSpace(beforePreviewHex) ||
            !string.IsNullOrWhiteSpace(afterPreviewHex);
        string beforeHex = useBeforeFullHex ? beforeFullHex : beforePreviewHex;
        string afterHex = useAfterFullHex ? afterFullHex : afterPreviewHex;
        bool hasHex = !string.IsNullOrWhiteSpace(beforeHex) || !string.IsNullOrWhiteSpace(afterHex);
        bool contractHexComplete = GetBool(difference, "IsHexPreviewComplete") ||
            (hasFullHex &&
             (string.IsNullOrWhiteSpace(beforeHex) || useBeforeFullHex) &&
             (string.IsNullOrWhiteSpace(afterHex) || useAfterFullHex));
        int displayLimit = usesContractHexPreview && !contractHexComplete && previewByteCount > 0
            ? (int)Math.Min(previewByteCount, MaximumRenderedHexPreviewBytes)
            : MaximumRenderedHexPreviewBytes;
        FormattedHexPreview beforePreview = FormatBytePreview(beforeHex, displayLimit);
        FormattedHexPreview afterPreview = FormatBytePreview(afterHex, displayLimit);
        bool isHexComplete = contractHexComplete && beforePreview.IsComplete && afterPreview.IsComplete;
        long displayedByteCount = isHexComplete
            ? Math.Max(beforePreview.ByteCount, afterPreview.ByteCount)
            : usesContractHexPreview && previewByteCount > 0
                ? Math.Min(previewByteCount, MaximumRenderedHexPreviewBytes)
                : Math.Max(beforePreview.ByteCount, afterPreview.ByteCount);
        string before = hasHex ? beforePreview.Value : GetString(difference, "BeforeSha256");
        string after = hasHex ? afterPreview.Value : GetString(difference, "AfterSha256");
        string semanticSubjectLabel = GetSemanticString(difference, "SubjectLabel");
        string semanticExplanation = GetSemanticString(difference, "Explanation");
        string semanticSubjectId = GetSemanticString(difference, "SubjectId");
        string sectionLabel = GetOutputDifferenceSectionLabel(difference, classification, language);
        string reason = !string.IsNullOrWhiteSpace(semanticExplanation)
            ? semanticExplanation
            : FormatDifferenceReason(classification, accepted, sectionLabel, language);
        string title = !string.IsNullOrWhiteSpace(semanticSubjectLabel)
            ? semanticSubjectLabel
            : GetString(difference, "DifferenceId");
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
            range: GetRangeOrNull(difference, "Range") ?? string.Empty,
            changedSummary: FormatChangedBytes(GetLong(difference, "ChangedByteCount"), language),
            reason: reason,
            sectionLabel: sectionLabel,
            beforeLabel: hasHex
                ? FormatByteValueLabel(isBefore: true, isComplete: isHexComplete, displayedByteCount, language)
                : T(language, "Before range hash", "變更前 range hash"),
            beforeValue: before,
            afterLabel: hasHex
                ? FormatByteValueLabel(isBefore: false, isComplete: isHexComplete, displayedByteCount, language)
                : T(language, "After range hash", "變更後 range hash"),
            afterValue: after);
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

    private static string GetOutputDifferenceSectionLabel(
        JsonElement difference,
        string classification,
        ShellLanguage language)
    {
        string semanticParentLabel = GetSemanticString(difference, "ParentLabel");
        if (!string.IsNullOrWhiteSpace(semanticParentLabel))
        {
            return semanticParentLabel;
        }

        string semanticCategoryLabel = GetSemanticString(difference, "CategoryLabel");
        return GetStringOrNull(difference, "SectionLabel") ??
            (!string.IsNullOrWhiteSpace(semanticCategoryLabel)
                ? semanticCategoryLabel
                : FormatDifferenceSectionLabel(classification, language));
    }

    private static bool IsAcceptedOutputDifference(JsonElement difference, string classification)
    {
        return GetBool(difference, "IsAccepted") &&
            !string.Equals(
                classification,
                WorkbenchOutputDifferenceClassifications.Unexpected,
                StringComparison.Ordinal);
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

    private static FormattedHexPreview FormatBytePreview(string hex, int maximumBytes)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return new FormattedHexPreview(string.Empty, 0, IsComplete: true);
        }

        var builder = new StringBuilder(Math.Min(hex.Length, checked(maximumBytes * 3)));
        char? highNibble = null;
        int byteCount = 0;
        foreach (char value in hex)
        {
            if (char.IsWhiteSpace(value))
            {
                continue;
            }

            if (highNibble is null)
            {
                highNibble = char.ToUpperInvariant(value);
                continue;
            }

            if (byteCount >= maximumBytes)
            {
                return new FormattedHexPreview(builder.ToString(), byteCount, IsComplete: false);
            }

            if (builder.Length > 0)
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(highNibble.Value).Append(char.ToUpperInvariant(value));
            highNibble = null;
            byteCount++;
        }

        if (highNibble is not null)
        {
            if (builder.Length > 0)
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(highNibble.Value);
        }

        return new FormattedHexPreview(builder.ToString(), byteCount, IsComplete: true);
    }

    private sealed class OutputDifferenceProjection
    {
        internal static OutputDifferenceProjection Empty { get; } = new(
            new MemoizedIndexedReadOnlyList<ReportLineViewModel>(
                0,
                static _ => throw new InvalidOperationException("An empty projection has no report rows.")),
            [],
            [],
            acceptedCount: 0);

        internal OutputDifferenceProjection(
            MemoizedIndexedReadOnlyList<ReportLineViewModel> rows,
            IReadOnlyList<ReportDifferenceGroupViewModel> groups,
            IReadOnlyList<ReportDifferenceSummaryRowViewModel> summaryRows,
            int acceptedCount)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(groups);
            ArgumentNullException.ThrowIfNull(summaryRows);
            ArgumentOutOfRangeException.ThrowIfNegative(acceptedCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(acceptedCount, rows.Count);

            Rows = rows;
            Groups = groups;
            SummaryRows = summaryRows;
            AcceptedCount = acceptedCount;
        }

        internal MemoizedIndexedReadOnlyList<ReportLineViewModel> Rows { get; }

        internal IReadOnlyList<ReportDifferenceGroupViewModel> Groups { get; }

        internal IReadOnlyList<ReportDifferenceSummaryRowViewModel> SummaryRows { get; }

        internal int Count => Rows.Count;

        internal int AcceptedCount { get; }

        internal bool HasReviewRequired => AcceptedCount != Count;

        internal int MaterializedCount => Rows.MaterializedCount;
    }

    private sealed class DifferenceGroupBuilder(string sectionLabel)
    {
        internal string SectionLabel { get; } = sectionLabel;

        internal List<int> AcceptedIndices { get; } = [];

        internal List<int> ReviewIndices { get; } = [];

        internal bool IsAccepted => ReviewIndices.Count == 0;
    }

    private sealed class DifferenceSummaryBuilder(string sectionLabel)
    {
        internal string SectionLabel { get; } = sectionLabel;

        internal int AcceptedCount { get; set; }

        internal int ReviewCount { get; set; }

        internal bool IsAccepted => ReviewCount == 0;
    }

    private readonly record struct FormattedHexPreview(string Value, int ByteCount, bool IsComplete);
}
