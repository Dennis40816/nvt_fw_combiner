using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Range-bound, read-only replay of Report before/output bytes.</summary>
public sealed partial class ReportHexDiffViewModel : ObservableObject
{
    private readonly CompositionRunInspectionSnapshot? _verifiedSnapshot;
    private readonly ReportHexDiffSource _source;
    private readonly ShellLanguage _language;
    private readonly bool _reportBoundsValid;
    private readonly bool _snapshotWasRejected;
    private readonly RelayCommand<ReportHexDiffRangeViewModel> _selectRangeCommand;
    private readonly RelayCommand<HexViewportInteractionIntent> _viewportInteractionCommand;
    private OutputDifferenceReplaySegment? _activeReplay;
    private int _rangeScrollRow;
    private long? _selectedByteAddress;

    private ReportHexDiffViewModel(
        CompositionRunInspectionSnapshot? verifiedSnapshot,
        ReportHexDiffSource source,
        ShellLanguage language,
        long reportOutputSize,
        bool reportBoundsValid,
        bool snapshotWasRejected)
    {
        _verifiedSnapshot = verifiedSnapshot;
        _source = source;
        _language = language;
        _reportBoundsValid = reportBoundsValid;
        _snapshotWasRejected = snapshotWasRejected;
        OutputSpaceId = verifiedSnapshot?.OutputSpaceId ?? source.OutputSpaceId;
        ReferenceSpaceId = verifiedSnapshot?.ReferenceSpaceId ?? "reported-reference";
        TotalByteCount = reportBoundsValid ? checked((int)reportOutputSize) : 0;
        TotalRowCount = reportBoundsValid
            ? checked((TotalByteCount + HexViewportSnapshot.BytesPerRow - 1) / HexViewportSnapshot.BytesPerRow)
            : 0;
        HasDifferenceWorkspace = source.Count > 0;
        ViewportSnapshot = HexViewportSnapshot.Empty(
            HexViewportCapabilityProfile.ReportDiff,
            OutputSpaceId);
        Ranges = source.NavigatorRows;
        _selectRangeCommand = new RelayCommand<ReportHexDiffRangeViewModel>(SelectRange, CanSelectRange);
        _viewportInteractionCommand = new RelayCommand<HexViewportInteractionIntent>(HandleViewportIntent);

        ReportHexDiffRangeViewModel? initialRange = reportBoundsValid && Ranges.Count > 0
            ? Ranges[0]
            : null;
        SelectedRange = initialRange;
        if (initialRange is null)
        {
            RefreshSelectedReplay();
        }
    }

    /// <summary>True when the current report selection has complete, trusted replay bytes.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Short complete/replay/unavailable state.</summary>
    public string AvailabilityTitle { get; private set; } = string.Empty;

    /// <summary>Why the currently selected range can or cannot be replayed.</summary>
    public string AvailabilityDetail { get; private set; } = string.Empty;

    /// <summary>True when the selected viewport uses exact bytes persisted in the report.</summary>
    public bool IsReportedRangeMode { get; private set; }

    /// <summary>True when the selected viewport contains at least one byte row.</summary>
    public bool HasViewportBytes => ViewportSnapshot.Rows.Count > 0;

    /// <summary>True when the selected report range cannot expose replay bytes.</summary>
    public bool HasNoViewportBytes => !HasViewportBytes;

    /// <summary>True when at least one reported difference belongs in the sole Hex Diff workspace.</summary>
    public bool HasDifferenceWorkspace { get; }

    /// <summary>Application-owned compiled output address space.</summary>
    public string OutputSpaceId { get; }

    /// <summary>Application-owned canonical reference address space.</summary>
    public string ReferenceSpaceId { get; }

    /// <summary>Declared output byte count used only for checked replay bounds.</summary>
    public int TotalByteCount { get; }

    /// <summary>Total logical output row count; whole-document navigation is not exposed.</summary>
    public int TotalRowCount { get; }

    /// <summary>Immutable input consumed by the shared #191 renderer.</summary>
    internal HexViewportSnapshot ViewportSnapshot { get; private set; }

    /// <summary>Review-first semantic ranges, lazily materialized by the virtualized list.</summary>
    public IReadOnlyList<ReportHexDiffRangeViewModel> Ranges { get; }

    /// <summary>Number of range-detail models materialized by the virtualized navigator.</summary>
    internal int MaterializedRangeCount => _source.MaterializedCount;

    /// <summary>Selected report-owned semantic range.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRange))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedRange))]
    public partial ReportHexDiffRangeViewModel? SelectedRange { get; set; }

    /// <summary>True when the accordion has one selected range.</summary>
    public bool HasSelectedRange => SelectedRange is not null;

    /// <summary>True when no report range is selected.</summary>
    public bool HasNoSelectedRange => SelectedRange is null;

    /// <summary>Localized screen-reader projection for the selected custom-drawn byte.</summary>
    public string SelectedByteAccessibleLabel
    {
        get
        {
            if (_selectedByteAddress is not long selected || !TryGetVisibleCell(selected, out HexViewportCell cell))
            {
                return T(_language, "No Hex Diff byte selected.", "尚未選取 Hex Diff 位元組。");
            }

            string address = FormattableString.Invariant($"0x{selected:X6}");
            string output = FormattableString.Invariant($"0x{cell.PrimaryValue:X2}");
            string state = cell.IsDataChanged
                ? T(_language, "changed", "已變更")
                : T(_language, "unchanged context", "未變更的 context");
            return ShowOriginalRows && cell.ComparisonValue is byte original
                ? T(
                    _language,
                    $"Address {address}, output {output}, original 0x{original:X2}, {state}.",
                    $"位址 {address}，輸出 {output}，原始值 0x{original:X2}，{state}。")
                : T(
                    _language,
                    $"Address {address}, output {output}, {state}.",
                    $"位址 {address}，輸出 {output}，{state}。");
        }
    }

    /// <summary>First range-local row currently materialized.</summary>
    public int RangeScrollRow
    {
        get => _rangeScrollRow;
        set
        {
            int next = Math.Clamp(value, 0, RangeScrollMaximum);
            if (_rangeScrollRow == next)
            {
                return;
            }

            _rangeScrollRow = next;
            OnPropertyChanged();
            PublishViewport();
        }
    }

    /// <summary>Last admitted start row inside the selected replay segment.</summary>
    public int RangeScrollMaximum { get; private set; }

    /// <summary>Controls the optional immutable comparison plane; defaults off.</summary>
    [ObservableProperty]
    public partial bool ShowOriginalRows { get; set; }

    /// <summary>First output-space address currently materialized.</summary>
    public long FirstVisibleOffset => ViewportSnapshot.StartAddress;

    /// <summary>Selects one report-owned range and replays only its persisted bytes.</summary>
    public IRelayCommand<ReportHexDiffRangeViewModel> SelectRangeCommand => _selectRangeCommand;

    /// <summary>Receives source-neutral selection and range-scroll intents from the shared renderer.</summary>
    internal IRelayCommand<HexViewportInteractionIntent> ViewportInteractionCommand => _viewportInteractionCommand;

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
        return new ReportHexDiffViewModel(
            identityMatches ? snapshot : null,
            source,
            language,
            reportOutputSize,
            reportBoundsValid,
            snapshot is not null && !identityMatches);
    }

    /// <summary>Consumes source-neutral intents emitted by the shared renderer.</summary>
    internal void HandleViewportIntent(HexViewportInteractionIntent intent)
    {
        if (_activeReplay is null)
        {
            return;
        }

        switch (intent.Trigger)
        {
            case HexViewportInteractionTrigger.Scroll:
                RangeScrollRow = checked(RangeScrollRow + intent.Delta);
                break;
            case HexViewportInteractionTrigger.Select when intent.Address is long selected:
                SelectByte(selected, ensureVisible: false);
                break;
            case HexViewportInteractionTrigger.MoveSelection:
                long current = _selectedByteAddress ?? SelectedRange?.Start ?? _activeReplay.Range.Start;
                SelectByte(checked(current + intent.Delta), ensureVisible: true);
                break;
            case HexViewportInteractionTrigger.Select:
            case HexViewportInteractionTrigger.Activate:
            case HexViewportInteractionTrigger.Context:
            case HexViewportInteractionTrigger.StructuralContext:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent.Trigger, null);
        }
    }

    private bool CanSelectRange(ReportHexDiffRangeViewModel? range)
    {
        return _reportBoundsValid && range is not null && _source.Contains(range) &&
            string.Equals(range.OutputSpaceId, OutputSpaceId, StringComparison.Ordinal) &&
            range.Start >= 0 && range.Length > 0 && range.Start <= TotalByteCount - range.Length;
    }

    private void SelectRange(ReportHexDiffRangeViewModel? range)
    {
        if (!CanSelectRange(range))
        {
            return;
        }

        SelectedRange = range;
    }

    private void RefreshSelectedReplay()
    {
        _activeReplay = CreateReplay(SelectedRange);
        _rangeScrollRow = 0;
        _selectedByteAddress = SelectedRange?.Start;
        RangeScrollMaximum = CalculateRangeScrollMaximum(_activeReplay, ShowOriginalRows);
        RefreshAvailability();
        PublishViewport();
        OnPropertyChanged(nameof(RangeScrollRow));
        OnPropertyChanged(nameof(RangeScrollMaximum));
    }

    private OutputDifferenceReplaySegment? CreateReplay(ReportHexDiffRangeViewModel? range)
    {
        return range is null || !CanSelectRange(range)
            ? null
            : _verifiedSnapshot is not null
            ? OutputDifferenceReplaySegment.CreateWithAlignedContext(
                _verifiedSnapshot.ReferenceBytes,
                _verifiedSnapshot.OutputBytes,
                range.Start,
                range.Length)
            : range.Replay is { } replay &&
                replay.Range.EndExclusive <= TotalByteCount &&
                range.Start >= replay.Range.Start &&
                range.Length <= replay.Range.Length &&
                range.Start <= replay.Range.EndExclusive - range.Length
                    ? replay
                    : null;
    }

    private static int CalculateRangeScrollMaximum(
        OutputDifferenceReplaySegment? replay,
        bool showOriginalRows)
    {
        if (replay is null)
        {
            return 0;
        }

        int totalRows = checked((int)(
            (replay.Range.Length + HexViewportSnapshot.BytesPerRow - 1) /
            HexViewportSnapshot.BytesPerRow));
        return Math.Max(
            0,
            totalRows - ReportHexDiffViewportAdapter.GetLogicalRowBudget(showOriginalRows));
    }

    private void RefreshAvailability()
    {
        IsAvailable = _verifiedSnapshot is not null || _activeReplay is not null;
        IsReportedRangeMode = _verifiedSnapshot is null && _activeReplay is not null;
        if (_verifiedSnapshot is not null)
        {
            AvailabilityTitle = T(_language, "Complete Hex Diff", "完整 Hex Diff");
            AvailabilityDetail = T(
                _language,
                $"Verified {OutputSpaceId} output against {ReferenceSpaceId} from the same run.",
                $"已用同一次執行的 {ReferenceSpaceId} 驗證 {OutputSpaceId} output。");
        }
        else if (_activeReplay is not null)
        {
            AvailabilityTitle = T(_language, "Replayable Report Hex Diff", "可重播的 Report Hex Diff");
            AvailabilityDetail = _snapshotWasRejected
                ? T(
                    _language,
                    "The in-session bytes do not match this report; the exact persisted range replay is shown.",
                    "目前 session bytes 與此 Report 不相符；顯示 Report 保留的完整 range replay。")
                : T(
                    _language,
                    "The persisted range contains complete before/output bytes and aligned context.",
                    "Report range 已保留完整變更前／輸出 bytes 與對齊 context。");
        }
        else
        {
            AvailabilityTitle = T(_language, "Hex Diff unavailable", "無法使用 Hex Diff");
            AvailabilityDetail = !_reportBoundsValid
                ? T(
                    _language,
                    "The reported ranges do not fit the declared output bounds.",
                    "已報告區段不符合宣告的 output 範圍。")
                : SelectedRange?.ReplayCoverage ?? T(
                    _language,
                    "Select a reported range to inspect its bytes.",
                    "選擇一個 Report range 以檢視 bytes。");
        }

        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsReportedRangeMode));
        OnPropertyChanged(nameof(AvailabilityTitle));
        OnPropertyChanged(nameof(AvailabilityDetail));
    }

    private void PublishViewport()
    {
        ViewportSnapshot = _activeReplay is null || SelectedRange is null
            ? HexViewportSnapshot.Empty(
                HexViewportCapabilityProfile.ReportDiff,
                OutputSpaceId)
            : ReportHexDiffViewportAdapter.Create(
                OutputSpaceId,
                TotalByteCount,
                SelectedRange.Start,
                SelectedRange.Length,
                _activeReplay,
                RangeScrollRow,
                _selectedByteAddress,
                ShowOriginalRows);

        OnPropertyChanged(nameof(ViewportSnapshot));
        OnPropertyChanged(nameof(HasViewportBytes));
        OnPropertyChanged(nameof(HasNoViewportBytes));
        OnPropertyChanged(nameof(FirstVisibleOffset));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
    }

    private void SelectByte(long address, bool ensureVisible)
    {
        if (_activeReplay is null)
        {
            return;
        }

        long selected = Math.Clamp(
            address,
            _activeReplay.Range.Start,
            _activeReplay.Range.EndExclusive - 1);
        _selectedByteAddress = selected;
        if (ensureVisible)
        {
            int targetRow = checked((int)(
                (selected - _activeReplay.Range.Start) / HexViewportSnapshot.BytesPerRow));
            if (targetRow < RangeScrollRow)
            {
                RangeScrollRow = targetRow;
                return;
            }

            int lastVisibleRow = RangeScrollRow + HexViewportCapabilityProfile.ReportDiff.InitialRows - 1;
            if (targetRow > lastVisibleRow)
            {
                RangeScrollRow = targetRow - HexViewportCapabilityProfile.ReportDiff.InitialRows + 1;
                return;
            }
        }

        ViewportSnapshot = ViewportSnapshot.WithSelectedAddress(selected);
        OnPropertyChanged(nameof(ViewportSnapshot));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
    }

    private bool TryGetVisibleCell(long address, out HexViewportCell cell)
    {
        foreach (HexViewportRow row in ViewportSnapshot.Rows)
        {
            long index = address - row.Address;
            if ((ulong)index < (ulong)row.Cells.Count)
            {
                cell = row.Cells[(int)index];
                return true;
            }
        }

        cell = default;
        return false;
    }

    private static string T(ShellLanguage language, string english, string traditionalChinese)
    {
        return language == ShellLanguage.ChineseTraditional ? traditionalChinese : english;
    }

    partial void OnShowOriginalRowsChanged(bool value)
    {
        RangeScrollMaximum = CalculateRangeScrollMaximum(_activeReplay, value);
        int nextRow = Math.Min(_rangeScrollRow, RangeScrollMaximum);
        if (nextRow != _rangeScrollRow)
        {
            _rangeScrollRow = nextRow;
            OnPropertyChanged(nameof(RangeScrollRow));
        }

        OnPropertyChanged(nameof(RangeScrollMaximum));
        PublishViewport();
    }

    partial void OnSelectedRangeChanging(ReportHexDiffRangeViewModel? value)
    {
        if (value is not null && !CanSelectRange(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The selected Report Diff range is not owned by this report.");
        }

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

        RefreshSelectedReplay();
    }
}
