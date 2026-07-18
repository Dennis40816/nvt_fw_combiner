using System.Text.Json;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseInputs(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Inputs), out JsonElement inputs) || inputs.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. inputs.EnumerateArray().Select(input =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string addressSpaceId = GetString(input, "AddressSpaceId");
                    string artifactId = GetString(input, "ArtifactId");
                    long size = GetLong(input, "Size");
                    string role = FormatInputRole(addressSpaceId);
                    return new ReportLineViewModel(
                        FormatInputTitle(addressSpaceId, artifactId),
                        string.IsNullOrWhiteSpace(artifactId) ? addressSpaceId : artifactId,
                        addressSpaceId,
                        classification: ClassifyInput(addressSpaceId),
                        inputRole: role,
                        inputSizeLabel: $"{size} bytes",
                        inputAddressSpace: addressSpaceId);
                }),
            ];
    }

    private static IReadOnlyList<ReportLineViewModel> ParseMutations(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Mutations), out JsonElement mutations) ||
               mutations.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. mutations.EnumerateArray().Select(mutation =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ReportLineViewModel(
                        GetString(mutation, "OperationId"),
                        $"{GetString(mutation, "TargetSpaceId")} {GetRangeOrNull(mutation, "TargetRange")} changed={GetLong(mutation, "ChangedByteCount")}",
                        $"{GetString(mutation, "BeforeSha256")} -> {GetString(mutation, "AfterSha256")}");
                }),
            ];
    }

    private static IReadOnlyList<ReportLineViewModel> ParseIssues(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        return !root.TryGetProperty(nameof(Issues), out JsonElement issues) || issues.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. issues.EnumerateArray().Select(issue =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string code = GetString(issue, "Code");
                    string severity = GetStringOrNull(issue, "Severity") ??
                        GetStringOrNull(issue, "severity") ??
                        LegacySeverityForIssueCode(code);
                    return new ReportLineViewModel(
                        code,
                        GetString(issue, "Message"),
                        GetStringOrNull(issue, "OperationId") ?? "run",
                        severity: severity);
                }),
            ];
    }

    private static string LegacySeverityForIssueCode(string code)
    {
        return string.Equals(code, WorkbenchCompositionIssueCodes.InputAddressSpaceTruncated, StringComparison.Ordinal)
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
            : WorkbenchSlotIds.TryFormatReplaceCtrlRamLabel(source, out string ctrlRamLabel)
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
