using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Grouped output-difference rows for readable report change review.</summary>
public sealed class ReportDifferenceGroupViewModel(
    string title,
    string detail,
    string status,
    IReadOnlyList<ReportLineViewModel> rows,
    bool isAccepted,
    ShellLanguage language)
{
    private readonly IReadOnlyList<ReportLineViewModel> _rows =
        rows ?? throw new ArgumentNullException(nameof(rows));

    /// <summary>Human-readable section title.</summary>
    public string Title { get; } = title ?? string.Empty;

    /// <summary>One-line physical-section summary shown before the field rows are expanded.</summary>
    public string Detail { get; } = detail ?? string.Empty;

    /// <summary>Accepted/review status summary for the section.</summary>
    public string Status { get; } = status ?? string.Empty;

    /// <summary>Number of rows in this section.</summary>
    public string Count => _rows.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>True when the group has a compact section summary.</summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>True when every row in this section is accepted.</summary>
    public bool IsAccepted { get; } = isAccepted;

    /// <summary>True when at least one row in this section needs review.</summary>
    public bool IsReviewRequired => !IsAccepted;

    /// <summary>Bounded difference rows materialized for this expanded section.</summary>
    public ReportPagedListViewModel RowsPage { get; } =
        ReportPagedListViewModel.Create(
            rows ?? throw new ArgumentNullException(nameof(rows)),
            pageSize: 24,
            language);
}
