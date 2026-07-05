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
        IEnumerable<ReportLineFactViewModel>? facts = null)
    {
        Title = title;
        Detail = detail;
        Meta = meta;
        CodeBlock = codeBlock;
        HasCodeBlock = !string.IsNullOrWhiteSpace(codeBlock);
        Badges = badges is null ? [] : [.. badges];
        HasBadges = Badges.Count > 0;
        Facts = facts is null ? [] : [.. facts];
        HasFacts = Facts.Count > 0;
    }

    /// <summary>Primary line text.</summary>
    public string Title { get; }

    /// <summary>Secondary line text.</summary>
    public string Detail { get; }

    /// <summary>Small metadata line.</summary>
    public string Meta { get; }

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
