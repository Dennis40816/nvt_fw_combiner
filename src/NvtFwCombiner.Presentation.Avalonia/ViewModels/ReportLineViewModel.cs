namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class ReportLineViewModel(
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
    public static ReportLineViewModel Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    public string Title { get; } = title;

    public string Detail { get; } = detail;

    public string Meta { get; } = meta;

    public string OperationKind { get; } = operationKind ?? string.Empty;

    public string OperationSource { get; } = operationSource ?? string.Empty;

    public string OperationTarget { get; } = operationTarget ?? string.Empty;

    /// <summary>External processor or tool binding rendered in the report operation table.</summary>
    public string OperationProcessor { get; } = operationProcessor ?? string.Empty;

    public string OperationStatus { get; } = operationStatus ?? string.Empty;

    public string Severity { get; } = severity ?? string.Empty;

    public string Classification { get; } = classification ?? string.Empty;

    public bool IsAccepted { get; } = isAccepted;

    /// <summary>Human-readable range rendered in report comparison tables.</summary>
    public string Range { get; } = range ?? string.Empty;

    /// <summary>Short changed-byte summary rendered beside a range.</summary>
    public string ChangedSummary { get; } = changedSummary ?? string.Empty;

    public string Reason { get; } = reason ?? string.Empty;

    public string SectionLabel { get; } = sectionLabel ?? string.Empty;

    /// <summary>Label for the before comparison value.</summary>
    public string BeforeLabel { get; } = beforeLabel ?? string.Empty;

    /// <summary>Before comparison value, usually hex bytes or a range hash.</summary>
    public string BeforeValue { get; } = beforeValue ?? string.Empty;

    /// <summary>Label for the after comparison value.</summary>
    public string AfterLabel { get; } = afterLabel ?? string.Empty;

    /// <summary>After comparison value, usually hex bytes or a range hash.</summary>
    public string AfterValue { get; } = afterValue ?? string.Empty;

    public string InputRole { get; } = inputRole ?? string.Empty;

    public string InputSizeLabel { get; } = inputSizeLabel ?? string.Empty;

    /// <summary>Input address space rendered in the report input table.</summary>
    public string InputAddressSpace { get; } = inputAddressSpace ?? string.Empty;

    /// <summary>True when the row has a before/after comparison block.</summary>
    public bool HasBeforeAfter => !string.IsNullOrWhiteSpace(BeforeValue) || !string.IsNullOrWhiteSpace(AfterValue);

    public bool HasInputFields =>
        !string.IsNullOrWhiteSpace(InputRole) ||
        !string.IsNullOrWhiteSpace(InputSizeLabel) ||
        !string.IsNullOrWhiteSpace(InputAddressSpace);

    /// <summary>True when the input detail adds information beyond the address-space id.</summary>
    public bool HasInputDetail =>
        HasInputFields &&
        !string.IsNullOrWhiteSpace(Detail) &&
        !string.Equals(Detail, InputAddressSpace, StringComparison.Ordinal);

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    /// <summary>True when the row has a dedicated range value.</summary>
    public bool HasRange => !string.IsNullOrWhiteSpace(Range);

    public bool HasChangedSummary => !string.IsNullOrWhiteSpace(ChangedSummary);

    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    public string CodeBlock { get; } = codeBlock;

    public string CodeBlockLabel { get; } = codeBlockLabel ?? string.Empty;

    public bool HasCodeBlock => !string.IsNullOrWhiteSpace(CodeBlock);

    public IReadOnlyList<ReportRuntimeCommandViewModel> RuntimeCommands { get; } =
        runtimeCommands is null ? [] : [.. runtimeCommands];

    /// <summary>True when the report contains completed runtime process invocation evidence.</summary>
    public bool HasRuntimeCommands => RuntimeCommands.Count > 0;

    public IReadOnlyList<ReportLineBadgeViewModel> Badges { get; } = badges is null ? [] : [.. badges];

    public bool HasBadges => Badges.Count > 0;

    public IReadOnlyList<ReportLineFactViewModel> Facts { get; } = facts is null ? [] : [.. facts];

    public bool HasFacts => Facts.Count > 0;

    /// <summary>Structured source, target, and processor range evidence rows.</summary>
    public IReadOnlyList<ReportRangeTableRowViewModel> RangeRows { get; } =
        rangeRows is null ? [] : [.. rangeRows];

    /// <summary>True when the line has range evidence rows.</summary>
    public bool HasRangeRows => RangeRows.Count > 0;
}
