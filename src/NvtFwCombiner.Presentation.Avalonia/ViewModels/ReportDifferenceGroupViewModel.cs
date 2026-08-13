using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportDifferenceGroupViewModel : ObservableObject
{
    /// <summary>True when the UI has opened this section and requested its first bounded row page.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

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

    public string Title { get; }

    /// <summary>One-line physical-section summary shown before the field rows are expanded.</summary>
    public string Detail { get; }

    public string Status { get; }

    public string Count => RowsPage.TotalCount.ToString(CultureInfo.InvariantCulture);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool IsAccepted { get; }

    public bool IsReviewRequired => !IsAccepted;

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
