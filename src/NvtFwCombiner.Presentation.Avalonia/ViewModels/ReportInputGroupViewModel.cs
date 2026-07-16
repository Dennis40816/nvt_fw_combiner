using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Grouped input rows for a readable report inputs section.</summary>
public sealed class ReportInputGroupViewModel(
    string title,
    string detail,
    IReadOnlyList<ReportLineViewModel> rows,
    string sizeLabel,
    string addressSpaceLabel)
{
    /// <summary>Group title.</summary>
    public string Title { get; } = title;

    /// <summary>Group explanation.</summary>
    public string Detail { get; } = detail;

    /// <summary>Number of rows in this group.</summary>
    public string Count => Rows.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>Column label for input byte size.</summary>
    public string SizeLabel { get; } = sizeLabel ?? string.Empty;

    /// <summary>Column label for input address space.</summary>
    public string AddressSpaceLabel { get; } = addressSpaceLabel ?? string.Empty;

    /// <summary>Grouped input rows.</summary>
    public IReadOnlyList<ReportLineViewModel> Rows { get; } =
        rows ?? throw new ArgumentNullException(nameof(rows));
}
