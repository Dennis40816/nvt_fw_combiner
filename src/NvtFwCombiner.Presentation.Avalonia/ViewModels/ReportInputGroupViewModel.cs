using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Grouped input rows for a readable report inputs section.</summary>
public sealed class ReportInputGroupViewModel
{
    /// <summary>Creates an input group.</summary>
    public ReportInputGroupViewModel(
        string title,
        string detail,
        IReadOnlyList<ReportLineViewModel> rows)
    {
        Title = title;
        Detail = detail;
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Count = Rows.Count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Group title.</summary>
    public string Title { get; }

    /// <summary>Group explanation.</summary>
    public string Detail { get; }

    /// <summary>Number of rows in this group.</summary>
    public string Count { get; }

    /// <summary>Grouped input rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Rows { get; }
}
