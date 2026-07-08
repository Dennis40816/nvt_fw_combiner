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
        return string.Equals(code, "input.address-space.truncated", StringComparison.Ordinal)
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
        if (ClassifyInput(addressSpaceId) == "base")
        {
            return "Base flash image";
        }

        string normalized = source;
        const string prefix = "replace-ctrlram-";
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }

        string[] parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return source;
        }

        string region = parts[0].ToUpperInvariant() switch
        {
            "NF" => "NF",
            "MP" => "MP",
            "VN" => "VN",
            "NORMAL" => "Normal",
            _ => parts[0],
        };
        string side = parts.Length >= 2 && string.Equals(parts[1], "master", StringComparison.OrdinalIgnoreCase)
            ? "Master"
            : parts.Length >= 3 && string.Equals(parts[1], "slave", StringComparison.OrdinalIgnoreCase)
                ? $"Slave {parts[2].ToUpperInvariant()}"
                : string.Empty;

        return string.IsNullOrWhiteSpace(side) ? region : $"{region} CtrlRAM ({side})";
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
