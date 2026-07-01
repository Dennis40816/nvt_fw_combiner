using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Readable UI projection of a CLI/application run report JSON file.</summary>
public sealed class ReportReviewViewModel
{
    private ReportReviewViewModel(
        bool isEmpty,
        string sourceName,
        string title,
        string subtitle,
        string status,
        string output,
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        IsEmpty = isEmpty;
        SourceName = sourceName;
        Title = title;
        Subtitle = subtitle;
        Status = status;
        Output = output;
        Inputs = inputs;
        Operations = operations;
        Mutations = mutations;
        Issues = issues;
    }

    /// <summary>Empty report placeholder.</summary>
    public static ReportReviewViewModel Empty { get; } = new(
        true,
        string.Empty,
        "No report loaded",
        "Load a run report JSON to review it here.",
        "Idle",
        string.Empty,
        [],
        [],
        [],
        []);

    /// <summary>True when no report is loaded.</summary>
    public bool IsEmpty { get; }

    /// <summary>File name or parser source label.</summary>
    public string SourceName { get; }

    /// <summary>Report title.</summary>
    public string Title { get; }

    /// <summary>Report subtitle.</summary>
    public string Subtitle { get; }

    /// <summary>Run status summary.</summary>
    public string Status { get; }

    /// <summary>Output artifact summary.</summary>
    public string Output { get; }

    /// <summary>Input artifact rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Inputs { get; }

    /// <summary>Operation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Operations { get; }

    /// <summary>Mutation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Mutations { get; }

    /// <summary>Issue rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Issues { get; }

    /// <summary>Loads a readable report model from run report JSON.</summary>
    public static ReportReviewViewModel FromJson(string json, string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string profileId = GetString(root, "ProfileId");
        string icId = GetString(root, "IcId");
        string experienceId = GetString(root, "ExperienceId");
        string compositionKind = GetString(root, "CompositionKind");
        string runId = GetString(root, "RunId");
        string startedAt = GetString(root, "StartedAtUtc");
        IReadOnlyList<ReportLineViewModel> inputs = ParseInputs(root);
        IReadOnlyList<ReportLineViewModel> operations = ParseOperations(root);
        IReadOnlyList<ReportLineViewModel> mutations = ParseMutations(root);
        IReadOnlyList<ReportLineViewModel> issues = ParseIssues(root);
        string status = issues.Count == 0
            ? "Succeeded"
            : string.Create(CultureInfo.InvariantCulture, $"{issues.Count} issue(s)");

        return new ReportReviewViewModel(
            false,
            sourceName,
            $"{profileId} ({icId})",
            $"{compositionKind} / {experienceId} / {Shorten(runId, 18)} / {startedAt}",
            status,
            ParseOutput(root),
            inputs,
            operations,
            mutations,
            issues);
    }

    /// <summary>Creates an error report when JSON parsing fails.</summary>
    public static ReportReviewViewModel Error(string sourceName, string message)
    {
        return new ReportReviewViewModel(
            false,
            sourceName,
            "Report could not be loaded",
            sourceName,
            "Invalid JSON",
            string.Empty,
            [],
            [],
            [],
            [new ReportLineViewModel("Parse error", message, "report-json")]);
    }

    private static IReadOnlyList<ReportLineViewModel> ParseInputs(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Inputs), out JsonElement inputs) || inputs.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. inputs.EnumerateArray().Select(input => new ReportLineViewModel(
                $"{GetString(input, "AddressSpaceId")} ({GetLong(input, "Size")} bytes)",
                Shorten(GetString(input, "Sha256"), 16),
                GetString(input, "ArtifactId"))),
            ];
    }

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
                string processor = GetStringOrNull(operation, "ProcessorId") is { } processorId
                    ? $" / {processorId}"
                    : string.Empty;
                return new ReportLineViewModel(
                    $"{GetLong(operation, "Sequence")}. {GetString(operation, "OperationId")}",
                    $"{GetString(operation, "Kind")} {source} -> {target}",
                    $"{GetString(operation, nameof(Status))} / {GetString(operation, "OverlapPolicy")}{processor} / {GetString(operation, "Reason")}");
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
                $"{Shorten(GetString(mutation, "BeforeSha256"), 10)} -> {Shorten(GetString(mutation, "AfterSha256"), 10)}")),
            ];
    }

    private static IReadOnlyList<ReportLineViewModel> ParseIssues(JsonElement root)
    {
        return !root.TryGetProperty(nameof(Issues), out JsonElement issues) || issues.ValueKind != JsonValueKind.Array
            ? []
            :
            [
                .. issues.EnumerateArray().Select(issue => new ReportLineViewModel(
                GetString(issue, "Code"),
                GetString(issue, "Message"),
                GetStringOrNull(issue, "OperationId") ?? "run")),
            ];
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
        return $"{GetString(output, "FileName")} / {GetLong(output, "Size")} bytes / {committed} / {Shorten(GetString(output, "Sha256"), 16)}";
    }

    private static string FormatEndpoint(string? addressSpaceId, string? range)
    {
        return string.IsNullOrWhiteSpace(addressSpaceId)
            ? "(none)"
            : $"{addressSpaceId} {range ?? string.Empty}".Trim();
    }

    private static string? GetRangeOrNull(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement range) && range.ValueKind == JsonValueKind.Object
            ? FormatRange(range)
            : null;
    }

    private static string FormatRange(JsonElement range)
    {
        long start = GetLong(range, "Start");
        long end = GetLong(range, "EndExclusive");
        long length = GetLong(range, "Length");
        return string.Create(CultureInfo.InvariantCulture, $"0x{start:X}..0x{end:X} ({length})");
    }

    private static string? GetStringOrNull(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return GetStringOrNull(element, propertyName) ?? string.Empty;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long number)
            ? number
            : 0;
    }

    private static string Shorten(string text, int keep)
    {
        return text.Length <= keep ? text : $"{text[..keep]}...";
    }
}
