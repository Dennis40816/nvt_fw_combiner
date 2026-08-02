using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Presentation state for the standalone raw-BIN Hex Editor utility. This workspace has no IC,
/// profile, flash-map, processor, or report behavior; it only projects one application-owned
/// in-memory binary editing session.
/// </summary>
public sealed partial class HexEditorWorkspaceViewModel : ObservableObject
{
    private const int BytesPerRow = 16;
    private static readonly int CurrentViewportRowCount = HexViewportCapabilityProfile.RawEditor.InitialRows;
    private readonly RawBinaryEditorSession _editor = new();
    private readonly WorkbenchRawBinaryEditorSession _files;
    private readonly Func<string, long, CancellationToken, Task<RawBinaryEditorSearchResult>> _findAsciiAsync;
    private RawBinaryEditorState _state = new(false, 0, 0, 0, 0, false);
    private long? _activeInlineEditAddress;
    private int _selectedColumnIndex = -1;
    private Dictionary<long, HexViewportSnapshot>? _selectionSnapshots;
    private Dictionary<long, string>? _selectionAddressLabels;
    private HexViewportSnapshot? _unselectedSnapshot;
    private HexViewportSnapshot CurrentViewportSnapshot { get; set; } = HexViewportSnapshot.Empty(
        HexViewportCapabilityProfile.RawEditor,
        "raw-binary-work-buffer");

    /// <summary>Creates the standalone raw-BIN workspace with its initial localized text bundle.</summary>
    public HexEditorWorkspaceViewModel(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _files = new WorkbenchRawBinaryEditorSession(_editor);
        _findAsciiAsync = _files.FindAsciiAsync;
        Text = text;
        ChangedBlockPage = CreateChangedBlockPage([]);
        ColumnHeaders = [.. Enumerable.Range(0, 16).Select(index => new HexEditorColumnHeaderViewModel(index))];
        GoToCommand = new RelayCommand(GoToViewport);
        FindAsciiCommand = new AsyncRelayCommand(FindAsciiAsync, CanFindAscii);
        SetViewportStartRowCommand = new RelayCommand<int>(SetViewportStartRow);
        BeginByteEditCommand = new RelayCommand<long>(BeginByteEdit);
        CommitByteEditCommand = new RelayCommand<HexEditorByteEditRequest>(CommitByteEdit);
        CancelByteEditCommand = new RelayCommand<long>(CancelByteEdit);
        InsertZeroBeforeCommand = new RelayCommand<long>(InsertZeroBefore);
        InsertZeroAfterCommand = new RelayCommand<long>(InsertZeroAfter);
        RequestInsertBytesBeforeCommand = new RelayCommand<long>(RequestInsertBytesBefore);
        RequestInsertBytesAfterCommand = new RelayCommand<long>(RequestInsertBytesAfter);
        ConfirmInsertBytesCommand = new RelayCommand(ConfirmInsertBytes, CanConfirmInsertBytes);
        CancelInsertBytesCommand = new RelayCommand(CancelInsertBytes);
        DeleteByteCommand = new RelayCommand<long>(DeleteByte);
        SetByteToZeroCommand = new RelayCommand<long>(SetByteToZero);
        SetByteToFfCommand = new RelayCommand<long>(SetByteToFf);
        SelectNextChangedBlockCommand = new RelayCommand(SelectNextChangedBlock, () => HasChangedBlocks);
        SelectChangedBlockCommand = new RelayCommand<HexEditorChangedBlockViewModel>(SelectChangedBlock);
        GoToChangedBlockStartCommand = new RelayCommand<HexEditorChangedBlockViewModel>(GoToChangedBlockStart);
        GoToChangedBlockEndCommand = new RelayCommand<HexEditorChangedBlockViewModel>(GoToChangedBlockEnd);
        ApplyRangeEditCommand = new RelayCommand(ApplyRangeEdit, CanApplyRangeEdit);
        ApplyOverwriteRangeCommand = new RelayCommand(ApplyOverwriteRange, CanApplyRangeEdit);
        ApplyFillRangeCommand = new RelayCommand(ApplyFillRange, CanApplyRangeEdit);
        UndoCommand = new RelayCommand(Undo, () => _state.UndoCount > 0);
        RedoCommand = new RelayCommand(Redo, () => _state.RedoCount > 0);
        RequestSaveCommand = new RelayCommand(RequestSave, () => CanSave);
        CancelSaveCommand = new RelayCommand(CancelSave);
        EditorStatus = text.HexEditorSourceEmptyDetail;
    }

    internal HexEditorWorkspaceViewModel(
        ShellTextResources text,
        Func<string, long, CancellationToken, Task<RawBinaryEditorSearchResult>> findAsciiAsync)
        : this(text)
    {
        _findAsciiAsync = findAsciiAsync ?? throw new ArgumentNullException(nameof(findAsciiAsync));
    }

    /// <summary>Gets the active localized text bundle.</summary>
    public ShellTextResources Text { get; private set; }

    /// <summary>Gets fixed byte-offset headers for the 16-column grid.</summary>
    public IReadOnlyList<HexEditorColumnHeaderViewModel> ColumnHeaders { get; }

    /// <summary>Gets the bounded immutable window consumed by the source-neutral renderer.</summary>
    internal HexViewportSnapshot ViewportSnapshot => CurrentViewportSnapshot;

    /// <summary>Gets or sets the address used by the explicit Go to command.</summary>
    [ObservableProperty]
    public partial string ViewportAddress { get; set; } = "0x000000";

    /// <summary>Printable ASCII text to find in the in-memory work buffer.</summary>
    [ObservableProperty]
    public partial string AsciiSearchText { get; set; } = string.Empty;

    /// <summary>Gets the first logical raw-BIN row currently projected into the bounded viewport.</summary>
    [ObservableProperty]
    public partial int ViewportStartRow { get; private set; }

    /// <summary>Gets or sets the selected inclusive range start for explicit range operations.</summary>
    [ObservableProperty]
    public partial string RangeStartAddress { get; set; } = "0x000000";

    /// <summary>Gets or sets the selected inclusive range end for explicit range operations.</summary>
    [ObservableProperty]
    public partial string RangeEndAddress { get; set; } = "0x000000";

    /// <summary>Gets or sets hexadecimal bytes used by overwrite or the one-byte fill value.</summary>
    [ObservableProperty]
    public partial string RangeValue { get; set; } = string.Empty;

    /// <summary>Controls the optional original-source row below changed current-data rows.</summary>
    [ObservableProperty]
    public partial bool IsOriginalRowsVisible { get; set; }

    /// <summary>True while Save requests a user confirmation before opening the Save As dialog.</summary>
    [ObservableProperty]
    public partial bool IsSaveConfirmationOpen { get; set; }

    /// <summary>Normalized source path for the in-memory document, never used as an output target.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceName))]
    [NotifyPropertyChangedFor(nameof(SourcePathDisplay))]
    public partial string? SourcePath { get; set; }

    /// <summary>Current local status for the raw-BIN workspace.</summary>
    [ObservableProperty]
    public partial string EditorStatus { get; set; } = string.Empty;

    /// <summary>Display name of the loaded source document.</summary>
    public string SourceName => string.IsNullOrWhiteSpace(SourcePath)
        ? Text.NoBinSelectedLabel
        : Path.GetFileName(SourcePath);

    /// <summary>Normalized source path used only for human display.</summary>
    public string SourcePathDisplay => string.IsNullOrWhiteSpace(SourcePath)
        ? string.Empty
        : FirmwarePathDisplay.Normalize(SourcePath);

    /// <summary>True when the application-owned work buffer has a loaded document.</summary>
    public bool HasDocument => _state.HasDocument;

    /// <summary>True when one or more retained edits differ from the loaded source document.</summary>
    public bool HasUnsavedChanges => _state.HasUnsavedChanges;

    /// <summary>True when Save As can export a new BIN without modifying the source file.</summary>
    public bool CanSave => HasDocument && HasUnsavedChanges;

    /// <summary>True while one byte is receiving direct inline text input.</summary>
    public bool IsInlineEditActive => _activeInlineEditAddress.HasValue;

    /// <summary>True while an editor text box owns the keyboard, preserving its native editing shortcuts.</summary>
    [ObservableProperty]
    public partial bool IsTextEntryFocused { get; set; }

    /// <summary>Currently selected work-buffer address, retained while it is outside the visible row window.</summary>
    public string? SelectedByteAddress { get; private set; }

    /// <summary>Accessible value and change context for the currently selected byte.</summary>
    public string SelectedByteAccessibleLabel => TryGetViewportCell(SelectedByteAddress, out HexViewportCell cell)
        ? CreateAccessibleLabel(cell)
        : SelectedByteAddress ?? string.Empty;

    /// <summary>Compact hexadecimal length of the in-memory work buffer.</summary>
    public string WorkingLengthLabel => FormattableString.Invariant($"0x{_state.WorkingLength:X} bytes");

    /// <summary>Gets the complete logical 16-byte-row count used to size the document scrollbar immediately.</summary>
    public int TotalRowCount => GetRowCount();

    /// <summary>Gets the greatest valid first-row position for the bounded document viewport.</summary>
    public int DocumentScrollMaximum => CalculateDocumentScrollMaximum();

    /// <summary>Gets the fixed number of logical rows displayed in one document viewport.</summary>
    public int VisibleRowCount => Math.Min(ViewportRowCount, TotalRowCount);

    /// <summary>Number of retained in-memory edit operations.</summary>
    public int ChangeCount => _state.UndoCount;

    /// <summary>Suggested non-destructive output file name.</summary>
    public string SuggestedOutputFileName => _files.SuggestedOutputFileName;

    /// <summary>Explicit navigation command for a requested address.</summary>
    public IRelayCommand GoToCommand { get; }

    /// <summary>Finds the next printable ASCII occurrence in the in-memory work buffer.</summary>
    public IAsyncRelayCommand FindAsciiCommand { get; }

    /// <summary>Moves the bounded viewport to a coalesced document-scrollbar row position.</summary>
    public IRelayCommand<int> SetViewportStartRowCommand { get; }

    /// <summary>Starts direct two-character editing for one current-data byte.</summary>
    public IRelayCommand<long> BeginByteEditCommand { get; }

    /// <summary>Commits the current inline byte value through the raw-BIN application session.</summary>
    internal IRelayCommand<HexEditorByteEditRequest> CommitByteEditCommand { get; }

    /// <summary>Cancels one inline edit without changing the in-memory work buffer.</summary>
    public IRelayCommand<long> CancelByteEditCommand { get; }

    /// <summary>Inserts one 00 byte before the selected current-data byte.</summary>
    public IRelayCommand<long> InsertZeroBeforeCommand { get; }

    /// <summary>Inserts one 00 byte after the selected current-data byte.</summary>
    public IRelayCommand<long> InsertZeroAfterCommand { get; }

    /// <summary>Deletes the selected current-data byte.</summary>
    public IRelayCommand<long> DeleteByteCommand { get; }

    /// <summary>Sets the selected current-data byte to 00.</summary>
    public IRelayCommand<long> SetByteToZeroCommand { get; }

    /// <summary>Sets the selected current-data byte to FF.</summary>
    public IRelayCommand<long> SetByteToFfCommand { get; }

    /// <summary>Applies an exact byte sequence to the chosen inclusive range.</summary>
    public IRelayCommand ApplyOverwriteRangeCommand { get; }

    /// <summary>Fills the chosen inclusive range with one byte.</summary>
    public IRelayCommand ApplyFillRangeCommand { get; }

    /// <summary>Reverts the most recent in-memory edit.</summary>
    public IRelayCommand UndoCommand { get; }

    /// <summary>Reapplies the most recently reverted in-memory edit.</summary>
    public IRelayCommand RedoCommand { get; }

    /// <summary>Opens the non-destructive Save As confirmation.</summary>
    public IRelayCommand RequestSaveCommand { get; }

    /// <summary>Closes the non-destructive Save As confirmation.</summary>
    public IRelayCommand CancelSaveCommand { get; }

    /// <summary>Records whether a text entry control currently owns the keyboard focus.</summary>
    public void SetTextEntryFocused(bool isFocused)
    {
        IsTextEntryFocused = isFocused;
    }

    /// <summary>Updates user-facing labels after a shell language change.</summary>
    public void ApplyTextResources(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(WorkingLengthLabel));
        OnPropertyChanged(nameof(CurrentWriteModeLabel));
        OnPropertyChanged(nameof(CurrentWriteModeTooltip));
        OnPropertyChanged(nameof(EditGuidance));
        OnPropertyChanged(nameof(EditNotice));
        OnPropertyChanged(nameof(InsertBytesPromptTitle));
        OnPropertyChanged(nameof(InsertBytesMaximumLabel));
        ClearEditFeedback();
        if (HasDocument)
        {
            RefreshChangeTracking();
        }

        EditorStatus = HasDocument ? CreateReadyStatus() : Text.HexEditorSourceEmptyDetail;
    }

    private bool CanApplyRangeEdit()
    {
        return HasDocument &&
               !string.IsNullOrWhiteSpace(RangeStartAddress) &&
               !string.IsNullOrWhiteSpace(RangeEndAddress) &&
               !string.IsNullOrWhiteSpace(RangeValue);
    }

    private void GoToViewport()
    {
        if (!TryParseAddressLabel(ViewportAddress, out long targetAddress) ||
            !TryGetRowIndex(targetAddress, out int targetRowIndex))
        {
            EditorStatus = Text.HexEditorInvalidAddressDetail;
            return;
        }

        SetViewportStartRow(Math.Max(0, targetRowIndex - 4));
        UpdateSelection(targetAddress);
        EditorStatus = CreateReadyStatus();
    }

    internal void SelectByte(long address)
    {
        if (!TryGetRowIndex(address, out _))
        {
            return;
        }

        string label = GetSelectionAddressLabel(address);
        RangeStartAddress = label;
        RangeEndAddress = label;
        UpdateSelection(address, label);
    }

    internal void MoveSelection(int offsetDelta)
    {
        if (offsetDelta == 0 ||
            !TryParseAddressLabel(SelectedByteAddress ?? string.Empty, out long selectedAddress) ||
            _state.WorkingLength == 0)
        {
            return;
        }

        long nextAddress = Math.Clamp(selectedAddress + offsetDelta, 0, _state.WorkingLength - 1);
        if (TryGetRowIndex(nextAddress, out int rowIndex) &&
            (rowIndex < ViewportStartRow || rowIndex >= ViewportStartRow + VisibleRowCount))
        {
            SetViewportStartRow(Math.Max(0, rowIndex - 4));
        }

        UpdateSelection(nextAddress);
    }

    private void BeginByteEdit(long address)
    {
        if (!TryGetRowIndex(address, out _))
        {
            return;
        }

        SelectByte(address);
        _activeInlineEditAddress = address;
        OnPropertyChanged(nameof(IsInlineEditActive));
    }

    private void CommitByteEdit(HexEditorByteEditRequest? request)
    {
        if (request is null || _activeInlineEditAddress != request.Address)
        {
            return;
        }

        string address = FormatAddress(request.Address);
        RawBinaryEditorOperationResult result = _editor.OverwriteByte(address, request.Value);
        if (!result.Succeeded)
        {
            EditorStatus = DescribeIssue(result.Issue!);
            return;
        }

        _activeInlineEditAddress = null;
        OnPropertyChanged(nameof(IsInlineEditActive));
        ApplySuccessfulOperation(result, address);
    }

    private void CancelByteEdit(long address)
    {
        if (_activeInlineEditAddress == address)
        {
            _activeInlineEditAddress = null;
            OnPropertyChanged(nameof(IsInlineEditActive));
        }
    }

    private void InsertZeroBefore(long address)
    {
        if (TryGetRowIndex(address, out _))
        {
            string label = FormatAddress(address);
            ApplyOperation(_editor.InsertZeroBefore(label), label);
        }
    }

    private void InsertZeroAfter(long address)
    {
        if (TryGetRowIndex(address, out _))
        {
            string label = FormatAddress(address);
            ApplyOperation(_editor.InsertZeroAfter(label), FormatAddress(checked(address + 1)));
        }
    }

    private void DeleteByte(long address)
    {
        if (TryGetRowIndex(address, out _))
        {
            string label = FormatAddress(address);
            ApplyOperation(_editor.DeleteByte(label), label);
        }
    }

    private void SetByteToZero(long address)
    {
        if (TryGetRowIndex(address, out _))
        {
            string label = FormatAddress(address);
            ApplyOperation(_editor.OverwriteByte(label, "00"), label);
        }
    }

    private void SetByteToFf(long address)
    {
        if (TryGetRowIndex(address, out _))
        {
            string label = FormatAddress(address);
            ApplyOperation(_editor.OverwriteByte(label, "FF"), label);
        }
    }

    private void ApplyOverwriteRange()
    {
        ApplyRangeOperation(_editor.OverwriteRange(RangeStartAddress, RangeEndAddress, RangeValue), RangeStartAddress);
    }

    private void ApplyFillRange()
    {
        ApplyRangeOperation(_editor.FillRange(RangeStartAddress, RangeEndAddress, RangeValue), RangeStartAddress);
    }

    private void Undo()
    {
        IReadOnlyDictionary<long, VisibleByteFingerprint> before = CaptureVisibleByteFingerprints();
        RawBinaryEditorOperationResult result = _editor.Undo();
        ApplyOperation(result, SelectedByteAddress);
        if (result.Succeeded)
        {
            PublishHistoryFeedback(before);
        }
    }

    private void Redo()
    {
        IReadOnlyDictionary<long, VisibleByteFingerprint> before = CaptureVisibleByteFingerprints();
        RawBinaryEditorOperationResult result = _editor.Redo();
        ApplyOperation(result, SelectedByteAddress);
        if (result.Succeeded)
        {
            PublishHistoryFeedback(before);
        }
    }

    private void RequestSave()
    {
        if (CanSave)
        {
            IsSaveConfirmationOpen = true;
        }
    }

    private void CancelSave()
    {
        IsSaveConfirmationOpen = false;
    }

    private void ApplyOperation(RawBinaryEditorOperationResult result, string? selectedAddress)
    {
        if (!result.Succeeded)
        {
            EditorStatus = DescribeIssue(result.Issue!);
            return;
        }

        ApplySuccessfulOperation(result, selectedAddress);
    }

    private void ApplySuccessfulOperation(RawBinaryEditorOperationResult result, string? selectedAddress)
    {
        FindAsciiCommand.Cancel();
        UpdateState(result.State);
        ClearAsciiSearchResults(refreshViewport: false);
        RefreshChangeTracking();
        ViewportStartRow = Math.Min(ViewportStartRow, DocumentScrollMaximum);
        RefreshViewportSnapshot();

        if (!string.IsNullOrWhiteSpace(selectedAddress))
        {
            UpdateSelection(selectedAddress);
        }

        EditorStatus = CreateReadyStatus();
    }

    private void SetViewportStartRow(int requestedRow)
    {
        int nextRow = Math.Clamp(requestedRow, 0, DocumentScrollMaximum);
        if (ViewportStartRow == nextRow)
        {
            return;
        }

        ViewportStartRow = nextRow;
        RefreshViewportSnapshot();
    }

    private void RefreshViewportSnapshot()
    {
        if (!HasDocument || TotalRowCount == 0)
        {
            PublishViewportSnapshot(HexViewportSnapshot.Empty(
                HexViewportCapabilityProfile.RawEditor,
                "raw-binary-work-buffer"));
            return;
        }

        long address = checked((long)ViewportStartRow * BytesPerRow);
        RawBinaryEditorViewport viewport = _editor.CreatePage(address, ViewportRowCount);
        if (!viewport.Succeeded)
        {
            PublishViewportSnapshot(HexViewportSnapshot.Empty(
                HexViewportCapabilityProfile.RawEditor,
                "raw-binary-work-buffer"));
            ClearSelection();
            EditorStatus = DescribeIssue(viewport.Issue!);
            return;
        }

        long? selectedAddress = TryParseAddressLabel(SelectedByteAddress ?? string.Empty, out long selected) &&
                                selected >= 0 &&
                                selected < _state.WorkingLength
            ? selected
            : null;
        var rows = new HexViewportRow[viewport.Rows.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = CreateViewportRow(viewport.Rows[index]);
        }

        PublishViewportSnapshot(HexViewportSnapshot.CreateOwned(
            HexViewportCapabilityProfile.RawEditor,
            "raw-binary-work-buffer",
            _state.WorkingLength,
            address,
            rows,
            selectedAddress,
            IsOriginalRowsVisible,
            HistoryFeedbackVersion));
    }

    private int GetRowCount()
    {
        return !_state.HasDocument || _state.WorkingLength == 0
            ? 0
            : checked((int)((_state.WorkingLength + BytesPerRow - 1) / BytesPerRow));
    }

    private bool TryGetRowIndex(long address, out int rowIndex)
    {
        rowIndex = 0;
        if (!_state.HasDocument || address < 0 || address >= _state.WorkingLength)
        {
            return false;
        }

        rowIndex = checked((int)(address / BytesPerRow));
        return true;
    }

    private void UpdateSelection(string address)
    {
        UpdateSelection(TryParseAddressLabel(address, out long selectedAddress) ? selectedAddress : -1);
    }

    private void UpdateSelection(long address)
    {
        UpdateSelection(address, TryGetRowIndex(address, out _) ? GetSelectionAddressLabel(address) : null);
    }

    private void UpdateSelection(long address, string? addressLabel)
    {
        long? selectedAddress = TryGetRowIndex(address, out _) ? address : null;
        SelectedByteAddress = selectedAddress.HasValue ? addressLabel : null;
        OnPropertyChanged(nameof(SelectedByteAddress));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
        PublishViewportSnapshot(GetSelectionSnapshot(selectedAddress));

        int selectedColumn = selectedAddress is long selectedOffset
            ? (int)(selectedOffset & 0xF)
            : -1;
        SetSelectedColumn(selectedColumn);
    }

    private void ClearSelection()
    {
        SelectedByteAddress = null;
        OnPropertyChanged(nameof(SelectedByteAddress));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
        PublishViewportSnapshot(GetSelectionSnapshot(null));
        SetSelectedColumn(-1);
    }

    private void SetSelectedColumn(int selectedColumn)
    {
        if (_selectedColumnIndex == selectedColumn)
        {
            return;
        }

        if (_selectedColumnIndex >= 0)
        {
            ColumnHeaders[_selectedColumnIndex].IsSelected = false;
        }

        _selectedColumnIndex = selectedColumn;
        if (_selectedColumnIndex >= 0)
        {
            ColumnHeaders[_selectedColumnIndex].IsSelected = true;
        }
    }

    private void UpdateState(RawBinaryEditorState state)
    {
        _state = state;
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(WorkingLengthLabel));
        OnPropertyChanged(nameof(TotalRowCount));
        OnPropertyChanged(nameof(VisibleRowCount));
        OnPropertyChanged(nameof(DocumentScrollMaximum));
        OnPropertyChanged(nameof(ChangeCount));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
        ApplyRangeEditCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        RequestSaveCommand.NotifyCanExecuteChanged();
        FindAsciiCommand.NotifyCanExecuteChanged();
    }

}

internal sealed record HexEditorByteEditRequest(long Address, string Value);
