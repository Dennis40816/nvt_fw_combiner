using System.Text.Json;
using System.Text;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseOperations(JsonElement root, ShellLanguage language)
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
                string kind = GetString(operation, "Kind");
                string operationId = GetString(operation, "OperationId");
                string? processorId = GetStringOrNull(operation, "ProcessorId");
                string? toolBindingId = GetStringOrNull(operation, "ToolBindingId");
                string processor = processorId ??
                    toolBindingId ??
                    "-";
                string status = GetString(operation, nameof(Status));
                string reason = GetString(operation, "Reason");
                (string reasonSummary, string commandBlock) = ExtractCombinerCommand(reason);
                IReadOnlyList<ReportRuntimeCommandViewModel> runtimeCommands = ParseRuntimeCommands(operation, language);
                return new ReportLineViewModel(
                    FormatOperationTitle(GetLong(operation, "Sequence"), kind, operationId),
                    FormatOperationDetail(kind, target, processorId, toolBindingId),
                    reasonSummary,
                    commandBlock,
                    CreateOperationBadges(operation),
                    CreateOperationFacts(operation, reasonSummary),
                    CreateOperationRangeRows(operation),
                    operationKind: kind,
                    operationSource: source,
                    operationTarget: target,
                    operationProcessor: processor,
                    operationStatus: string.IsNullOrWhiteSpace(status) ? "unknown" : status,
                    codeBlockLabel: string.IsNullOrWhiteSpace(commandBlock)
                        ? string.Empty
                        : T(language, "Profile-declared Combiner plan", "Profile 宣告的 Combiner 計畫"),
                    runtimeCommands: runtimeCommands,
                    runtimeCommandsLabel: runtimeCommands.Count == 0
                        ? string.Empty
                        : T(
                            language,
                            $"Actual runtime argv ({runtimeCommands.Count})",
                            $"實際執行 argv（{runtimeCommands.Count}）"));
            }),
            ];
    }

    private static IReadOnlyList<ReportRuntimeCommandViewModel> ParseRuntimeCommands(
        JsonElement operation,
        ShellLanguage language)
    {
        return !operation.TryGetProperty("ExecutedCommands", out JsonElement commands) ||
               commands.ValueKind != JsonValueKind.Array
            ? []
            : [
                .. commands.EnumerateArray().Select((command, index) =>
                {
                    string executablePath = GetString(command, "ExecutablePath");
                    string workingDirectory = GetString(command, "WorkingDirectory");
                    return new ReportRuntimeCommandViewModel(
                        T(language, $"Runtime invocation {index + 1}", $"實際呼叫 {index + 1}"),
                        FormatRuntimeArgumentList(command, FirmwarePathDisplay.Normalize(executablePath)),
                        T(
                            language,
                            $"Working directory: {FirmwarePathDisplay.Normalize(workingDirectory)}",
                            $"工作目錄：{FirmwarePathDisplay.Normalize(workingDirectory)}"));
                }),
            ];
    }

    private static string FormatRuntimeArgumentList(JsonElement command, string executablePath)
    {
        var builder = new StringBuilder($"exe: {executablePath}");
        if (!command.TryGetProperty("Arguments", out JsonElement arguments) ||
            arguments.ValueKind != JsonValueKind.Array)
        {
            return builder.ToString();
        }

        int index = 0;
        foreach (JsonElement argument in arguments.EnumerateArray())
        {
            _ = builder
                .AppendLine()
                .Append("argv[")
                .Append(index++)
                .Append("]: ")
                .Append(argument.GetString() ?? string.Empty);
        }

        return builder.ToString();
    }

    private static string FormatOperationTitle(long sequence, string kind, string operationId)
    {
        string title = string.Equals(kind, "RunExternalProcessor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "run-external-processor", StringComparison.OrdinalIgnoreCase)
            ? "Postbuild refresh"
            : string.IsNullOrWhiteSpace(operationId) ? kind : operationId;

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{sequence}. {title}");
    }

    private static string FormatOperationDetail(
        string kind,
        string target,
        string? processorId,
        string? toolBindingId)
    {
        if (!string.Equals(kind, "RunExternalProcessor", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(kind, "run-external-processor", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        string tool = string.IsNullOrWhiteSpace(toolBindingId) ? "approved postbuild tool" : toolBindingId;
        string processor = string.IsNullOrWhiteSpace(processorId) ? string.Empty : $" via {processorId}";
        return $"{tool}{processor} updates {target}";
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

    private static string FormatEndpoint(string? addressSpaceId, string? range)
    {
        return string.IsNullOrWhiteSpace(addressSpaceId)
            ? "(none)"
            : $"{addressSpaceId} {range ?? string.Empty}".Trim();
    }

    private static int CountRuntimeInvocations(IEnumerable<ReportLineViewModel> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations.Sum(operation => operation.RuntimeCommands.Count);
    }

    private static (string ReasonSummary, string CommandBlock) ExtractCombinerCommand(string reason)
    {
        const string marker = "Combiner command: ";
        int markerIndex = reason.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return (reason, string.Empty);
        }

        string summary = reason[..markerIndex].Trim();
        string command = reason[(markerIndex + marker.Length)..].Trim();
        if (command.EndsWith('.'))
        {
            command = command[..^1];
        }

        return (summary, command);
    }
}
