using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Presentation state for the standalone raw-BIN Hex Editor utility. This workspace has no IC,
/// profile, flash-map, processor, or report behavior; it only projects one application-owned
/// in-memory binary editing session.
/// </summary>
public sealed partial class HexEditorWorkspaceViewModel : ObservableObject
{
    private const int BytesPerRow = 16;
    private const int CurrentViewportRowCount = 12;
    private readonly WorkbenchRawBinaryEditorSession _session = new();
    private RawBinaryEditorState _state = new(false, 0, 0, 0, 0, false);
    private HexEditorByteCellViewModel? _activeInlineEdit;
    private HexEditorByteCellViewModel? _selectedCell;
    private HexEditorViewportRowViewModel? _selectedRow;

    /// <summary>Creates the standalone raw-BIN workspace with its initial localized text bundle.</summary>
    public HexEditorWorkspaceViewModel(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        ColumnHeaders = [.. Enumerable.Range(0, 16).Select(index => new HexEditorColumnHeaderViewModel(index))];
        GoToCommand = new RelayCommand(GoToViewport);
        FindAsciiCommand = new AsyncRelayCommand(FindAsciiAsync, CanFindAscii);
        SetViewportStartRowCommand = new RelayCommand<int>(SetViewportStartRow);
        SelectByteCommand = new RelayCommand<HexEditorByteCellViewModel>(SelectByte);
        MoveSelectionCommand = new RelayCommand<int>(MoveSelection);
        BeginByteEditCommand = new RelayCommand<HexEditorByteCellViewModel>(BeginByteEdit);
        CommitByteEditCommand = new RelayCommand<HexEditorByteCellViewModel>(CommitByteEdit);
        CancelByteEditCommand = new RelayCommand<HexEditorByteCellViewModel>(CancelByteEdit);
        InsertZeroBeforeCommand = new RelayCommand<HexEditorByteCellViewModel>(InsertZeroBefore);
        InsertZeroAfterCommand = new RelayCommand<HexEditorByteCellViewModel>(InsertZeroAfter);
        RequestInsertBytesBeforeCommand = new RelayCommand<HexEditorByteCellViewModel>(RequestInsertBytesBefore);
        RequestInsertBytesAfterCommand = new RelayCommand<HexEditorByteCellViewModel>(RequestInsertBytesAfter);
        ConfirmInsertBytesCommand = new RelayCommand(ConfirmInsertBytes, CanConfirmInsertBytes);
        CancelInsertBytesCommand = new RelayCommand(CancelInsertBytes);
        DeleteByteCommand = new RelayCommand<HexEditorByteCellViewModel>(DeleteByte);
        SetByteToZeroCommand = new RelayCommand<HexEditorByteCellViewModel>(SetByteToZero);
        SetByteToFfCommand = new RelayCommand<HexEditorByteCellViewModel>(SetByteToFf);
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

    /// <summary>Gets the active localized text bundle.</summary>
    public ShellTextResources Text { get; private set; }

    /// <summary>Gets fixed byte-offset headers for the 16-column grid.</summary>
    public IReadOnlyList<HexEditorColumnHeaderViewModel> ColumnHeaders { get; }

    /// <summary>Gets the bounded current window of rows projected from the in-memory raw-BIN document.</summary>
    public HexEditorViewportRowCollection ViewportRows { get; } = [];

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
    public bool IsInlineEditActive => _activeInlineEdit is not null;

    /// <summary>True while an editor text box owns the keyboard, preserving its native editing shortcuts.</summary>
    [ObservableProperty]
    public partial bool IsTextEntryFocused { get; set; }

    /// <summary>Currently selected work-buffer address, retained while it is outside the visible row window.</summary>
    public string? SelectedByteAddress { get; private set; }

    /// <summary>Accessible value and change context for the currently selected byte.</summary>
    public string SelectedByteAccessibleLabel => _selectedCell?.AccessibleLabel ?? SelectedByteAddress ?? string.Empty;

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
    public string SuggestedOutputFileName => _session.SuggestedOutputFileName;

    /// <summary>Explicit navigation command for a requested address.</summary>
    public IRelayCommand GoToCommand { get; }

    /// <summary>Finds the next printable ASCII occurrence in the in-memory work buffer.</summary>
    public IAsyncRelayCommand FindAsciiCommand { get; }

    /// <summary>Moves the bounded viewport to a coalesced document-scrollbar row position.</summary>
    public IRelayCommand<int> SetViewportStartRowCommand { get; }

    /// <summary>Selects one current-data byte for focus, range defaults, and context actions.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> SelectByteCommand { get; }

    /// <summary>Moves the byte selection by a signed in-memory offset for keyboard navigation.</summary>
    public IRelayCommand<int> MoveSelectionCommand { get; }

    /// <summary>Starts direct two-character editing for one current-data byte.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> BeginByteEditCommand { get; }

    /// <summary>Commits the current inline byte value through the raw-BIN application session.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> CommitByteEditCommand { get; }

    /// <summary>Cancels one inline edit without changing the in-memory work buffer.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> CancelByteEditCommand { get; }

    /// <summary>Inserts one 00 byte before the selected current-data byte.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> InsertZeroBeforeCommand { get; }

    /// <summary>Inserts one 00 byte after the selected current-data byte.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> InsertZeroAfterCommand { get; }

    /// <summary>Deletes the selected current-data byte.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> DeleteByteCommand { get; }

    /// <summary>Sets the selected current-data byte to 00.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> SetByteToZeroCommand { get; }

    /// <summary>Sets the selected current-data byte to FF.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> SetByteToFfCommand { get; }

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
        UpdateSelection(FormatAddress(targetAddress));
        EditorStatus = CreateReadyStatus();
    }

    private void SelectByte(HexEditorByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference)
        {
            return;
        }

        RangeStartAddress = cell.Address;
        RangeEndAddress = cell.Address;
        UpdateSelection(cell.Address);
    }

    private void MoveSelection(int offsetDelta)
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

        UpdateSelection(FormatAddress(nextAddress));
    }

    private void BeginByteEdit(HexEditorByteCellViewModel? cell)
    {
        if (cell is null || !cell.IsEditable)
        {
            return;
        }

        if (_activeInlineEdit is not null && !ReferenceEquals(_activeInlineEdit, cell))
        {
            CancelByteEdit(_activeInlineEdit);
        }

        SelectByte(cell);
        cell.EditValue = cell.ValueHex;
        cell.IsEditing = true;
        _activeInlineEdit = cell;
        OnPropertyChanged(nameof(IsInlineEditActive));
    }

    private void CommitByteEdit(HexEditorByteCellViewModel? cell)
    {
        if (cell is null || !cell.IsEditable || !cell.IsEditing)
        {
            return;
        }

        RawBinaryEditorOperationResult result = _session.OverwriteByte(cell.Address, cell.EditValue);
        if (!result.Succeeded)
        {
            return;
        }

        cell.IsEditing = false;
        _activeInlineEdit = null;
        OnPropertyChanged(nameof(IsInlineEditActive));
        ApplySuccessfulOperation(result, cell.Address);
    }

    private void CancelByteEdit(HexEditorByteCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        cell.EditValue = cell.ValueHex;
        cell.IsEditing = false;
        if (ReferenceEquals(_activeInlineEdit, cell))
        {
            _activeInlineEdit = null;
            OnPropertyChanged(nameof(IsInlineEditActive));
        }
    }

    private void InsertZeroBefore(HexEditorByteCellViewModel? cell)
    {
        if (cell is not null && cell.IsEditable)
        {
            ApplyOperation(_session.InsertZeroBefore(cell.Address), cell.Address);
        }
    }

    private void InsertZeroAfter(HexEditorByteCellViewModel? cell)
    {
        if (cell is not null && cell.IsEditable)
        {
            string selectedAddress = TryParseAddressLabel(cell.Address, out long anchor)
                ? FormatAddress(checked(anchor + 1))
                : cell.Address;
            ApplyOperation(_session.InsertZeroAfter(cell.Address), selectedAddress);
        }
    }

    private void DeleteByte(HexEditorByteCellViewModel? cell)
    {
        if (cell is not null && cell.IsEditable)
        {
            ApplyOperation(_session.DeleteByte(cell.Address), cell.Address);
        }
    }

    private void SetByteToZero(HexEditorByteCellViewModel? cell)
    {
        if (cell is not null && cell.IsEditable)
        {
            ApplyOperation(_session.OverwriteByte(cell.Address, "00"), cell.Address);
        }
    }

    private void SetByteToFf(HexEditorByteCellViewModel? cell)
    {
        if (cell is not null && cell.IsEditable)
        {
            ApplyOperation(_session.OverwriteByte(cell.Address, "FF"), cell.Address);
        }
    }

    private void ApplyOverwriteRange()
    {
        ApplyRangeOperation(_session.OverwriteRange(RangeStartAddress, RangeEndAddress, RangeValue), RangeStartAddress);
    }

    private void ApplyFillRange()
    {
        ApplyRangeOperation(_session.FillRange(RangeStartAddress, RangeEndAddress, RangeValue), RangeStartAddress);
    }

    private void Undo()
    {
        IReadOnlyDictionary<string, VisibleByteFingerprint> before = CaptureVisibleByteFingerprints();
        RawBinaryEditorOperationResult result = _session.Undo();
        ApplyOperation(result, SelectedByteAddress);
        if (result.Succeeded)
        {
            PublishHistoryFeedback(before);
        }
    }

    private void Redo()
    {
        IReadOnlyDictionary<string, VisibleByteFingerprint> before = CaptureVisibleByteFingerprints();
        RawBinaryEditorOperationResult result = _session.Redo();
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
        RefreshViewportRows();

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
        RefreshViewportRows();
    }

    private void RefreshViewportRows()
    {
        if (!HasDocument || TotalRowCount == 0)
        {
            ViewportRows.ReplaceAll([]);
            return;
        }

        long address = checked((long)ViewportStartRow * BytesPerRow);
        RawBinaryEditorViewport viewport = _session.CreatePage(address, ViewportRowCount);
        if (!viewport.Succeeded)
        {
            ViewportRows.ReplaceAll([]);
            ClearSelection();
            EditorStatus = DescribeIssue(viewport.Issue!);
            return;
        }

        ViewportRows.ReplaceAll(viewport.Rows.Select(CreateCurrentRow));
        RestoreVisibleSelection();
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

    private HexEditorViewportRowViewModel CreateCurrentRow(RawBinaryEditorViewportRow row)
    {
        IReadOnlyList<HexEditorByteCellViewModel> bytes = [.. row.Bytes.Select(value => CreateViewportByte(value, isReference: false))];
        IReadOnlyList<HexEditorByteCellViewModel> originalBytes = row.HasChanges
            ? [.. row.Bytes.Select(value => CreateViewportByte(value, isReference: true))]
            : [];
        bool hasReferenceComparison = bytes.Any(cell => cell.IsDataChanged || cell.IsStructuralChanged);
        return new HexEditorViewportRowViewModel(
            FormatAddress(row.Address),
            bytes,
            originalBytes,
            row.OriginalAscii,
            row.CurrentAscii,
            row.HasChanges,
            hasReferenceComparison)
        {
            IsOriginalRowsVisible = IsOriginalRowsVisible,
        };
    }

    private HexEditorByteCellViewModel CreateViewportByte(RawBinaryEditorByte value, bool isReference)
    {
        HexEditorStructuralBoundaryInfo boundary = GetStructuralBoundary(value.Address);
        string original = value.OriginalValueAtAddress is byte originalAtAddress
            ? originalAtAddress.ToString("X2", CultureInfo.InvariantCulture)
            : "--";
        return new HexEditorByteCellViewModel(
            FormatAddress(value.Address),
            original,
            isReference ? original : value.CurrentValue.ToString("X2", CultureInfo.InvariantCulture),
            value.HasOriginalValueAtAddress,
            value.IsDataChanged,
            value.IsStructuralChanged,
            boundary.IsStart,
            boundary.IsEnd,
            boundary.Index,
            boundary.Label,
            isReference,
            !isReference && IsAsciiSearchMatch(value.Address));
    }

    private void UpdateSelection(string address)
    {
        HexEditorViewportRowViewModel? row = null;
        HexEditorByteCellViewModel? selected = null;
        if (TryParseAddressLabel(address, out long offset) &&
            TryGetRowIndex(offset, out int rowIndex) &&
            rowIndex - ViewportStartRow is int viewportRowIndex &&
            viewportRowIndex >= 0 &&
            viewportRowIndex < ViewportRows.Count)
        {
            row = ViewportRows[viewportRowIndex];
            selected = row.Bytes[(int)(offset & 0xF)];
        }

        if (_selectedCell is not null && !ReferenceEquals(_selectedCell, selected))
        {
            _selectedCell.IsSelected = false;
        }

        if (_selectedRow is not null && !ReferenceEquals(_selectedRow, row))
        {
            _selectedRow.IsSelected = false;
        }

        _selectedCell = selected;
        _selectedRow = row;
        SelectedByteAddress = TryParseAddressLabel(address, out long selectedAddress) &&
            TryGetRowIndex(selectedAddress, out _)
            ? FormatAddress(selectedAddress)
            : null;
        OnPropertyChanged(nameof(SelectedByteAddress));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
        if (selected is { })
        {
            selected.IsSelected = true;
        }

        if (row is { })
        {
            row.IsSelected = true;
        }

        int selectedColumn = SelectedByteAddress is not null && TryParseAddressLabel(SelectedByteAddress, out long selectedOffset)
            ? (int)(selectedOffset & 0xF)
            : -1;
        foreach (HexEditorColumnHeaderViewModel header in ColumnHeaders)
        {
            header.IsSelected = header.Index == selectedColumn;
        }
    }

    private void ClearSelection()
    {
        if (_selectedCell is { })
        {
            _selectedCell.IsSelected = false;
        }

        if (_selectedRow is { })
        {
            _selectedRow.IsSelected = false;
        }

        _selectedCell = null;
        _selectedRow = null;
        SelectedByteAddress = null;
        OnPropertyChanged(nameof(SelectedByteAddress));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
        foreach (HexEditorColumnHeaderViewModel header in ColumnHeaders)
        {
            header.IsSelected = false;
        }
    }

    private void RestoreVisibleSelection()
    {
        if (!string.IsNullOrWhiteSpace(SelectedByteAddress))
        {
            UpdateSelection(SelectedByteAddress);
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
