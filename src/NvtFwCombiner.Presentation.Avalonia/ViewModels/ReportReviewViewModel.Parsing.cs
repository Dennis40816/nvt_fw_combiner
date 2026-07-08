using System.Text.Json;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseInputs(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Inputs), out JsonElement inputs) || inputs.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. inputs.EnumerateArray().Select(input =>
                {
                    string addressSpaceId = GetString(input, "AddressSpaceId");
                    string artifactId = GetString(input, "ArtifactId");
                    long size = GetLong(input, "Size");
                    return new ReportLineViewModel(
                        FormatInputTitle(addressSpaceId, artifactId),
                        string.IsNullOrWhiteSpace(artifactId) ? addressSpaceId : artifactId,
                        addressSpaceId,
                        badges:
                        [
                            new ReportLineBadgeViewModel(FormatInputRole(addressSpaceId)),
                            new ReportLineBadgeViewModel($"{size} bytes"),
                        ],
                        facts:
                        [
                            new ReportLineFactViewModel("Address space", addressSpaceId, isTechnical: true),
                        ],
                        classification: ClassifyInput(addressSpaceId));
                }),
            ];
    }

    private static IReadOnlyList<ReportLineViewModel> ParseMutations(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Mutations), out JsonElement mutations) ||
               mutations.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. mutations.EnumerateArray().Select(mutation => new ReportLineViewModel(
                GetString(mutation, "OperationId"),
                $"{GetString(mutation, "TargetSpaceId")} {GetRangeOrNull(mutation, "TargetRange")} changed={GetLong(mutation, "ChangedByteCount")}",
                $"{GetString(mutation, "BeforeSha256")} -> {GetString(mutation, "AfterSha256")}")),
            ];
    }

    private static IReadOnlyList<ReportLineViewModel> ParseIssues(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Issues), out JsonElement issues) || issues.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. issues.EnumerateArray().Select(issue =>
                {
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
        return addressSpaceId.Contains("base", StringComparison.OrdinalIgnoreCase) ||
            addressSpaceId.Contains("reference", StringComparison.OrdinalIgnoreCase)
            ? "base"
            : addressSpaceId.Contains("ctrlram", StringComparison.OrdinalIgnoreCase)
                ? "ctrlram"
                : "other";
    }

    private static string FormatInputRole(string addressSpaceId)
    {
        return ClassifyInput(addressSpaceId) switch
        {
            "base" => "base",
            "ctrlram" => "replacement",
            _ => "input",
        };
    }

    private static string FormatInputTitle(string addressSpaceId, string artifactId)
    {
        string source = string.IsNullOrWhiteSpace(artifactId) ? addressSpaceId : artifactId;
        return ClassifyInput(addressSpaceId) == "base"
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
}
