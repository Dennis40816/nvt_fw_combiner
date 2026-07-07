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
                string processor = GetStringOrNull(operation, "ProcessorId") ??
                    GetStringOrNull(operation, "ToolBindingId") ??
                    "-";
                string status = GetString(operation, nameof(Status));
                string reason = GetString(operation, "Reason");
                (string reasonSummary, string commandBlock) = ExtractCombinerCommand(reason);
                return new ReportLineViewModel(
                    $"{GetLong(operation, "Sequence")}. {GetString(operation, "OperationId")}",
                    $"{GetString(operation, "Kind")} {source} -> {target}",
                    reasonSummary,
                    commandBlock,
                    CreateOperationBadges(operation),
                    CreateOperationFacts(operation, reasonSummary),
                    CreateOperationRangeRows(operation),
                    operationKind: GetString(operation, "Kind"),
                    operationSource: source,
                    operationTarget: target,
                    operationProcessor: processor,
                    operationStatus: string.IsNullOrWhiteSpace(status) ? "unknown" : status);
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
        string reasonSummary)
    {
        List<ReportLineFactViewModel> facts =
        [
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

        return facts;
    }

    private static List<ReportRangeTableRowViewModel> CreateOperationRangeRows(JsonElement operation)
    {
        List<ReportRangeTableRowViewModel> rows = [];
        string targetSpace = GetString(operation, "TargetSpaceId");
        AddSingleRangeRow(
            rows,
            "Source",
            GetStringOrNull(operation, "SourceSpaceId"),
            TryGetRange(operation, "SourceRange"),
            "operation input");
        AddSingleRangeRow(
            rows,
            "Target",
            targetSpace,
            TryGetRange(operation, "TargetRange"),
            "work image");
        AddRangeRows(
            rows,
            operation,
            "ProcessorAllowedReadRanges",
            "Processor read",
            targetSpace,
            "postbuild read policy");
        AddRangeRows(
            rows,
            operation,
            "ProcessorAllowedWriteRanges",
            "Processor write",
            targetSpace,
            "postbuild write policy");
        return rows;
    }

    private static void AddSingleRangeRow(
        List<ReportRangeTableRowViewModel> rows,
        string kind,
        string? addressSpace,
        JsonElement? range,
        string source)
    {
        if (string.IsNullOrWhiteSpace(addressSpace) || range is null)
        {
            return;
        }

        rows.Add(new ReportRangeTableRowViewModel(
            kind,
            addressSpace,
            FormatRange(range.Value),
            source));
    }

    private static void AddRangeRows(
        List<ReportRangeTableRowViewModel> rows,
        JsonElement operation,
        string propertyName,
        string kind,
        string addressSpace,
        string source)
    {
        if (string.IsNullOrWhiteSpace(addressSpace) ||
            !operation.TryGetProperty(propertyName, out JsonElement ranges) ||
            ranges.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement range in ranges.EnumerateArray())
        {
            rows.Add(new ReportRangeTableRowViewModel(
                kind,
                addressSpace,
                FormatRange(range),
                source));
        }
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
