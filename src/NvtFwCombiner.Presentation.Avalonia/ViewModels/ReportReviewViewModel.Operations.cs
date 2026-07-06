using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseOperations(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Operations), out JsonElement operations) ||
               operations.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. operations.EnumerateArray().Select(operation =>
            {
                string source = FormatEndpoint(GetStringOrNull(operation, "SourceSpaceId"), GetRangeOrNull(operation, "SourceRange"));
                string target = FormatEndpoint(GetString(operation, "TargetSpaceId"), GetRangeOrNull(operation, "TargetRange"));
                string reason = GetString(operation, "Reason");
                (string reasonSummary, string commandBlock) = ExtractCombinerCommand(reason);
                return new ReportLineViewModel(
                    $"{GetLong(operation, "Sequence")}. {GetString(operation, "OperationId")}",
                    $"{GetString(operation, "Kind")} {source} -> {target}",
                    reasonSummary,
                    commandBlock,
                    CreateOperationBadges(operation),
                    CreateOperationFacts(operation, source, target, reasonSummary));
            }),
            ];
    }

    private static ReportLineBadgeViewModel[] CreateOperationBadges(JsonElement operation)
    {
        string status = GetString(operation, nameof(Status));
        string overlapPolicy = GetString(operation, "OverlapPolicy");
        string provenance = FormatOperationProvenance(operation);
        return
        [
            new ReportLineBadgeViewModel(string.IsNullOrWhiteSpace(status) ? "status unknown" : status),
            new ReportLineBadgeViewModel(string.IsNullOrWhiteSpace(overlapPolicy) ? "overlap unknown" : $"overlap {overlapPolicy}"),
            new ReportLineBadgeViewModel(string.IsNullOrWhiteSpace(provenance) ? "source unknown" : provenance),
        ];
    }

    private static List<ReportLineFactViewModel> CreateOperationFacts(
        JsonElement operation,
        string source,
        string target,
        string reasonSummary)
    {
        List<ReportLineFactViewModel> facts =
        [
            new("Source", source, isTechnical: true),
            new("Target", target, isTechnical: true),
            new("Operation source", FormatOperationProvenance(operation), isTechnical: true),
            new("Reason", reasonSummary),
        ];

        if (GetStringOrNull(operation, "ProcessorId") is not { } processorId)
        {
            return facts;
        }

        facts.Add(new ReportLineFactViewModel("Processor", processorId, isTechnical: true));
        if (GetStringOrNull(operation, "ToolBindingId") is { } toolBindingId)
        {
            facts.Add(new ReportLineFactViewModel("Tool", toolBindingId, isTechnical: true));
        }

        facts.Add(new ReportLineFactViewModel(
            "Read ranges",
            FormatRangeList(operation, "ProcessorAllowedReadRanges"),
            isTechnical: true));
        facts.Add(new ReportLineFactViewModel(
            "Write ranges",
            FormatRangeList(operation, "ProcessorAllowedWriteRanges"),
            isTechnical: true));
        return facts;
    }

    private static string FormatOperationProvenance(JsonElement operation)
    {
        if (!operation.TryGetProperty("Provenance", out JsonElement provenance) ||
            provenance.ValueKind != JsonValueKind.Object)
        {
            return "source unknown";
        }

        string kind = GetString(provenance, "Kind");
        string? sourceId = GetStringOrNull(provenance, "SourceId");
        string? sourceVersion = GetStringOrNull(provenance, "SourceVersion");
        return string.IsNullOrWhiteSpace(sourceId)
            ? kind
            : string.IsNullOrWhiteSpace(sourceVersion) ? $"{kind}: {sourceId}" : $"{kind}: {sourceId}@{sourceVersion}";
    }
}
