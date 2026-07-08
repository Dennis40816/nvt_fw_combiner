namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One row in the simplified byte-difference summary table.</summary>
public sealed record ReportDifferenceSummaryRowViewModel(
    string Label,
    string Count,
    string Status,
    string Detail);
