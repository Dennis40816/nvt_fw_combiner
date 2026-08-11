using System.Text.Json;
using System.Text;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    internal static IReadOnlyList<ReportLineViewModel> ProjectOperations(
        IReadOnlyList<OperationRunSummary> operations,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        return ProjectLines(
            operations,
            operation => CreateOperationLine(
                    new OperationProjection(
                        operation.OperationId,
                        operation.Sequence,
                        operation.Kind.ToString(),
                        operation.Status.ToString(),
                        FormatEndpoint(
                            operation.SourceSpaceId,
                            operation.SourceRange is { } sourceRange ? FormatRange(sourceRange) : null),
                        FormatEndpoint(operation.TargetSpaceId, FormatRange(operation.TargetRange)),
                        operation.OverlapPolicy.ToString(),
                        operation.ProcessorId,
                        operation.ToolBindingId,
                        operation.Reason,
                        FormatOperationProvenance(operation.Provenance),
                        CreateOperationRangeRows(operation),
                        ProjectRuntimeCommands(operation.ExecutedCommands, language)),
                    language),
            cancellationToken);
    }

    private static List<ReportLineViewModel> ParseOperations(
        JsonElement root,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Operations), out JsonElement operations) ||
            operations.ValueKind != JsonValueKind.Array
            ? []
            : ProjectLines(
                operations.EnumerateArray(),
                operation => CreateOperationLine(
                    new OperationProjection(
                        GetString(operation, "OperationId"),
                        GetLong(operation, "Sequence"),
                        GetString(operation, "Kind"),
                        GetString(operation, nameof(Status)),
                        FormatEndpoint(
                            GetStringOrNull(operation, "SourceSpaceId"),
                            GetRangeOrNull(operation, "SourceRange")),
                        FormatEndpoint(
                            GetString(operation, "TargetSpaceId"),
                            GetRangeOrNull(operation, "TargetRange")),
                        GetString(operation, "OverlapPolicy"),
                        GetStringOrNull(operation, "ProcessorId"),
                        GetStringOrNull(operation, "ToolBindingId"),
                        GetString(operation, "Reason"),
                        FormatOperationProvenance(operation),
                        CreateOperationRangeRows(operation),
                        ParseRuntimeCommands(operation, language)),
                    language),
                cancellationToken);
    }

    private static ReportLineViewModel CreateOperationLine(
        OperationProjection operation,
        ShellLanguage language)
    {
        bool hasStatus = !string.IsNullOrWhiteSpace(operation.Status);
        string status = hasStatus ? operation.Status : "unknown";
        string processor = operation.ProcessorId ?? operation.ToolBindingId ?? "-";
        (string reasonSummary, string commandBlock) = ExtractCombinerCommand(operation.Reason);
        List<ReportLineFactViewModel> facts =
        [
            new("Operation source", operation.Provenance, isTechnical: true),
            new("Reason", reasonSummary),
        ];
        if (operation.ProcessorId is { } processorId)
        {
            facts.Add(new ReportLineFactViewModel("Processor", processorId, isTechnical: true));
            if (operation.ToolBindingId is { } toolBindingId)
            {
                facts.Add(new ReportLineFactViewModel("Tool", toolBindingId, isTechnical: true));
            }
        }

        return new ReportLineViewModel(
            FormatOperationTitle(operation.Sequence, operation.Kind, operation.OperationId),
            FormatOperationDetail(
                operation.Kind,
                operation.Target,
                operation.ProcessorId,
                operation.ToolBindingId),
            reasonSummary,
            commandBlock,
            [
                new ReportLineBadgeViewModel(hasStatus ? status : "status unknown"),
                new ReportLineBadgeViewModel(string.IsNullOrWhiteSpace(operation.OverlapPolicy)
                    ? "overlap unknown"
                    : $"overlap {operation.OverlapPolicy}"),
                new ReportLineBadgeViewModel(string.IsNullOrWhiteSpace(operation.Provenance)
                    ? "source unknown"
                    : operation.Provenance),
            ],
            facts,
            operation.RangeRows,
            operationKind: operation.Kind,
            operationSource: operation.Source,
            operationTarget: operation.Target,
            operationProcessor: processor,
            operationStatus: status,
            codeBlockLabel: string.IsNullOrWhiteSpace(commandBlock)
                ? string.Empty
                : T(language, "Profile-declared Combiner plan", "Profile 宣告的 Combiner 計畫"),
            runtimeCommands: operation.RuntimeCommands);
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

    private static IReadOnlyList<ReportRuntimeCommandViewModel> ProjectRuntimeCommands(
        IReadOnlyList<ExternalProcessInvocation> commands,
        ShellLanguage language)
    {
        return
        [
            .. commands.Select((command, index) => new ReportRuntimeCommandViewModel(
                T(language, $"Runtime invocation {index + 1}", $"實際呼叫 {index + 1}"),
                FormatRuntimeArgumentList(command),
                T(
                    language,
                    $"Working directory: {FirmwarePathDisplay.Normalize(command.WorkingDirectory)}",
                    $"工作目錄：{FirmwarePathDisplay.Normalize(command.WorkingDirectory)}"))),
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

    private static string FormatRuntimeArgumentList(ExternalProcessInvocation command)
    {
        var builder = new StringBuilder($"exe: {FirmwarePathDisplay.Normalize(command.ExecutablePath)}");
        for (int index = 0; index < command.Arguments.Count; index++)
        {
            _ = builder
                .AppendLine()
                .Append("argv[")
                .Append(index)
                .Append("]: ")
                .Append(command.Arguments[index]);
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

    private static List<ReportRangeTableRowViewModel> CreateOperationRangeRows(OperationRunSummary operation)
    {
        List<ReportRangeTableRowViewModel> rows = [];
        if (operation.SourceSpaceId is { } sourceSpace && operation.SourceRange is { } sourceRange)
        {
            rows.Add(new ReportRangeTableRowViewModel(
                "Source",
                sourceSpace,
                FormatRange(sourceRange),
                "operation input"));
        }

        rows.Add(new ReportRangeTableRowViewModel(
            "Target",
            operation.TargetSpaceId,
            FormatRange(operation.TargetRange),
            "work image"));
        rows.AddRange(operation.ProcessorAllowedReadRanges.Select(range => new ReportRangeTableRowViewModel(
            "Processor read",
            operation.TargetSpaceId,
            FormatRange(range),
            "postbuild read policy")));
        rows.AddRange(operation.ProcessorAllowedWriteRanges.Select(range => new ReportRangeTableRowViewModel(
            "Processor write",
            operation.TargetSpaceId,
            FormatRange(range),
            "postbuild write policy")));
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

    private static string FormatOperationProvenance(OperationProvenance provenance)
    {
        return string.IsNullOrWhiteSpace(provenance.SourceId)
            ? provenance.Kind
            : string.IsNullOrWhiteSpace(provenance.SourceVersion)
                ? $"{provenance.Kind}: {provenance.SourceId}"
                : $"{provenance.Kind}: {provenance.SourceId}@{provenance.SourceVersion}";
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

    private readonly record struct OperationProjection(
        string OperationId,
        long Sequence,
        string Kind,
        string Status,
        string Source,
        string Target,
        string OverlapPolicy,
        string? ProcessorId,
        string? ToolBindingId,
        string Reason,
        string Provenance,
        IReadOnlyList<ReportRangeTableRowViewModel> RangeRows,
        IReadOnlyList<ReportRuntimeCommandViewModel> RuntimeCommands);
}
