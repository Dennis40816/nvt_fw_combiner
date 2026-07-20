using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Bounded read-only projection of one verified Replace before/output snapshot.</summary>
public sealed partial class ReportHexDiffViewModel : ObservableObject
{
    private const int BytesPerRow = 16;
    private const int InitialVisibleRowCount = 48;
    private const int MaximumVisibleRowCount = 128;
    private readonly CompositionRunInspectionSnapshot? _snapshot;
    private readonly ReportHexDiffSource _source;
    private readonly ShellLanguage _language;
    private readonly bool _canInspectRanges;
    private readonly RelayCommand<ReportHexDiffRangeViewModel> _selectRangeCommand;
    private readonly RelayCommand _jumpAddressCommand;

    private ReportHexDiffViewModel(
        CompositionRunInspectionSnapshot? snapshot,
        ReportHexDiffSource source,
        ShellLanguage language,
        long reportOutputSize,
        bool hasVerifiedSnapshot,
        bool canInspectRanges,
        string availabilityDetail)
    {
        _snapshot = snapshot;
        _source = source;
        _language = language;
        _canInspectRanges = canInspectRanges;
        IsAvailable = hasVerifiedSnapshot;
        IsReportedRangeMode = !hasVerifiedSnapshot && canInspectRanges;
        AvailabilityTitle = hasVerifiedSnapshot
            ? T(language, "Complete Hex Diff", "完整 Hex Diff")
            : canInspectRanges
                ? T(language, "Reported-range Hex Diff", "Report 區段 Hex Diff")
                : T(language, "Hex Diff unavailable", "無法使用 Hex Diff");
        AvailabilityDetail = availabilityDetail;
        OutputSpaceId = snapshot?.OutputSpaceId ?? source.OutputSpaceId;
        ReferenceSpaceId = snapshot?.ReferenceSpaceId ?? "reported-reference";
        TotalByteCount = canInspectRanges ? checked((int)reportOutputSize) : 0;
        TotalRowCount = canInspectRanges ? checked((TotalByteCount + BytesPerRow - 1) / BytesPerRow) : 0;
        HasDifferenceWorkspace = source.Count > 0;
        NavigatorPage = ReportWindowedListViewModel.Create(
            source.NavigatorRows,
            pageSize: 64,
            language,
            loadInitialPage: canInspectRanges);
        NavigatorPage.PropertyChanged += NavigatorPage_OnPropertyChanged;
        _selectRangeCommand = new RelayCommand<ReportHexDiffRangeViewModel>(SelectRange, CanSelectRange);
        _jumpAddressCommand = new RelayCommand(JumpToAddress, () => canInspectRanges);

        if (canInspectRanges)
        {
            SelectedRange = NavigatorPage.Items.Count > 0
                ? (ReportHexDiffRangeViewModel)NavigatorPage.Items[0]
                : null;
            long initialOffset = SelectedRange?.Start ?? 0;
            JumpAddress = FormatAddress(initialOffset);
            ShowSelectedRows(initialOffset);
        }
    }

    /// <summary>True when verified full reference/output bytes match this report.</summary>
    public bool IsAvailable { get; }

    /// <summary>Short complete/fallback state.</summary>
    public string AvailabilityTitle { get; }

    /// <summary>Why complete inspection is or is not available.</summary>
    public string AvailabilityDetail { get; }

    /// <summary>True when the workspace is backed only by report-retained range previews.</summary>
    public bool IsReportedRangeMode { get; }

    /// <summary>True when at least one reported difference belongs in the sole Hex Diff workspace.</summary>
    public bool HasDifferenceWorkspace { get; }

    /// <summary>Application-owned compiled output address space.</summary>
    public string OutputSpaceId { get; }

    /// <summary>Application-owned canonical reference address space.</summary>
    public string ReferenceSpaceId { get; }

    /// <summary>Number of bytes in each verified comparison buffer.</summary>
    public int TotalByteCount { get; }

    /// <summary>Total logical 16-byte output rows; only a bounded window is materialized.</summary>
    public int TotalRowCount { get; }

    /// <summary>Currently materialized output/reference rows.</summary>
    public ReportHexDiffViewportRowCollection VisibleRows { get; } = [];

    /// <summary>Review-first, bounded range navigator.</summary>
    public ReportWindowedListViewModel NavigatorPage { get; }

    /// <summary>Selected page-external range kept visible without materializing preceding pages.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPinnedSelectedRange))]
    [NotifyPropertyChangedFor(nameof(VisibleNavigatorRowCount))]
    public partial ReportHexDiffRangeViewModel? PinnedSelectedRange { get; private set; }

    /// <summary>True when the selected range is outside the currently materialized navigator page.</summary>
    public bool HasPinnedSelectedRange => PinnedSelectedRange is not null;

    /// <summary>Bounded navigator control count, including at most one pinned selected range.</summary>
    public int VisibleNavigatorRowCount => NavigatorPage.VisibleCount + (HasPinnedSelectedRange ? 1 : 0);

    /// <summary>Number of navigator range-detail models retained from report JSON.</summary>
    internal int MaterializedRangeCount => VisibleNavigatorRowCount;

    /// <summary>Selected report-owned range.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRange))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedRange))]
    public partial ReportHexDiffRangeViewModel? SelectedRange { get; private set; }

    /// <summary>True when the information panel has one report-owned selection.</summary>
    public bool HasSelectedRange => SelectedRange is not null;

    /// <summary>True when the current output address is outside every reported difference.</summary>
    public bool HasNoSelectedRange => SelectedRange is null;

    /// <summary>First output-space offset currently materialized.</summary>
    [ObservableProperty]
    public partial long FirstVisibleOffset { get; private set; }

    /// <summary>Hex address entered by the reviewer.</summary>
    [ObservableProperty]
    public partial string JumpAddress { get; set; } = string.Empty;

    /// <summary>Bounded address-jump feedback.</summary>
    [ObservableProperty]
    public partial string JumpStatus { get; private set; } = string.Empty;

    /// <summary>Controls the optional verified reference row beneath each output row.</summary>
    [ObservableProperty]
    public partial bool ShowOriginalRows { get; set; }

    /// <summary>Selects and focuses one report-owned range.</summary>
    public IRelayCommand<ReportHexDiffRangeViewModel> SelectRangeCommand => _selectRangeCommand;

    /// <summary>Jumps to a checked output-space offset.</summary>
    public IRelayCommand JumpAddressCommand => _jumpAddressCommand;

    internal static ReportHexDiffViewModel Create(
        CompositionRunInspectionSnapshot? snapshot,
        ReportHexDiffSource source,
        string reportRunId,
        long reportOutputSize,
        string reportOutputSha256,
        ShellLanguage language)
    {
        ArgumentNullException.ThrowIfNull(source);
        bool reportBoundsValid = reportOutputSize is > 0 and <= int.MaxValue && source.IsWithin(reportOutputSize);
        bool identityMatches = snapshot is not null && reportBoundsValid &&
            string.Equals(snapshot.RunId, reportRunId, StringComparison.Ordinal) &&
            string.Equals(snapshot.OutputSha256, reportOutputSha256, StringComparison.OrdinalIgnoreCase) &&
            snapshot.OutputBytes.Length > 0 &&
            reportOutputSize == snapshot.OutputBytes.Length;
        if (identityMatches)
        {
            return new ReportHexDiffViewModel(
                snapshot,
                source,
                language,
                reportOutputSize,
                hasVerifiedSnapshot: true,
                canInspectRanges: true,
                T(
                    language,
                    $"Verified {snapshot!.OutputSpaceId} output against {snapshot.ReferenceSpaceId} from the same run.",
                    $"已用同一次執行的 {snapshot.ReferenceSpaceId} 驗證 {snapshot.OutputSpaceId} output。"));
        }

        string detail = reportBoundsValid
            ? snapshot is null
                ? T(
                    language,
                    "Historical report: only its stored before/output previews are shown; bytes beyond each preview are unavailable.",
                    "歷史 Report：僅顯示已儲存的變更前／輸出 preview；各 preview 以外的 bytes 不可用。")
                : T(
                    language,
                    "The in-session bytes do not match this report; only its stored before/output previews are shown.",
                    "目前 session bytes 與此 Report 不相符；僅顯示 Report 已儲存的變更前／輸出 preview。")
            : T(
                language,
                "The reported ranges do not fit the declared output bounds, so no byte view is exposed.",
                "已報告區段不符合宣告的輸出範圍，因此不顯示 byte view。 ");
        return new ReportHexDiffViewModel(
            null,
            source,
            language,
            reportOutputSize,
            hasVerifiedSnapshot: false,
            canInspectRanges: reportBoundsValid,
            detail);
    }

    private bool CanSelectRange(ReportHexDiffRangeViewModel? range)
    {
        return _canInspectRanges && range is not null &&
            string.Equals(range.OutputSpaceId, OutputSpaceId, StringComparison.Ordinal) &&
            range.Start >= 0 && range.Length > 0 && range.Start <= TotalByteCount - range.Length;
    }

    private void SelectRange(ReportHexDiffRangeViewModel? range)
    {
        if (!CanSelectRange(range))
        {
            JumpStatus = T(_language, "Range is outside the reported output space.", "Range 超出已報告的 output space。");
            return;
        }

        SelectedRange = range;
        JumpAddress = FormatAddress(range!.Start);
        JumpStatus = range.AccessibleRange;
        ShowSelectedRows(range.Start);
    }

    private void JumpToAddress()
    {
        if (!TryParseAddress(JumpAddress, out long offset) || offset < 0 || offset >= TotalByteCount)
        {
            JumpStatus = T(
                _language,
                $"Enter a 0x address inside {OutputSpaceId}.",
                $"請輸入 {OutputSpaceId} 範圍內的 0x address。");
            return;
        }

        SelectedRange = _source.FindContaining(offset);
        JumpStatus = SelectedRange is null
            ? T(
                _language,
                string.Create(CultureInfo.InvariantCulture, $"{OutputSpaceId} address 0x{offset:X}; no reported change contains this byte."),
                string.Create(CultureInfo.InvariantCulture, $"{OutputSpaceId} 位址 0x{offset:X}；此 byte 不在任何已報告變更內。"))
            : T(
                _language,
                string.Create(CultureInfo.InvariantCulture, $"{OutputSpaceId} address 0x{offset:X}"),
                string.Create(CultureInfo.InvariantCulture, $"{OutputSpaceId} 位址 0x{offset:X}"));
        if (IsAvailable)
        {
            ShowRowsAtOffset(offset, InitialVisibleRowCount);
            return;
        }

        if (SelectedRange is null)
        {
            VisibleRows.ReplaceAll([]);
            return;
        }

        if (IsReportedRangeMode && offset >= SelectedRange.Start + SelectedRange.PreviewByteCount)
        {
            JumpStatus = T(
                _language,
                string.Create(CultureInfo.InvariantCulture, $"Address 0x{offset:X} belongs to this range, but its byte was not retained in the report preview."),
                string.Create(CultureInfo.InvariantCulture, $"位址 0x{offset:X} 屬於此區段，但該 byte 未保留在 Report preview。"));
        }

        ShowPreviewRows(SelectedRange, offset);
    }

    private void ShowSelectedRows(long offset)
    {
        if (IsAvailable)
        {
            ShowRowsAtOffset(offset, InitialVisibleRowCount);
        }
        else if (SelectedRange is not null)
        {
            ShowPreviewRows(SelectedRange, offset);
        }
    }

    private void ShowPreviewRows(ReportHexDiffRangeViewModel range, long offset)
    {
        int available = range.PreviewByteCount;
        if (available == 0)
        {
            FirstVisibleOffset = range.Start;
            VisibleRows.ReplaceAll([]);
            return;
        }

        int relativeOffset = checked((int)Math.Clamp(offset - range.Start, 0, available - 1));
        int firstPreviewIndex = relativeOffset / BytesPerRow * BytesPerRow;
        FirstVisibleOffset = checked(range.Start + firstPreviewIndex);
        int rowCount = Math.Min(
            InitialVisibleRowCount,
            (available - firstPreviewIndex + BytesPerRow - 1) / BytesPerRow);
        var rows = new ReportHexDiffViewportRowViewModel[rowCount];
        ReadOnlySpan<byte> output = range.AfterPreview.Span;
        ReadOnlySpan<byte> reference = range.BeforePreview.Span;
        for (int index = 0; index < rowCount; index++)
        {
            int previewIndex = firstPreviewIndex + (index * BytesPerRow);
            int length = Math.Min(BytesPerRow, available - previewIndex);
            rows[index] = CreateRow(
                checked(range.Start + previewIndex),
                output.Slice(previewIndex, length),
                reference.Slice(previewIndex, length));
        }

        VisibleRows.ReplaceAll(rows);
    }

    internal void ShowRowsAtOffset(long offset, int requestedRowCount)
    {
        if (!IsAvailable)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedRowCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, TotalByteCount);

        int rowCount = Math.Min(requestedRowCount, MaximumVisibleRowCount);
        long firstOffset = offset / BytesPerRow * BytesPerRow;
        FirstVisibleOffset = firstOffset;
        int remainingRows = checked((int)Math.Min(
            rowCount,
            (TotalByteCount - firstOffset + BytesPerRow - 1) / BytesPerRow));
        var rows = new ReportHexDiffViewportRowViewModel[remainingRows];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = CreateRow(checked(firstOffset + (index * BytesPerRow)));
        }

        VisibleRows.ReplaceAll(rows);
    }

    private ReportHexDiffViewportRowViewModel CreateRow(long start)
    {
        ReadOnlySpan<byte> output = _snapshot!.OutputBytes.Span;
        ReadOnlySpan<byte> reference = _snapshot.ReferenceBytes.Span;
        int rowStart = checked((int)start);
        int length = Math.Min(BytesPerRow, output.Length - rowStart);
        return CreateRow(
            start,
            output.Slice(rowStart, length),
            reference.Slice(rowStart, length));
    }

    private ReportHexDiffViewportRowViewModel CreateRow(
        long start,
        ReadOnlySpan<byte> output,
        ReadOnlySpan<byte> reference)
    {
        ushort changedMask = 0;
        for (int index = 0; index < output.Length; index++)
        {
            if (output[index] != reference[index])
            {
                changedMask |= checked((ushort)(1 << index));
            }
        }

        int addressWidth = Math.Max(6, Math.Max(0, TotalByteCount - 1).ToString("X", CultureInfo.InvariantCulture).Length);
        return new ReportHexDiffViewportRowViewModel(
            start,
            $"{OutputSpaceId}:0x{start.ToString($"X{addressWidth}", CultureInfo.InvariantCulture)}",
            FormatHex(output),
            FormatAscii(output),
            FormatHex(reference),
            FormatAscii(reference),
            changedMask,
            ShowOriginalRows,
            _language);
    }

    private static string FormatHex(ReadOnlySpan<byte> bytes)
    {
        string compactHex = Convert.ToHexString(bytes);
        return compactHex.Length == 0
            ? string.Empty
            : string.Create(
                checked(compactHex.Length + (compactHex.Length / 2) - 1),
                compactHex,
                static (destination, source) =>
                {
                    int destinationIndex = 0;
                    for (int sourceIndex = 0; sourceIndex < source.Length; sourceIndex += 2)
                    {
                        if (destinationIndex > 0)
                        {
                            destination[destinationIndex++] = ' ';
                        }

                        destination[destinationIndex++] = source[sourceIndex];
                        destination[destinationIndex++] = source[sourceIndex + 1];
                    }
                });
    }

    private static string FormatAscii(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        foreach (byte value in bytes)
        {
            _ = builder.Append(value is >= 0x20 and <= 0x7e ? (char)value : '.');
        }

        return builder.ToString();
    }

    private static string FormatAddress(long offset)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{offset:X}");
    }

    private static bool TryParseAddress(string value, out long offset)
    {
        offset = 0;
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
    }

    private static string T(ShellLanguage language, string english, string traditionalChinese)
    {
        return language == ShellLanguage.ChineseTraditional ? traditionalChinese : english;
    }

    partial void OnSelectedRangeChanging(ReportHexDiffRangeViewModel? value)
    {
        if (SelectedRange is not null)
        {
            SelectedRange.IsSelected = false;
        }
    }

    partial void OnSelectedRangeChanged(ReportHexDiffRangeViewModel? value)
    {
        if (value is not null)
        {
            value.IsSelected = true;
        }

        UpdatePinnedSelectedRange();
    }

    private void NavigatorPage_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ReportWindowedListViewModel.VisibleCount), StringComparison.Ordinal))
        {
            UpdatePinnedSelectedRange();
            OnPropertyChanged(nameof(VisibleNavigatorRowCount));
        }
    }

    private void UpdatePinnedSelectedRange()
    {
        if (SelectedRange is null)
        {
            PinnedSelectedRange = null;
            return;
        }

        foreach (object item in NavigatorPage.Items)
        {
            if (item is not ReportHexDiffRangeViewModel visibleRange ||
                visibleRange.SourceIndex != SelectedRange.SourceIndex)
            {
                continue;
            }

            if (!ReferenceEquals(visibleRange, SelectedRange))
            {
                SelectedRange = visibleRange;
                return;
            }

            PinnedSelectedRange = null;
            return;
        }

        PinnedSelectedRange = SelectedRange;
    }

    partial void OnShowOriginalRowsChanged(bool value)
    {
        foreach (ReportHexDiffViewportRowViewModel row in VisibleRows)
        {
            row.IsOriginalVisible = value && row.HasChanges;
        }
    }
}

/// <summary>One immutable 16-byte output row plus optional verified reference row.</summary>
public sealed partial class ReportHexDiffViewportRowViewModel : ObservableObject
{
    internal ReportHexDiffViewportRowViewModel(
        long start,
        string address,
        string outputHex,
        string outputAscii,
        string originalHex,
        string originalAscii,
        ushort changedMask,
        bool isOriginalVisible,
        ShellLanguage language)
    {
        Start = start;
        Address = address;
        OutputHex = outputHex;
        OutputAscii = outputAscii;
        OriginalHex = originalHex;
        OriginalAscii = originalAscii;
        ChangedMask = changedMask;
        OutputAccessibleLabel = language == ShellLanguage.ChineseTraditional ? "輸出" : "output";
        OriginalAccessibleLabel = language == ShellLanguage.ChineseTraditional ? "原始" : "original";
        ChangeAccessibleLabel = language == ShellLanguage.ChineseTraditional
            ? HasChanges ? "已變更" : "未變更"
            : HasChanges ? "changed" : "unchanged";
        IsOriginalVisible = isOriginalVisible && HasChanges;
    }

    /// <inheritdoc/>
    public long Start { get; }

    /// <inheritdoc/>
    public string Address { get; }

    /// <inheritdoc/>
    public string OutputHex { get; }

    /// <inheritdoc/>
    public string OutputAscii { get; }

    /// <inheritdoc/>
    public string OriginalHex { get; }

    /// <inheritdoc/>
    public string OriginalAscii { get; }

    /// <inheritdoc/>
    public ushort ChangedMask { get; }

    /// <inheritdoc/>
    public bool HasChanges => ChangedMask != 0;

    private string OutputAccessibleLabel { get; }

    private string OriginalAccessibleLabel { get; }

    private string ChangeAccessibleLabel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleLabel))]
    public partial bool IsOriginalVisible { get; set; }

    /// <summary>Address-space-qualified row content for assistive review.</summary>
    public string AccessibleLabel => IsOriginalVisible
        ? $"{Address}, {ChangeAccessibleLabel}, {OutputAccessibleLabel} {OutputHex}, {OriginalAccessibleLabel} {OriginalHex}"
        : $"{Address}, {ChangeAccessibleLabel}, {OutputAccessibleLabel} {OutputHex}";
}

/// <summary>Bounded row window that publishes one collection reset per viewport change.</summary>
public sealed class ReportHexDiffViewportRowCollection : ObservableCollection<ReportHexDiffViewportRowViewModel>
{
    internal void ReplaceAll(IEnumerable<ReportHexDiffViewportRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        CheckReentrancy();
        Items.Clear();
        foreach (ReportHexDiffViewportRowViewModel row in rows)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
