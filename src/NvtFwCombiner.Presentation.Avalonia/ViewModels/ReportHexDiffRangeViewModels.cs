using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal readonly record struct ReportHexDiffRangeDescriptor(
    int SourceIndex,
    long Start,
    long Length,
    long ReportedEndExclusive,
    long ChangedByteCount,
    bool IsAccepted)
{
    internal long EndExclusive => checked(Start + Length);
}

/// <summary>One report-owned difference range in the compiled output address space.</summary>
public sealed class ReportHexDiffRangeViewModel
{
    internal ReportHexDiffRangeViewModel(
        ReportHexDiffRangeDescriptor descriptor,
        ReportLineViewModel detail,
        string outputSpaceId,
        ShellLanguage language,
        string evidence,
        string beforeSha256,
        string afterSha256)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSpaceId);
        Descriptor = descriptor;
        Detail = detail;
        OutputSpaceId = outputSpaceId;
        AccessibleRange = language == ShellLanguage.ChineseTraditional
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{OutputSpaceId} 0x{Start:X}-0x{EndExclusive:X} 半開區間")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{OutputSpaceId} 0x{Start:X}-0x{EndExclusive:X} half-open range");
        Evidence = evidence ?? string.Empty;
        BeforeSha256 = beforeSha256 ?? string.Empty;
        AfterSha256 = afterSha256 ?? string.Empty;
    }

    internal ReportHexDiffRangeDescriptor Descriptor { get; }

    /// <summary>Underlying report detail without Presentation-derived firmware meaning.</summary>
    public ReportLineViewModel Detail { get; }

    /// <summary>Application/report-owned field or difference title.</summary>
    public string Title => Detail.Title;

    /// <summary>Application/report-owned modification reason.</summary>
    public string Reason => Detail.Reason;

    /// <summary>Application/report-owned section label.</summary>
    public string SectionLabel => Detail.SectionLabel;

    /// <summary>Machine-readable report classification.</summary>
    public string Classification => Detail.Classification;

    /// <summary>Localized expected/review label already projected from the report.</summary>
    public string Status => Detail.Badges.Count > 0 ? Detail.Badges[0].Text : string.Empty;

    /// <summary>Readable half-open range summary.</summary>
    public string RangeLabel => Detail.Range;

    /// <summary>Readable changed-byte count.</summary>
    public string ChangedSummary => Detail.ChangedSummary;

    /// <summary>Compiled mutable address space containing this half-open range.</summary>
    public string OutputSpaceId { get; }

    /// <summary>First output-space offset.</summary>
    public long Start => Descriptor.Start;

    /// <summary>Number of bytes in the half-open range.</summary>
    public long Length => Descriptor.Length;

    /// <summary>Exclusive end output-space offset.</summary>
    public long EndExclusive => Descriptor.EndExclusive;

    /// <summary>True when Application/report policy accepted this difference.</summary>
    public bool IsAccepted => Descriptor.IsAccepted;

    /// <summary>True when a reviewer must inspect this range before release.</summary>
    public bool IsReviewRequired => !IsAccepted;

    /// <summary>Typed report evidence id.</summary>
    public string Evidence { get; }

    /// <summary>True when a typed evidence id was recorded.</summary>
    public bool HasEvidence => !string.IsNullOrWhiteSpace(Evidence);

    /// <summary>Reference-range SHA-256.</summary>
    public string BeforeSha256 { get; }

    /// <summary>Output-range SHA-256.</summary>
    public string AfterSha256 { get; }

    /// <summary>Address-space-qualified accessible range label.</summary>
    public string AccessibleRange { get; }
}

internal sealed class ReportHexDiffSource
{
    private readonly ReportHexDiffRangeDescriptor[] _descriptors;
    private readonly int[] _addressOrder;
    private readonly MemoizedIndexedReadOnlyList<ReportHexDiffRangeViewModel> _sourceRows;

    internal static ReportHexDiffSource Empty { get; } = new(
        [],
        new MemoizedIndexedReadOnlyList<ReportHexDiffRangeViewModel>(
            0,
            static _ => throw new InvalidOperationException("An empty Hex Diff has no ranges.")));

    internal ReportHexDiffSource(
        IReadOnlyList<ReportHexDiffRangeDescriptor> descriptors,
        MemoizedIndexedReadOnlyList<ReportHexDiffRangeViewModel> sourceRows)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(sourceRows);
        if (descriptors.Count != sourceRows.Count)
        {
            throw new ArgumentException("Hex Diff descriptor and row counts must match.", nameof(descriptors));
        }

        _descriptors = [.. descriptors];
        _sourceRows = sourceRows;
        if (_descriptors.Where((descriptor, index) => descriptor.SourceIndex != index).Any())
        {
            throw new ArgumentException("Hex Diff descriptors must preserve report source order.", nameof(descriptors));
        }

        int[] reviewOrder = [.. Enumerable.Range(0, _descriptors.Length)];
        Array.Sort(reviewOrder, CompareReviewOrder);
        NavigatorRows = new IndexedReadOnlyList<ReportHexDiffRangeViewModel>(_sourceRows, reviewOrder);

        _addressOrder = [.. Enumerable.Range(0, _descriptors.Length)];
        Array.Sort(_addressOrder, CompareAddressOrder);
    }

    internal IReadOnlyList<ReportHexDiffRangeViewModel> NavigatorRows { get; }

    internal int Count => _descriptors.Length;

    internal int MaterializedCount => _sourceRows.MaterializedCount;

    internal bool IsWithin(long byteLength)
    {
        if (byteLength < 0)
        {
            return false;
        }

        foreach (ReportHexDiffRangeDescriptor descriptor in _descriptors)
        {
            if (descriptor.Start < 0 || descriptor.Length <= 0 ||
                descriptor.ChangedByteCount <= 0 || descriptor.ChangedByteCount > descriptor.Length ||
                descriptor.Start > byteLength - descriptor.Length ||
                descriptor.ReportedEndExclusive != descriptor.EndExclusive)
            {
                return false;
            }
        }

        long priorEndExclusive = 0;
        foreach (int sourceIndex in _addressOrder)
        {
            ReportHexDiffRangeDescriptor descriptor = _descriptors[sourceIndex];
            if (descriptor.Start < priorEndExclusive)
            {
                return false;
            }

            priorEndExclusive = descriptor.EndExclusive;
        }

        return true;
    }

    internal ReportHexDiffRangeViewModel? FindContaining(long offset)
    {
        if (_addressOrder.Length == 0)
        {
            return null;
        }

        int low = 0;
        int high = _addressOrder.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (_descriptors[_addressOrder[middle]].Start <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int prior = low - 1;
        if (prior >= 0)
        {
            ReportHexDiffRangeDescriptor candidate = _descriptors[_addressOrder[prior]];
            if (offset < candidate.EndExclusive)
            {
                return _sourceRows[candidate.SourceIndex];
            }
        }

        return null;
    }

    private int CompareReviewOrder(int leftIndex, int rightIndex)
    {
        ReportHexDiffRangeDescriptor left = _descriptors[leftIndex];
        ReportHexDiffRangeDescriptor right = _descriptors[rightIndex];
        int acceptance = left.IsAccepted.CompareTo(right.IsAccepted);
        return acceptance != 0 ? acceptance : CompareAddress(left, right);
    }

    private int CompareAddressOrder(int leftIndex, int rightIndex)
    {
        return CompareAddress(_descriptors[leftIndex], _descriptors[rightIndex]);
    }

    private static int CompareAddress(
        ReportHexDiffRangeDescriptor left,
        ReportHexDiffRangeDescriptor right)
    {
        int start = left.Start.CompareTo(right.Start);
        return start != 0 ? start : left.SourceIndex.CompareTo(right.SourceIndex);
    }
}
