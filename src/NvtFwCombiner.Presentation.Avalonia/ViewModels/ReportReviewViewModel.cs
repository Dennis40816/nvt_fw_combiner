using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Readable UI projection of a CLI/application run report JSON file.</summary>
public sealed partial class ReportReviewViewModel
{
    private ReportReviewViewModel(
        bool isEmpty,
        string sourceName,
        string profileId,
        string icId,
        string modeId,
        string experienceId,
        string compositionKind,
        string runId,
        string startedAtUtc,
        string title,
        string subtitle,
        string status,
        string output,
        string outputFileName,
        long outputSize,
        string outputSha256,
        string outputArtifactPath,
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> mutations,
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        IsEmpty = isEmpty;
        SourceName = sourceName;
        ProfileId = profileId;
        IcId = icId;
        ModeId = modeId;
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
        RunId = runId;
        StartedAtUtc = startedAtUtc;
        Title = title;
        Subtitle = subtitle;
        Status = status;
        Output = output;
        OutputFileName = outputFileName;
        OutputSize = outputSize;
        OutputSha256 = outputSha256;
        OutputHashLabel = string.IsNullOrWhiteSpace(outputSha256) ? "No output hash" : Shorten(outputSha256, 16);
        OutputArtifactPath = string.IsNullOrWhiteSpace(outputArtifactPath) ? string.Empty : outputArtifactPath;
        HasOutputArtifactPath = !string.IsNullOrWhiteSpace(OutputArtifactPath);
        Inputs = inputs;
        Operations = operations;
        CommandOperations = [.. operations.Where(operation => operation.HasCodeBlock)];
        StepOperations = [.. operations.Where(operation => !operation.HasCodeBlock)];
        Mutations = mutations;
        OutputDifferences = outputDifferences;
        Issues = issues;
        Warnings = [.. issues.Where(IsWarning)];
        BlockingIssues = [.. issues.Where(issue => !IsWarning(issue))];
        PrimaryIssue = BlockingIssues.Count == 0 ? ReportLineViewModel.Empty : BlockingIssues[0];
        HasPrimaryIssue = BlockingIssues.Count > 0;
        HasWarnings = Warnings.Count > 0;
        HasWarningsWithoutBlockingIssues = HasWarnings && !HasPrimaryIssue;
        IsClean = !HasPrimaryIssue && !HasWarnings;
        HasInputs = inputs.Count > 0;
        HasOperations = operations.Count > 0;
        HasCommandOperations = CommandOperations.Count > 0;
        HasStepOperations = StepOperations.Count > 0;
        HasMutations = mutations.Count > 0;
        HasOutputDifferences = outputDifferences.Count > 0;
        HasIssues = issues.Count > 0;
        SummaryRows = CreateSummaryRows(status, output, inputs, operations, mutations, issues);
        OutcomeTitle = CreateOutcomeTitle(status, issues);
        OutcomeDetail = CreateOutcomeDetail(output, issues);
        OutcomeMeta = CreateOutcomeMeta(issues);
        OutcomeIcon = HasPrimaryIssue || HasWarnings ? "!" : "✓";
        OutcomeAccessibilityLabel = HasPrimaryIssue
            ? "Report has issues"
            : HasWarnings ? "Report succeeded with warnings" : "Report succeeded";
        NextStepTitle = HasPrimaryIssue ? "Start with this issue" : HasWarnings ? "Review warning" : "Ready for audit";
        NextStepDetail = HasPrimaryIssue
            ? PrimaryIssue.Detail
            : HasWarnings
            ? Warnings[0].Detail
            : "Use the evidence map only when you need hashes, operation order, or byte-change proof.";
        TriageRows = CreateTriageRows(status, output, operations, issues);
        EvidenceRows = CreateEvidenceRows(inputs, operations, mutations, outputDifferences, issues);
        ShouldExpandIssues = HasIssues && (HasPrimaryIssue || HasWarnings);
        ShouldExpandCommandOperations = HasCommandOperations;
        ShouldExpandStepOperations = HasStepOperations && !HasCommandOperations;
    }

    /// <summary>Empty report placeholder.</summary>
    public static ReportReviewViewModel Empty { get; } = new(
        true,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        "No report loaded",
        "Load a run report JSON to review it here.",
        "Idle",
        string.Empty,
        string.Empty,
        0,
        string.Empty,
        string.Empty,
        [],
        [],
        [],
        [],
        []);

    /// <summary>True when no report is loaded.</summary>
    public bool IsEmpty { get; }

    /// <summary>File name or parser source label.</summary>
    public string SourceName { get; }

    /// <summary>Profile id recorded by the run report.</summary>
    public string ProfileId { get; }

    /// <summary>IC id recorded by the run report.</summary>
    public string IcId { get; }

    /// <summary>Mode id recorded by the run report.</summary>
    public string ModeId { get; }

    /// <summary>Experience id recorded by the run report.</summary>
    public string ExperienceId { get; }

    /// <summary>Composition kind recorded by the run report.</summary>
    public string CompositionKind { get; }

    /// <summary>Run id recorded by the run report.</summary>
    public string RunId { get; }

    /// <summary>Start timestamp recorded by the run report.</summary>
    public string StartedAtUtc { get; }

    /// <summary>Report title.</summary>
    public string Title { get; }

    /// <summary>Report subtitle.</summary>
    public string Subtitle { get; }

    /// <summary>Run status summary.</summary>
    public string Status { get; }

    /// <summary>Output artifact summary.</summary>
    public string Output { get; }

    /// <summary>Report-safe output file name.</summary>
    public string OutputFileName { get; }

    /// <summary>Output size in bytes.</summary>
    public long OutputSize { get; }

    /// <summary>Full output SHA-256 recorded by the report.</summary>
    public string OutputSha256 { get; }

    /// <summary>Compact output hash label for dense traceability surfaces.</summary>
    public string OutputHashLabel { get; }

    /// <summary>Host-side output artifact path for the current UI session, not persisted in report JSON.</summary>
    public string OutputArtifactPath { get; }

    /// <summary>True when the current UI session knows the committed output artifact path.</summary>
    public bool HasOutputArtifactPath { get; }

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

    /// <summary>Final output-vs-reference difference rows.</summary>
    public IReadOnlyList<ReportLineViewModel> OutputDifferences { get; }

    /// <summary>Number of final output-vs-reference difference rows.</summary>
    public int OutputDifferenceCount => OutputDifferences.Count;

    /// <summary>True when output difference details are available.</summary>
    public bool HasOutputDifferences { get; }

    /// <summary>Issue rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Issues { get; }

    /// <summary>Number of diagnostic rows, including warnings.</summary>
    public int IssueCount => Issues.Count;

    /// <summary>True when issue or warning diagnostics are available.</summary>
    public bool HasIssues { get; }

    /// <summary>Warning diagnostics that do not block a successful run.</summary>
    public IReadOnlyList<ReportLineViewModel> Warnings { get; }

    /// <summary>Number of warning diagnostics.</summary>
    public int WarningCount => Warnings.Count;

    /// <summary>True when warning diagnostics are available.</summary>
    public bool HasWarnings { get; }

    /// <summary>Blocking issue diagnostics.</summary>
    public IReadOnlyList<ReportLineViewModel> BlockingIssues { get; }

    /// <summary>Number of blocking issue diagnostics.</summary>
    public int BlockingIssueCount => BlockingIssues.Count;

    /// <summary>True when warnings exist but no blocking issue exists.</summary>
    public bool HasWarningsWithoutBlockingIssues { get; }

    /// <summary>The first issue to show as the report's primary reason.</summary>
    public ReportLineViewModel PrimaryIssue { get; }

    /// <summary>True when the report should show a primary blocking reason.</summary>
    public bool HasPrimaryIssue { get; }

    /// <summary>True when the report has no blocking issue and can use the success treatment.</summary>
    public bool IsSuccessful => !HasPrimaryIssue;

    /// <summary>True when the report has neither blocking issues nor warnings.</summary>
    public bool IsClean { get; }

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
    public static ReportReviewViewModel FromJson(
        string json,
        string sourceName,
        string? outputArtifactPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string profileId = GetString(root, nameof(ProfileId));
        string icId = GetString(root, nameof(IcId));
        string modeId = GetString(root, nameof(ModeId));
        string experienceId = GetString(root, nameof(ExperienceId));
        string compositionKind = GetString(root, nameof(CompositionKind));
        string runId = GetString(root, nameof(RunId));
        string startedAt = GetString(root, nameof(StartedAtUtc));
        string outputFileName = GetOutputString(root, "FileName");
        long outputSize = GetOutputLong(root, "Size");
        string outputSha256 = GetOutputString(root, "Sha256");
        IReadOnlyList<ReportLineViewModel> inputs = ParseInputs(root);
        IReadOnlyList<ReportLineViewModel> operations = ParseOperations(root);
        IReadOnlyList<ReportLineViewModel> mutations = ParseMutations(root);
        IReadOnlyList<ReportLineViewModel> outputDifferences = ParseOutputDifferences(root);
        IReadOnlyList<ReportLineViewModel> issues = ParseIssues(root);
        string status = CreateStatus(issues);

        return new ReportReviewViewModel(
            false,
            sourceName,
            profileId,
            icId,
            modeId,
            experienceId,
            compositionKind,
            runId,
            startedAt,
            $"{profileId} ({icId})",
            $"{compositionKind} / {experienceId} / {Shorten(runId, 18)} / {startedAt}",
            status,
            ParseOutput(root),
            outputFileName,
            outputSize,
            outputSha256,
            outputArtifactPath ?? string.Empty,
            inputs,
            operations,
            mutations,
            outputDifferences,
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
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "Report could not be loaded",
            sourceName,
            status,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            [],
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

    private static string CreateStatus(IReadOnlyList<ReportLineViewModel> issues)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? warningCount == 0
                ? "Succeeded"
                : string.Create(CultureInfo.InvariantCulture, $"Succeeded with {warningCount} warning(s)")
            : warningCount == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{blockingIssueCount} issue(s)")
            : string.Create(CultureInfo.InvariantCulture, $"{blockingIssueCount} issue(s), {warningCount} warning(s)");
    }

    private static int CountBlockingIssues(IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count(issue => !IsWarning(issue));
    }

    private static int CountWarnings(IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count(IsWarning);
    }

    private static bool IsWarning(ReportLineViewModel issue)
    {
        return string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Severity, "info", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrWhiteSpace(issue.Severity) &&
                string.Equals(issue.Title, "input.address-space.truncated", StringComparison.Ordinal));
    }

    private static string FormatWarningMeta(int warningCount)
    {
        return warningCount == 0
            ? "No blocking issue"
            : string.Create(CultureInfo.InvariantCulture, $"{warningCount} warning(s)");
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
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        int warningCount = CountWarnings(issues);
        return
        [
            new ReportLineViewModel("Status", status, firstBlockingIssue?.Title ?? FormatWarningMeta(warningCount)),
            new ReportLineViewModel("Inputs", inputs.Count.ToString(CultureInfo.InvariantCulture), "files"),
            new ReportLineViewModel("Steps", operations.Count.ToString(CultureInfo.InvariantCulture), commandCount == 0 ? "operations" : $"{commandCount} command(s)"),
            new ReportLineViewModel("Mutations", mutations.Count.ToString(CultureInfo.InvariantCulture), output),
        ];
    }

    private static string CreateOutcomeTitle(string status, IReadOnlyList<ReportLineViewModel> issues)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? status
            : string.Equals(status, "Load failed", StringComparison.Ordinal)
            ? "Report load failed"
            : warningCount == 0 ? "Needs attention" : "Needs attention with warnings";
    }

    private static string CreateOutcomeDetail(string output, IReadOnlyList<ReportLineViewModel> issues)
    {
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        return blockingIssueCount == 0
            ? warningCount == 0
                ? "No reported issues. The detailed sections below are audit evidence, not required reading."
                : "The run completed, but review the warning before treating the output as final evidence."
            : string.IsNullOrWhiteSpace(output)
            ? "The run did not produce an output artifact. Start with the first issue below."
            : "Start with the first issue below, then use the evidence map to verify the related inputs, operations, and output.";
    }

    private static string CreateOutcomeMeta(IReadOnlyList<ReportLineViewModel> issues)
    {
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        return firstBlockingIssue?.Meta ?? FormatWarningMeta(CountWarnings(issues));
    }

    private static IReadOnlyList<ReportLineViewModel> CreateTriageRows(
        string status,
        string output,
        IReadOnlyList<ReportLineViewModel> operations,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        ReportLineViewModel? primaryIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        ReportLineViewModel? firstWarning = issues.FirstOrDefault(IsWarning);
        return primaryIssue is not null
            ?
            [
                new ReportLineViewModel("1. First issue", primaryIssue.Title, primaryIssue.Meta),
                new ReportLineViewModel("2. Message", primaryIssue.Detail, "reason"),
                new ReportLineViewModel(
                    "3. Evidence",
                    commandCount > 0 ? "Combiner commands" : "Operation steps",
                    commandCount > 0 ? $"{commandCount} external command(s)" : $"{operations.Count} operation(s)"),
            ]
            : firstWarning is not null
            ?
            [
                new ReportLineViewModel("1. Result", status, "No blocking issue"),
                new ReportLineViewModel("2. Warning", firstWarning.Title, firstWarning.Meta),
                new ReportLineViewModel(
                    "3. Evidence",
                    commandCount > 0 ? "Combiner commands available" : "Operation trace available",
                    commandCount > 0 ? $"{commandCount} external command(s)" : $"{operations.Count} operation(s)"),
            ]
            :
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
        IReadOnlyList<ReportLineViewModel> outputDifferences,
        IReadOnlyList<ReportLineViewModel> issues)
    {
        int commandCount = operations.Count(operation => operation.HasCodeBlock);
        int stepCount = operations.Count - commandCount;
        int blockingIssueCount = CountBlockingIssues(issues);
        int warningCount = CountWarnings(issues);
        ReportLineViewModel? firstBlockingIssue = issues.FirstOrDefault(issue => !IsWarning(issue));
        return
        [
            new ReportLineViewModel(
                "Issues",
                blockingIssueCount.ToString(CultureInfo.InvariantCulture),
                firstBlockingIssue?.Title ?? "No blocking issue"),
            new ReportLineViewModel(
                "Warnings",
                warningCount.ToString(CultureInfo.InvariantCulture),
                warningCount == 0 ? "No warning" : issues.First(IsWarning).Title),
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
            new ReportLineViewModel(
                "Output diff",
                outputDifferences.Count.ToString(CultureInfo.InvariantCulture),
                CreateOutputDifferenceMeta(outputDifferences)),
        ];
    }

    private static string CreateOutputDifferenceMeta(IReadOnlyList<ReportLineViewModel> outputDifferences)
    {
        if (outputDifferences.Count == 0)
        {
            return "same as base or non-Replace";
        }

        int acceptedCount = outputDifferences.Count(
            difference => difference.Badges.Any(
                badge => string.Equals(badge.Text, "accepted", StringComparison.OrdinalIgnoreCase)));
        int reviewCount = outputDifferences.Count - acceptedCount;
        return reviewCount == 0
            ? "all accepted"
            : $"{acceptedCount.ToString(CultureInfo.InvariantCulture)} accepted / {reviewCount.ToString(CultureInfo.InvariantCulture)} review";
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
        return $"{GetString(output, "FileName")} / {GetLong(output, "Size")} bytes / {committed} / {Shorten(GetString(output, "Sha256"), 16)}";
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

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }

    private static string Shorten(string text, int keep)
    {
        return text.Length <= keep ? text : $"{text[..keep]}...";
    }
}
