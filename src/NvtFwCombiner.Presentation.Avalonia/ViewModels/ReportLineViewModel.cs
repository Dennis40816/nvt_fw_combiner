namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact line in the loaded run report view.</summary>
public sealed class ReportLineViewModel
{
    /// <summary>Empty line used when an optional report section has no data.</summary>
    public static ReportLineViewModel Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    /// <summary>Creates a report line.</summary>
    public ReportLineViewModel(
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
        bool isAccepted = false)
    {
        Title = title;
        Detail = detail;
        Meta = meta;
        OperationKind = operationKind ?? string.Empty;
        OperationSource = operationSource ?? string.Empty;
        OperationTarget = operationTarget ?? string.Empty;
        OperationProcessor = operationProcessor ?? string.Empty;
        OperationStatus = operationStatus ?? string.Empty;
        Severity = severity ?? string.Empty;
        Classification = classification ?? string.Empty;
        IsAccepted = isAccepted;
        CodeBlock = codeBlock;
        HasCodeBlock = !string.IsNullOrWhiteSpace(codeBlock);
        Badges = badges is null ? [] : [.. badges];
        HasBadges = Badges.Count > 0;
        Facts = facts is null ? [] : [.. facts];
        HasFacts = Facts.Count > 0;
        RangeRows = rangeRows is null ? [] : [.. rangeRows];
        HasRangeRows = RangeRows.Count > 0;
    }

    /// <summary>Primary line text.</summary>
    public string Title { get; }

    /// <summary>Secondary line text.</summary>
    public string Detail { get; }

    /// <summary>Small metadata line.</summary>
    public string Meta { get; }

    /// <summary>Operation kind rendered in the report operation table.</summary>
    public string OperationKind { get; }

    /// <summary>Operation source rendered in the report operation table.</summary>
    public string OperationSource { get; }

    /// <summary>Operation target rendered in the report operation table.</summary>
    public string OperationTarget { get; }

    /// <summary>External processor or tool binding rendered in the report operation table.</summary>
    public string OperationProcessor { get; }

    /// <summary>Operation status rendered in the report operation table.</summary>
    public string OperationStatus { get; }

    /// <summary>Optional issue severity for report diagnostics.</summary>
    public string Severity { get; }

    /// <summary>Optional output-difference classification.</summary>
    public string Classification { get; }

    /// <summary>True when an output difference was classified as accepted by the report.</summary>
    public bool IsAccepted { get; }

    /// <summary>True when the metadata line contains text.</summary>
    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    /// <summary>Optional fixed-width command or technical block associated with this line.</summary>
    public string CodeBlock { get; }

    /// <summary>True when a fixed-width code block should be rendered for this line.</summary>
    public bool HasCodeBlock { get; }

    /// <summary>Compact status badges associated with this line.</summary>
    public IReadOnlyList<ReportLineBadgeViewModel> Badges { get; }

    /// <summary>True when the line has compact status badges.</summary>
    public bool HasBadges { get; }

    /// <summary>Structured traceability facts associated with this line.</summary>
    public IReadOnlyList<ReportLineFactViewModel> Facts { get; }

    /// <summary>True when the line has structured traceability facts.</summary>
    public bool HasFacts { get; }

    /// <summary>Structured source, target, and processor range evidence rows.</summary>
    public IReadOnlyList<ReportRangeTableRowViewModel> RangeRows { get; }

    /// <summary>True when the line has range evidence rows.</summary>
    public bool HasRangeRows { get; }
}

/// <summary>Small badge shown on dense report evidence rows.</summary>
public sealed class ReportLineBadgeViewModel
{
    /// <summary>Creates a badge.</summary>
    public ReportLineBadgeViewModel(string text)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>Badge text.</summary>
    public string Text { get; }
}

/// <summary>Label/value fact shown in report evidence rows.</summary>
public sealed class ReportLineFactViewModel
{
    /// <summary>Creates a report fact.</summary>
    public ReportLineFactViewModel(string label, string value, bool isTechnical = false)
    {
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
        IsTechnical = isTechnical;
        IsPlainText = !isTechnical;
    }

    /// <summary>Short field label.</summary>
    public string Label { get; }

    /// <summary>Field value.</summary>
    public string Value { get; }

    /// <summary>True when the value should use fixed-width technical typography.</summary>
    public bool IsTechnical { get; }

    /// <summary>True when the value should use normal typography.</summary>
    public bool IsPlainText { get; }
}

/// <summary>One table row for operation source/target and processor read/write ranges.</summary>
public sealed record ReportRangeTableRowViewModel(
    string Kind,
    string AddressSpace,
    string Range,
    string Source);

/// <summary>One row in the simplified byte-difference summary table.</summary>
public sealed record ReportDifferenceSummaryRowViewModel(
    string Label,
    string Count,
    string Status,
    string Detail);
