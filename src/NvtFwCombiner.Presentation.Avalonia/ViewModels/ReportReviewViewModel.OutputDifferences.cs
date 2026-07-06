using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseOutputDifferences(JsonElement root)
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
                    string before = Shorten(GetString(difference, "BeforeSha256"), 10);
                    string after = Shorten(GetString(difference, "AfterSha256"), 10);
                    return new ReportLineViewModel(
                        GetString(difference, "DifferenceId"),
                        $"{GetRangeOrNull(difference, "Range")} changed={GetLong(difference, "ChangedByteCount")}",
                        GetString(difference, "Explanation"),
                        badges:
                        [
                            new ReportLineBadgeViewModel(accepted ? "accepted" : "review"),
                            new ReportLineBadgeViewModel(classification),
                        ],
                        facts:
                        [
                            new ReportLineFactViewModel("Evidence", GetString(difference, "Evidence"), isTechnical: true),
                            new ReportLineFactViewModel("Before", before, isTechnical: true),
                            new ReportLineFactViewModel("After", after, isTechnical: true),
                        ]);
                }),
            ];
    }
}
