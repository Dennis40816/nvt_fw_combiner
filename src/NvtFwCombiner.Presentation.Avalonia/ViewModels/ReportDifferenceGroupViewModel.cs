using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Grouped output-difference rows for readable report change review.</summary>
public sealed partial class ReportDifferenceGroupViewModel : ObservableObject
{
    /// <summary>True when the UI has opened this section and requested its first bounded row page.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Creates a grouped, lazily paged report-difference section.</summary>
    public ReportDifferenceGroupViewModel(
        string title,
        string detail,
        string status,
        IReadOnlyList<ReportLineViewModel> rows,
        bool isAccepted,
        ShellLanguage language = ShellLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Title = title ?? string.Empty;
        Detail = detail ?? string.Empty;
        Status = status ?? string.Empty;
        IsAccepted = isAccepted;
        Rows = rows;
        RowsPage = ReportPagedListViewModel.Create(Rows, pageSize: 24, language, loadInitialPage: false);
    }

    /// <summary>Human-readable section title.</summary>
    public string Title { get; }

    /// <summary>One-line physical-section summary shown before the field rows are expanded.</summary>
    public string Detail { get; }

    /// <summary>Accepted/review status summary for the section.</summary>
    public string Status { get; }

    /// <summary>Number of rows in this section.</summary>
    public string Count => RowsPage.TotalCount.ToString(CultureInfo.InvariantCulture);

    /// <summary>True when the group has a compact section summary.</summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>True when every row in this section is accepted.</summary>
    public bool IsAccepted { get; }

    /// <summary>True when at least one row in this section needs review.</summary>
    public bool IsReviewRequired => !IsAccepted;

    /// <summary>Complete ordered difference-row projection for non-visual audit consumers.</summary>
    public IReadOnlyList<ReportLineViewModel> Rows { get; }

    /// <summary>Bounded difference rows materialized for this expanded section.</summary>
    public ReportPagedListViewModel RowsPage { get; }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            RowsPage.EnsureInitialPage();
        }
    }
}
