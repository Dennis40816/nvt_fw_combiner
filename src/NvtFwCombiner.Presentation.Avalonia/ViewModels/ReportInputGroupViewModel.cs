using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class ReportInputGroupViewModel(
    string title,
    string detail,
    IReadOnlyList<ReportLineViewModel> rows,
    string sizeLabel,
    string addressSpaceLabel)
{
    public string Title { get; } = title;

    public string Detail { get; } = detail;

    public string Count => Rows.Count.ToString(CultureInfo.InvariantCulture);

    public string SizeLabel { get; } = sizeLabel ?? string.Empty;

    /// <summary>Column label for input address space.</summary>
    public string AddressSpaceLabel { get; } = addressSpaceLabel ?? string.Empty;

    public IReadOnlyList<ReportLineViewModel> Rows { get; } =
        rows ?? throw new ArgumentNullException(nameof(rows));
}
