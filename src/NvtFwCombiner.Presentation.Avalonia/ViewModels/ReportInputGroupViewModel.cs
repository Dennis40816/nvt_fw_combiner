using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Grouped input rows for a readable report inputs section.</summary>
public sealed class ReportInputGroupViewModel
{
    /// <summary>Creates an input group.</summary>
    public ReportInputGroupViewModel(
        string title,
        string detail,
        IReadOnlyList<ReportLineViewModel> rows,
        string inputLabel,
        string roleLabel,
        string sizeLabel,
        string addressSpaceLabel)
    {
        Title = title;
        Detail = detail;
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Count = Rows.Count.ToString(CultureInfo.InvariantCulture);
        InputLabel = inputLabel ?? string.Empty;
        RoleLabel = roleLabel ?? string.Empty;
        SizeLabel = sizeLabel ?? string.Empty;
        AddressSpaceLabel = addressSpaceLabel ?? string.Empty;
    }

    /// <summary>Group title.</summary>
    public string Title { get; }

    /// <summary>Group explanation.</summary>
    public string Detail { get; }

    /// <summary>Number of rows in this group.</summary>
    public string Count { get; }

    /// <summary>Column label for input identity.</summary>
    public string InputLabel { get; }

    /// <summary>Column label for input role.</summary>
    public string RoleLabel { get; }

    /// <summary>Column label for input byte size.</summary>
    public string SizeLabel { get; }

    /// <summary>Column label for input address space.</summary>
    public string AddressSpaceLabel { get; }

    /// <summary>Grouped input rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Rows { get; }
}
