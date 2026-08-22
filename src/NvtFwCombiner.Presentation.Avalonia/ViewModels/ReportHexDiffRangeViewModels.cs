using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

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
internal sealed partial class ReportHexDiffRangeViewModel : ObservableObject
{
    internal ReportHexDiffRangeViewModel(
        ReportHexDiffRangeDescriptor descriptor,
        ReportLineViewModel detail,
        string outputSpaceId,
        ShellLanguage language,
        OutputDifferenceReplaySegment? replay = null)
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
        AccessibleLabel = string.Join("; ", Status, Title, AccessibleRange, ChangedSummary);
        Replay = replay;
        ReplayCoverage = replay is not null
            ? language == ShellLanguage.ChineseTraditional
                ? "Report 已保留完整變更區段與前後最多各兩列對齊 context，可重現此 viewport。"
                : "The report retains the complete changed range and up to two aligned context rows on each side."
            : language == ShellLanguage.ChineseTraditional
                ? "此 Report 未保留完整 replay bytes；byte viewport 不可用。"
                : "This report does not retain complete replay bytes; the byte viewport is unavailable.";
    }

    internal ReportHexDiffRangeDescriptor Descriptor { get; }

    /// <summary>Underlying report detail without Presentation-derived firmware meaning.</summary>
    public ReportLineViewModel Detail { get; }

    public string Title => Detail.Title;

    public string Reason => Detail.Reason;

    public string Status => Detail.Badges.Count > 0 ? Detail.Badges[0].Text : string.Empty;

    public string ChangedSummary => Detail.ChangedSummary;

    /// <summary>Compiled mutable address space containing this half-open range.</summary>
    public string OutputSpaceId { get; }

    public long Start => Descriptor.Start;

    /// <summary>Number of bytes in the half-open range.</summary>
    public long Length => Descriptor.Length;

    /// <summary>Exclusive end output-space offset.</summary>
    public long EndExclusive => Descriptor.EndExclusive;

    public bool IsAccepted => Descriptor.IsAccepted;

    /// <summary>True when a reviewer must inspect this range before release.</summary>
    public bool IsReviewRequired => !IsAccepted;

    /// <summary>True when persisted before/output bytes can reproduce this selected viewport.</summary>
    public bool HasReplay => Replay is not null;

    /// <summary>Honest replay availability for this report range.</summary>
    public string ReplayCoverage { get; }

    internal OutputDifferenceReplaySegment? Replay { get; }

    /// <summary>Address-space-qualified accessible range label.</summary>
    public string AccessibleRange { get; }

    /// <summary>Compact half-open output range for the visual navigator card.</summary>
    public string DisplayRange => string.Create(
        CultureInfo.InvariantCulture,
        $"[0x{Start:X6}, 0x{EndExclusive:X6})");

    /// <summary>Composite subject, verdict, range, and changed-count label for assistive navigation.</summary>
    public string AccessibleLabel { get; }

    /// <summary>True when this range is synchronized with the viewport and information panel.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; internal set; }
}

internal sealed class ReportHexDiffSource
{
    private readonly ReportHexDiffRangeDescriptor[] _descriptors;
    private readonly int[] _addressOrder;
    private readonly MemoizedIndexedReadOnlyList<ReportHexDiffRangeViewModel> _rows;

    internal static ReportHexDiffSource Empty { get; } = new(
        [],
        "reported-output",
        static _ => throw new InvalidOperationException("An empty Hex Diff has no ranges."));

    internal ReportHexDiffSource(
        IReadOnlyList<ReportHexDiffRangeDescriptor> descriptors,
        string outputSpaceId,
        Func<int, ReportHexDiffRangeViewModel> rowFactory)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(rowFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSpaceId);

        _descriptors = [.. descriptors];
        OutputSpaceId = outputSpaceId;
        if (_descriptors.Where((descriptor, index) => descriptor.SourceIndex != index).Any())
        {
            throw new ArgumentException("Hex Diff descriptors must preserve report source order.", nameof(descriptors));
        }

        int[] reviewOrder = [.. Enumerable.Range(0, _descriptors.Length)];
        Array.Sort(reviewOrder, CompareReviewOrder);
        _rows = new MemoizedIndexedReadOnlyList<ReportHexDiffRangeViewModel>(
            _descriptors.Length,
            rowFactory);
        NavigatorRows = new IndexedReadOnlyList<ReportHexDiffRangeViewModel>(_rows, reviewOrder);

        _addressOrder = [.. Enumerable.Range(0, _descriptors.Length)];
        Array.Sort(_addressOrder, CompareAddressOrder);
    }

    internal IReadOnlyList<ReportHexDiffRangeViewModel> NavigatorRows { get; }

    internal string OutputSpaceId { get; }

    internal int Count => _descriptors.Length;

    internal int MaterializedCount => _rows.MaterializedCount;

    internal bool Contains(ReportHexDiffRangeViewModel range)
    {
        ArgumentNullException.ThrowIfNull(range);
        int sourceIndex = range.Descriptor.SourceIndex;
        return (uint)sourceIndex < (uint)_rows.Count &&
            ReferenceEquals(_rows[sourceIndex], range);
    }

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
