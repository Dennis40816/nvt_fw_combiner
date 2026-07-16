namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact line in the loaded run report view.</summary>
public sealed class ReportLineViewModel(
    string title,
    string detail,
    string meta,
    string codeBlock = "",
    IEnumerable<ReportLineBadgeViewModel>? badges = null,
    IEnumerable<ReportLineFactViewModel>? facts = null,
    IEnumerable<ReportRangeTableRowViewModel>? rangeRows = null,
    string operationKind = "",
    string operationSource = "",
    string operationTarget = "",
    string operationProcessor = "",
    string operationStatus = "",
    string severity = "",
    string classification = "",
    bool isAccepted = false,
    string range = "",
    string changedSummary = "",
    string reason = "",
    string sectionLabel = "",
    string beforeLabel = "",
    string beforeValue = "",
    string afterLabel = "",
    string afterValue = "",
    string inputRole = "",
    string inputSizeLabel = "",
    string inputAddressSpace = "",
    string codeBlockLabel = "",
    IEnumerable<ReportRuntimeCommandViewModel>? runtimeCommands = null)
{
    /// <summary>Empty line used when an optional report section has no data.</summary>
    public static ReportLineViewModel Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    /// <summary>Primary line text.</summary>
    public string Title { get; } = title;

    /// <summary>Secondary line text.</summary>
    public string Detail { get; } = detail;

    /// <summary>Small metadata line.</summary>
    public string Meta { get; } = meta;

    /// <summary>Operation kind rendered in the report operation table.</summary>
    public string OperationKind { get; } = operationKind ?? string.Empty;

    /// <summary>Operation source rendered in the report operation table.</summary>
    public string OperationSource { get; } = operationSource ?? string.Empty;

    /// <summary>Operation target rendered in the report operation table.</summary>
    public string OperationTarget { get; } = operationTarget ?? string.Empty;

    /// <summary>External processor or tool binding rendered in the report operation table.</summary>
    public string OperationProcessor { get; } = operationProcessor ?? string.Empty;

    /// <summary>Operation status rendered in the report operation table.</summary>
    public string OperationStatus { get; } = operationStatus ?? string.Empty;

    /// <summary>Optional issue severity for report diagnostics.</summary>
    public string Severity { get; } = severity ?? string.Empty;

    /// <summary>Optional output-difference classification.</summary>
    public string Classification { get; } = classification ?? string.Empty;

    /// <summary>True when an output difference was classified as accepted by the report.</summary>
    public bool IsAccepted { get; } = isAccepted;

    /// <summary>Human-readable range rendered in report comparison tables.</summary>
    public string Range { get; } = range ?? string.Empty;

    /// <summary>Short changed-byte summary rendered beside a range.</summary>
    public string ChangedSummary { get; } = changedSummary ?? string.Empty;

    /// <summary>Human-readable reason for a report difference or operation.</summary>
    public string Reason { get; } = reason ?? string.Empty;

    /// <summary>Human-readable section label for output-difference rows.</summary>
    public string SectionLabel { get; } = sectionLabel ?? string.Empty;

    /// <summary>Label for the before comparison value.</summary>
    public string BeforeLabel { get; } = beforeLabel ?? string.Empty;

    /// <summary>Before comparison value, usually hex bytes or a range hash.</summary>
    public string BeforeValue { get; } = beforeValue ?? string.Empty;

    /// <summary>Label for the after comparison value.</summary>
    public string AfterLabel { get; } = afterLabel ?? string.Empty;

    /// <summary>After comparison value, usually hex bytes or a range hash.</summary>
    public string AfterValue { get; } = afterValue ?? string.Empty;

    /// <summary>Input role rendered in the report input table.</summary>
    public string InputRole { get; } = inputRole ?? string.Empty;

    /// <summary>Input size rendered in the report input table.</summary>
    public string InputSizeLabel { get; } = inputSizeLabel ?? string.Empty;

    /// <summary>Input address space rendered in the report input table.</summary>
    public string InputAddressSpace { get; } = inputAddressSpace ?? string.Empty;

    /// <summary>True when the row has a before/after comparison block.</summary>
    public bool HasBeforeAfter => !string.IsNullOrWhiteSpace(BeforeValue) || !string.IsNullOrWhiteSpace(AfterValue);

    /// <summary>True when the row has report-input table fields.</summary>
    public bool HasInputFields =>
        !string.IsNullOrWhiteSpace(InputRole) ||
        !string.IsNullOrWhiteSpace(InputSizeLabel) ||
        !string.IsNullOrWhiteSpace(InputAddressSpace);

    /// <summary>True when the input detail adds information beyond the address-space id.</summary>
    public bool HasInputDetail =>
        HasInputFields &&
        !string.IsNullOrWhiteSpace(Detail) &&
        !string.Equals(Detail, InputAddressSpace, StringComparison.Ordinal);

    /// <summary>True when the row has a user-facing reason.</summary>
    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    /// <summary>True when the row has a dedicated range value.</summary>
    public bool HasRange => !string.IsNullOrWhiteSpace(Range);

    /// <summary>True when the row has a changed-byte summary.</summary>
    public bool HasChangedSummary => !string.IsNullOrWhiteSpace(ChangedSummary);

    /// <summary>True when the metadata line contains text.</summary>
    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    /// <summary>Optional fixed-width command or technical block associated with this line.</summary>
    public string CodeBlock { get; } = codeBlock;

    /// <summary>Human-readable provenance label for the optional fixed-width code block.</summary>
    public string CodeBlockLabel { get; } = codeBlockLabel ?? string.Empty;

    /// <summary>True when a fixed-width code block should be rendered for this line.</summary>
    public bool HasCodeBlock => !string.IsNullOrWhiteSpace(CodeBlock);

    /// <summary>Completed external process invocations recorded by the runtime report.</summary>
    public IReadOnlyList<ReportRuntimeCommandViewModel> RuntimeCommands { get; } =
        runtimeCommands is null ? [] : [.. runtimeCommands];

    /// <summary>True when the report contains completed runtime process invocation evidence.</summary>
    public bool HasRuntimeCommands => RuntimeCommands.Count > 0;

    /// <summary>Compact status badges associated with this line.</summary>
    public IReadOnlyList<ReportLineBadgeViewModel> Badges { get; } = badges is null ? [] : [.. badges];

    /// <summary>True when the line has compact status badges.</summary>
    public bool HasBadges => Badges.Count > 0;

    /// <summary>Structured traceability facts associated with this line.</summary>
    public IReadOnlyList<ReportLineFactViewModel> Facts { get; } = facts is null ? [] : [.. facts];

    /// <summary>True when the line has structured traceability facts.</summary>
    public bool HasFacts => Facts.Count > 0;

    /// <summary>Structured source, target, and processor range evidence rows.</summary>
    public IReadOnlyList<ReportRangeTableRowViewModel> RangeRows { get; } =
        rangeRows is null ? [] : [.. rangeRows];

    /// <summary>True when the line has range evidence rows.</summary>
    public bool HasRangeRows => RangeRows.Count > 0;
}
