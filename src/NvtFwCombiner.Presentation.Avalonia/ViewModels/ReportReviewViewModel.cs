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
        CommandOperations = [.. operations.Where(operation => operation.HasCodeBlock)];
        StepOperations = [.. operations.Where(operation => !operation.HasCodeBlock)];
        Mutations = mutations;
        Issues = issues;
        PrimaryIssue = issues.Count == 0 ? ReportLineViewModel.Empty : issues[0];
        HasPrimaryIssue = issues.Count > 0;
        HasInputs = inputs.Count > 0;
        HasOperations = operations.Count > 0;
        HasCommandOperations = CommandOperations.Count > 0;
        HasStepOperations = StepOperations.Count > 0;
        HasMutations = mutations.Count > 0;
        HasIssues = issues.Count > 0;
        SummaryRows = CreateSummaryRows(status, output, inputs, operations, mutations, issues);
        OutcomeTitle = CreateOutcomeTitle(status, issues);
        OutcomeDetail = CreateOutcomeDetail(output, issues);
        OutcomeMeta = issues.Count == 0 ? "No blocking issue" : issues[0].Meta;
        OutcomeIcon = issues.Count == 0 ? "✓" : "!";
        OutcomeAccessibilityLabel = issues.Count == 0 ? "Report succeeded" : "Report has issues";
        NextStepTitle = issues.Count == 0 ? "Ready for audit" : "Start with this issue";
        NextStepDetail = issues.Count == 0
            ? "Use the evidence map only when you need hashes, operation order, or byte-change proof."
            : issues[0].Detail;
        TriageRows = CreateTriageRows(status, output, operations, issues);
        EvidenceRows = CreateEvidenceRows(inputs, operations, mutations, issues);
        ShouldExpandIssues = HasIssues;
        ShouldExpandCommandOperations = HasCommandOperations;
        ShouldExpandStepOperations = HasStepOperations && !HasCommandOperations;
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

    /// <summary>Number of input rows.</summary>
    public int InputCount => Inputs.Count;

    /// <summary>True when input details are available.</summary>
    public bool HasInputs { get; }

    /// <summary>Operation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Operations { get; }

    /// <summary>Number of operation rows.</summary>
    public int OperationCount => Operations.Count;

    /// <summary>True when operation details are available.</summary>
    public bool HasOperations { get; }

    /// <summary>Operations that contain a fixed-width external command block.</summary>
    public IReadOnlyList<ReportLineViewModel> CommandOperations { get; }

    /// <summary>Number of command operation rows.</summary>
    public int CommandOperationCount => CommandOperations.Count;

    /// <summary>True when external command operations are available.</summary>
    public bool HasCommandOperations { get; }

    /// <summary>Operations that do not contain an external command block.</summary>
    public IReadOnlyList<ReportLineViewModel> StepOperations { get; }

    /// <summary>Number of non-command operation rows.</summary>
    public int StepOperationCount => StepOperations.Count;

    /// <summary>True when non-command operation details are available.</summary>
    public bool HasStepOperations { get; }

    /// <summary>Mutation rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Mutations { get; }

    /// <summary>Number of mutation rows.</summary>
    public int MutationCount => Mutations.Count;

    /// <summary>True when mutation details are available.</summary>
    public bool HasMutations { get; }

    /// <summary>Issue rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Issues { get; }

    /// <summary>Number of issue rows.</summary>
    public int IssueCount => Issues.Count;

    /// <summary>True when issue details are available.</summary>
    public bool HasIssues { get; }

    /// <summary>The first issue to show as the report's primary reason.</summary>
    public ReportLineViewModel PrimaryIssue { get; }

    /// <summary>True when the report should show a primary blocking reason.</summary>
    public bool HasPrimaryIssue { get; }

    /// <summary>True when the report has no issue and can use the success treatment.</summary>
    public bool IsSuccessful => !HasPrimaryIssue;

    /// <summary>Compact summary chips shown at the top of the modal.</summary>
    public IReadOnlyList<ReportLineViewModel> SummaryRows { get; }

    /// <summary>Primary report outcome shown before detailed evidence.</summary>
    public string OutcomeTitle { get; }

    /// <summary>Short outcome explanation that tells the user where to start.</summary>
    public string OutcomeDetail { get; }

    /// <summary>Small outcome metadata line.</summary>
    public string OutcomeMeta { get; }

    /// <summary>Short semantic status icon displayed in the report outcome badge.</summary>
    public string OutcomeIcon { get; }

    /// <summary>Readable label for the report outcome icon.</summary>
    public string OutcomeAccessibilityLabel { get; }

    /// <summary>Title for the next recommended review step.</summary>
    public string NextStepTitle { get; }

    /// <summary>Description for the next recommended review step.</summary>
    public string NextStepDetail { get; }

    /// <summary>Ordered rows that tell the user where to look first.</summary>
    public IReadOnlyList<ReportLineViewModel> TriageRows { get; }

    /// <summary>Compact counts for each available evidence category.</summary>
    public IReadOnlyList<ReportLineViewModel> EvidenceRows { get; }

    /// <summary>True when the issue list should open by default.</summary>
    public bool ShouldExpandIssues { get; }

    /// <summary>True when external command evidence should open by default.</summary>
    public bool ShouldExpandCommandOperations { get; }

    /// <summary>True when normal operation evidence should open by default.</summary>
    public bool ShouldExpandStepOperations { get; }

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

    /// <summary>Creates an error report when JSON parsing or loading fails.</summary>
    public static ReportReviewViewModel Error(
        string sourceName,
        string message,
        string issueTitle = "Parse error",
        string status = "Invalid JSON")
    {
        return new ReportReviewViewModel(
            false,
            sourceName,
            "Report could not be loaded",
            sourceName,
            status,
            string.Empty,
            [],
            [],
            [],
            [new ReportLineViewModel(issueTitle, message, "report-json")]);
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

    private static IReadOnlyList<ReportLineViewModel> CreateSummaryRows(
        string status,
        string output,
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        return
        [
            new ReportLineViewModel("Status", status, issues.Count == 0 ? "No issue" : issues[0].Title),
            new ReportLineViewModel("Inputs", inputs.Count.ToString(CultureInfo.InvariantCulture), "files"),
            new ReportLineViewModel("Steps", operations.Count.ToString(CultureInfo.InvariantCulture), commandCount == 0 ? "operations" : $"{commandCount} command(s)"),
            new ReportLineViewModel("Mutations", mutations.Count.ToString(CultureInfo.InvariantCulture), output),
        ];
    }

    private static string CreateOutcomeTitle(string status, IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count == 0
            ? status
            : string.Equals(status, "Load failed", StringComparison.Ordinal)
            ? "Report load failed"
            : "Needs attention";
    }

    private static string CreateOutcomeDetail(string output, IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count == 0
            ? "No reported issues. The detailed sections below are audit evidence, not required reading."
            : string.IsNullOrWhiteSpace(output)
            ? "The run did not produce an output artifact. Start with the first issue below."
            : "Start with the first issue below, then use the evidence map to verify the related inputs, operations, and output.";
    }

    private static IReadOnlyList<ReportLineViewModel> CreateTriageRows(
        string status,
        string output,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        if (issues.Count > 0)
        {
            ReportLineViewModel primaryIssue = issues[0];
            return
            [
                new ReportLineViewModel("1. First issue", primaryIssue.Title, primaryIssue.Meta),
                new ReportLineViewModel("2. Message", primaryIssue.Detail, "reason"),
                new ReportLineViewModel(
                    "3. Evidence",
                    commandCount > 0 ? "Combiner commands" : "Operation steps",
                    commandCount > 0 ? $"{commandCount} external command(s)" : $"{operations.Count} operation(s)"),
            ];
        }

        return
        [
            new ReportLineViewModel("1. Result", status, "No issue"),
            new ReportLineViewModel("2. Output", string.IsNullOrWhiteSpace(output) ? "No output" : output, "artifact"),
            new ReportLineViewModel(
                "3. Evidence",
                commandCount > 0 ? "Combiner commands available" : "Operation trace available",
                commandCount > 0 ? $"{commandCount} external command(s)" : $"{operations.Count} operation(s)"),
        ];
    }

    private static IReadOnlyList<ReportLineViewModel> CreateEvidenceRows(
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        int stepCount = operations.Count - commandCount;
        return
        [
            new ReportLineViewModel(
                "Issues",
                issues.Count.ToString(CultureInfo.InvariantCulture),
                issues.Count == 0 ? "No blocking issue" : issues[0].Title),
            new ReportLineViewModel(
                "Inputs",
                inputs.Count.ToString(CultureInfo.InvariantCulture),
                "file hashes"),
            new ReportLineViewModel(
                "Commands",
                commandCount.ToString(CultureInfo.InvariantCulture),
                "external processors"),
            new ReportLineViewModel(
                "Steps",
                stepCount.ToString(CultureInfo.InvariantCulture),
                "copy/process order"),
            new ReportLineViewModel(
                "Mutations",
                mutations.Count.ToString(CultureInfo.InvariantCulture),
                "changed ranges"),
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
                string reason = GetString(operation, "Reason");
                (string reasonSummary, string commandBlock) = ExtractCombinerCommand(reason);
                string processorTrace = FormatProcessorTrace(operation);
                return new ReportLineViewModel(
                    $"{GetLong(operation, "Sequence")}. {GetString(operation, "OperationId")}",
                    $"{GetString(operation, "Kind")} {source} -> {target}",
                    string.IsNullOrWhiteSpace(processorTrace)
                        ? $"{GetString(operation, nameof(Status))} / {GetString(operation, "OverlapPolicy")} / {reasonSummary}"
                        : $"{GetString(operation, nameof(Status))} / {GetString(operation, "OverlapPolicy")} / {reasonSummary} / {processorTrace}",
                    commandBlock);
            }),
            ];
    }

    private static string FormatProcessorTrace(JsonElement operation)
    {
        if (GetStringOrNull(operation, "ProcessorId") is not { } processorId)
        {
            return string.Empty;
        }

        string toolBinding = GetStringOrNull(operation, "ToolBindingId") is { } toolBindingId
            ? $" / tool {toolBindingId}"
            : string.Empty;
        return $"processor {processorId}{toolBinding} / read {FormatRangeList(operation, "ProcessorAllowedReadRanges")} / write {FormatRangeList(operation, "ProcessorAllowedWriteRanges")}";
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

    private static (string ReasonSummary, string CommandBlock) ExtractCombinerCommand(string reason)
    {
        const string marker = "Combiner command: ";
        int markerIndex = reason.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return (reason, string.Empty);
        }

        string summary = reason[..(markerIndex + "Combiner command".Length)].Trim();
        string command = reason[(markerIndex + marker.Length)..].Trim();
        if (command.EndsWith('.'))
        {
            command = command[..^1];
        }

        return (summary, command);
    }

    private static string? GetRangeOrNull(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement range) && range.ValueKind == JsonValueKind.Object
            ? FormatRange(range)
            : null;
    }

    private static string FormatRangeList(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement ranges) && ranges.ValueKind == JsonValueKind.Array
            ? string.Join(", ", ranges.EnumerateArray().Select(FormatRange))
            : "(none)";
    }

    private static string FormatRange(JsonElement range)
    {
        long start = GetLong(range, "Start");
        long end = GetLong(range, "EndExclusive");
        long length = GetLong(range, "Length");
        return string.Create(CultureInfo.InvariantCulture, $"0x{start:X}-0x{end - 1:X} (len 0x{length:X})");
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
