using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    internal static IReadOnlyList<ReportLineViewModel> ProjectInputs(
        IReadOnlyList<InputArtifactSummary> inputs,
        CancellationToken cancellationToken)
    {
        return ProjectLines(
            inputs,
            static input => CreateInputLine(input.AddressSpaceId, input.ArtifactId, input.Size),
            cancellationToken);
    }

    private static List<ReportLineViewModel> ParseInputs(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Inputs), out JsonElement inputs) ||
            inputs.ValueKind != JsonValueKind.Array
            ? []
            : ProjectLines(
                inputs.EnumerateArray(),
                static input => CreateInputLine(
                    GetString(input, "AddressSpaceId"),
                    GetString(input, "ArtifactId"),
                    GetLong(input, "Size")),
                cancellationToken);
    }

    private static ReportLineViewModel CreateInputLine(
        string addressSpaceId,
        string artifactId,
        long size)
    {
        return new ReportLineViewModel(
            FormatInputTitle(addressSpaceId, artifactId),
            string.IsNullOrWhiteSpace(artifactId) ? addressSpaceId : artifactId,
            addressSpaceId,
            classification: ClassifyInput(addressSpaceId),
            inputRole: FormatInputRole(addressSpaceId),
            inputSizeLabel: $"{size} bytes",
            inputAddressSpace: addressSpaceId);
    }

    private static List<ReportLineViewModel> ParseMutations(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Mutations), out JsonElement mutations) ||
            mutations.ValueKind != JsonValueKind.Array
            ? []
            : ProjectLines(
                mutations.EnumerateArray(),
                static mutation => CreateMutationLine(
                    GetString(mutation, "OperationId"),
                    GetString(mutation, "TargetSpaceId"),
                    GetRangeOrNull(mutation, "TargetRange") ?? string.Empty,
                    GetLong(mutation, "ChangedByteCount"),
                    GetString(mutation, "BeforeSha256"),
                    GetString(mutation, "AfterSha256")),
                cancellationToken);
    }

    internal static IReadOnlyList<ReportLineViewModel> ProjectMutations(
        IReadOnlyList<MutationRunSummary> mutations,
        CancellationToken cancellationToken)
    {
        return ProjectLines(
            mutations,
            static mutation => CreateMutationLine(
                    mutation.OperationId,
                    mutation.TargetSpaceId,
                    FormatRange(mutation.TargetRange),
                    mutation.ChangedByteCount,
                    mutation.BeforeSha256,
                    mutation.AfterSha256),
            cancellationToken);
    }

    private static ReportLineViewModel CreateMutationLine(
        string operationId,
        string targetSpaceId,
        string targetRange,
        long changedByteCount,
        string beforeSha256,
        string afterSha256)
    {
        return new ReportLineViewModel(
            operationId,
            $"{targetSpaceId} {targetRange} changed={changedByteCount}",
            $"{beforeSha256} -> {afterSha256}");
    }

    private static List<ReportLineViewModel> ParseIssues(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Issues), out JsonElement issues) ||
            issues.ValueKind != JsonValueKind.Array
            ? []
            : ProjectLines(
                issues.EnumerateArray(),
                issue =>
                {
                    string code = GetString(issue, "Code");
                    string severity = GetStringOrNull(issue, "Severity") ??
                        GetStringOrNull(issue, "severity") ??
                        LegacySeverityForIssueCode(code);
                    return CreateIssueLine(
                        code,
                        GetString(issue, "Message"),
                        GetStringOrNull(issue, "OperationId") ?? "run",
                        severity);
                },
                cancellationToken);
    }

    internal static IReadOnlyList<ReportLineViewModel> ProjectIssues(
        IReadOnlyList<CompositionIssue> issues,
        CancellationToken cancellationToken)
    {
        return ProjectLines(
            issues,
            static issue => CreateIssueLine(
                    issue.Code,
                    issue.Message,
                    issue.OperationId ?? "run",
                    issue.Severity),
            cancellationToken);
    }

    private static List<ReportLineViewModel> ProjectLines<T>(
        IEnumerable<T> source,
        Func<T, ReportLineViewModel> project,
        CancellationToken cancellationToken)
    {
        List<ReportLineViewModel> lines = [];
        foreach (T item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.Add(project(item));
        }

        return lines;
    }

    private static ReportLineViewModel CreateIssueLine(
        string code,
        string message,
        string operationId,
        string severity)
    {
        return new ReportLineViewModel(code, message, operationId, severity: severity);
    }

    private static string LegacySeverityForIssueCode(string code)
    {
        return string.Equals(code, CompositionIssueCodes.InputAddressSpaceTruncated, StringComparison.Ordinal)
            ? "warning"
            : "error";
    }

    private static string ClassifyInput(string addressSpaceId)
    {
        return addressSpaceId.Contains(ReportInputClassifications.BaseSearchTerm, StringComparison.OrdinalIgnoreCase) ||
            addressSpaceId.Contains(ReportInputClassifications.ReferenceSearchTerm, StringComparison.OrdinalIgnoreCase)
            ? ReportInputClassifications.Base
            : addressSpaceId.Contains(ReportInputClassifications.CtrlRamSearchTerm, StringComparison.OrdinalIgnoreCase)
                ? ReportInputClassifications.CtrlRam
                : ReportInputClassifications.Other;
    }

    private static string FormatInputRole(string addressSpaceId)
    {
        return ClassifyInput(addressSpaceId) switch
        {
            ReportInputClassifications.Base => ReportInputClassifications.Base,
            ReportInputClassifications.CtrlRam => ReportInputClassifications.RoleReplacement,
            _ => ReportInputClassifications.RoleInput,
        };
    }

    private static string FormatInputTitle(string addressSpaceId, string artifactId)
    {
        string source = string.IsNullOrWhiteSpace(artifactId) ? addressSpaceId : artifactId;
        return ClassifyInput(addressSpaceId) == ReportInputClassifications.Base
            ? "Base flash image"
            : DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(source, out string ctrlRamLabel)
                ? ctrlRamLabel
                : source;
    }

    private static string ParseOutput(JsonElement root)
    {
        if (!root.TryGetProperty(nameof(Output), out JsonElement output) || output.ValueKind != JsonValueKind.Object)
        {
            return "No output";
        }

        string committed = output.TryGetProperty("Committed", out JsonElement committedElement) &&
                           committedElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? committedElement.GetBoolean() ? "committed" : "preview"
            : "unknown";
        return $"{GetString(output, "FileName")} / {GetLong(output, "Size")} bytes / {committed}";
    }

    internal static string ProjectOutput(OutputArtifactSummary? output)
    {
        return output is null
            ? "No output"
            : $"{output.FileName} / {output.Size} bytes / {(output.Committed ? "committed" : "preview")}";
    }

    private static string GetOutputString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(nameof(Output), out JsonElement output) && output.ValueKind == JsonValueKind.Object
            ? GetString(output, propertyName)
            : string.Empty;
    }

    private static long GetOutputLong(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(nameof(Output), out JsonElement output) && output.ValueKind == JsonValueKind.Object
            ? GetLong(output, propertyName)
            : 0;
    }

    private static bool? GetOutputCommitted(JsonElement root)
    {
        return root.TryGetProperty(nameof(Output), out JsonElement output) &&
               output.ValueKind == JsonValueKind.Object &&
               output.TryGetProperty("Committed", out JsonElement committed) &&
               committed.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? committed.GetBoolean()
            : null;
    }
}
