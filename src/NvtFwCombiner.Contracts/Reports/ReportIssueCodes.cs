namespace NvtFwCombiner.Contracts.Reports;

/// <summary>Stable report issue codes emitted by report generation and consumed by review surfaces.</summary>
public static class ReportIssueCodes
{
    /// <summary>Replace final output differs outside accepted replacement or postbuild refresh ranges.</summary>
    public const string UnexpectedOutputDifference = "report.output-difference.unexpected";
}
