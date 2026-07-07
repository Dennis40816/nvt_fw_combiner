using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static IReadOnlyList<ReportLineViewModel> ParseInputs(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Inputs), out JsonElement inputs) || inputs.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. inputs.EnumerateArray().Select(input => new ReportLineViewModel(
                $"{GetString(input, "AddressSpaceId")} ({GetLong(input, "Size")} bytes)",
                GetString(input, "Sha256"),
                GetString(input, "ArtifactId"))),
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
        return string.Equals(code, "input.address-space.truncated", StringComparison.Ordinal)
            ? "warning"
            : "error";
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
        return $"{GetString(output, "FileName")} / {GetLong(output, "Size")} bytes / {committed} / {GetString(output, "Sha256")}";
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
