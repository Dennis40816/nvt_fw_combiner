using System.Text.Json;

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
                    string before = GetString(difference, "BeforeSha256");
                    string after = GetString(difference, "AfterSha256");
                    return new ReportLineViewModel(
                        GetString(difference, "DifferenceId"),
                        $"{GetRangeOrNull(difference, "Range")} changed={GetLong(difference, "ChangedByteCount")}",
                        GetString(difference, "Explanation"),
                        badges:
                        [
                            new ReportLineBadgeViewModel(accepted ? T(language, "accepted", "可接受") : T(language, "review", "待審查")),
                            new ReportLineBadgeViewModel(classification),
                        ],
                        facts:
                        [
                            new ReportLineFactViewModel(T(language, "Evidence", "證據"), GetString(difference, "Evidence"), isTechnical: true),
                            new ReportLineFactViewModel(T(language, "Before", "之前"), before, isTechnical: true),
                            new ReportLineFactViewModel(T(language, "After", "之後"), after, isTechnical: true),
                        ],
                        classification: classification,
                        isAccepted: accepted);
                }),
            ];
    }
}
