namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact line in the loaded run report view.</summary>
public sealed class ReportLineViewModel
{
    /// <summary>Empty line used when an optional report section has no data.</summary>
    public static ReportLineViewModel Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    /// <summary>Creates a report line.</summary>
    public ReportLineViewModel(string title, string detail, string meta, string codeBlock = "")
    {
        Title = title;
        Detail = detail;
        Meta = meta;
        CodeBlock = codeBlock;
        HasCodeBlock = !string.IsNullOrWhiteSpace(codeBlock);
    }

    /// <summary>Primary line text.</summary>
    public string Title { get; }

    /// <summary>Secondary line text.</summary>
    public string Detail { get; }

    /// <summary>Small metadata line.</summary>
    public string Meta { get; }

    /// <summary>Optional fixed-width command or technical block associated with this line.</summary>
    public string CodeBlock { get; }

    /// <summary>True when a fixed-width code block should be rendered for this line.</summary>
    public bool HasCodeBlock { get; }
}
