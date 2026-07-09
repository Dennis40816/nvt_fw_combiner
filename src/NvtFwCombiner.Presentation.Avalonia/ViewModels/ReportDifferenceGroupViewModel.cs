using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Grouped output-difference rows for readable report change review.</summary>
public sealed class ReportDifferenceGroupViewModel
{
    /// <summary>Creates an output-difference group.</summary>
    public ReportDifferenceGroupViewModel(
        string title,
        string reason,
        string status,
        IReadOnlyList<ReportLineViewModel> rows,
        bool hasSharedReason,
        bool isAccepted)
    {
        Title = title ?? string.Empty;
        Reason = reason ?? string.Empty;
        Status = status ?? string.Empty;
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Count = Rows.Count.ToString(CultureInfo.InvariantCulture);
        HasSharedReason = hasSharedReason && !string.IsNullOrWhiteSpace(Reason);
        NeedsRowReasons = !HasSharedReason;
        IsAccepted = isAccepted;
        IsReviewRequired = !isAccepted;
    }

    /// <summary>Human-readable section title.</summary>
    public string Title { get; }

    /// <summary>One-line reason shared by rows in this section.</summary>
    public string Reason { get; }

    /// <summary>Accepted/review status summary for the section.</summary>
    public string Status { get; }

    /// <summary>Number of rows in this section.</summary>
    public string Count { get; }

    /// <summary>True when every row can share one section-level reason.</summary>
    public bool HasSharedReason { get; }

    /// <summary>True when rows need their own reasons.</summary>
    public bool NeedsRowReasons { get; }

    /// <summary>True when every row in this section is accepted.</summary>
    public bool IsAccepted { get; }

    /// <summary>True when at least one row in this section needs review.</summary>
    public bool IsReviewRequired { get; }

    /// <summary>Difference rows in this section.</summary>
    public IReadOnlyList<ReportLineViewModel> Rows { get; }
}
