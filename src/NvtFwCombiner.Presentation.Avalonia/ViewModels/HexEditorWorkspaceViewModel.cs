using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Presentation state for the standalone raw-BIN Hex Editor utility. This workspace has no IC,
/// profile, flash-map, processor, or report behavior; it only projects one application-owned
/// in-memory binary editing session.
/// </summary>
public sealed partial class HexEditorWorkspaceViewModel : ObservableObject
{
    private const int ProgressiveRowsPerPage = 32;
    private readonly Dictionary<string, HexEditorViewportRowViewModel> _authoringRows =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly WorkbenchRawBinaryEditorSession _session = UiCompositionRunner.CreateRawBinaryEditorSession();
    private WorkbenchRawBinaryEditorState _state = new(false, 0, 0, 0, 0);
    private long _nextUnrenderedAddress;
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
        LoadNextPageCommand = new RelayCommand(LoadNextPage, () => HasMoreRows);
        SelectByteCommand = new RelayCommand<HexEditorByteCellViewModel>(SelectByte);
        BeginByteEditCommand = new RelayCommand<HexEditorByteCellViewModel>(BeginByteEdit);
        CommitByteEditCommand = new RelayCommand<HexEditorByteCellViewModel>(CommitByteEdit);
        CancelByteEditCommand = new RelayCommand<HexEditorByteCellViewModel>(CancelByteEdit);
        InsertZeroBeforeCommand = new RelayCommand<HexEditorByteCellViewModel>(InsertZeroBefore);
        InsertZeroAfterCommand = new RelayCommand<HexEditorByteCellViewModel>(InsertZeroAfter);
        DeleteByteCommand = new RelayCommand<HexEditorByteCellViewModel>(DeleteByte);
        SetByteToZeroCommand = new RelayCommand<HexEditorByteCellViewModel>(SetByteToZero);
        SetByteToFfCommand = new RelayCommand<HexEditorByteCellViewModel>(SetByteToFf);
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

    /// <summary>Gets virtualized rows currently visible in the raw-BIN viewport.</summary>
    public HexEditorViewportRowCollection ViewportRows { get; } = [];

    /// <summary>Gets or sets the address used by the explicit Go to command.</summary>
    [ObservableProperty]
    public partial string ViewportAddress { get; set; } = "0x000000";

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

    /// <summary>True while later raw-BIN rows remain queued for progressive background rendering.</summary>
    [ObservableProperty]
    public partial bool HasMoreRows { get; set; }

    /// <summary>True only while the shell exposes this utility page and may continue rendering later rows.</summary>
    [ObservableProperty]
    public partial bool IsPageActive { get; set; }

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

    /// <summary>Compact hexadecimal length of the in-memory work buffer.</summary>
    public string WorkingLengthLabel => FormattableString.Invariant($"0x{_state.WorkingLength:X} bytes");

    /// <summary>Number of retained in-memory edit operations.</summary>
    public int ChangeCount => _state.UndoCount;

    /// <summary>Suggested non-destructive output file name.</summary>
    public string SuggestedOutputFileName => _session.SuggestedOutputFileName;

    /// <summary>Explicit navigation command for a requested address.</summary>
    public IRelayCommand GoToCommand { get; }

    /// <summary>Appends the next fixed-size row page without rereading the source BIN.</summary>
    public IRelayCommand LoadNextPageCommand { get; }

    /// <summary>Selects one current-data byte for focus, range defaults, and context actions.</summary>
    public IRelayCommand<HexEditorByteCellViewModel> SelectByteCommand { get; }

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

    /// <summary>Loads one BIN once through the Bootstrap adapter into the editor-owned memory buffer.</summary>
    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        WorkbenchRawBinaryEditorFileResult result = await _session.LoadAsync(path, cancellationToken);
        if (!result.Succeeded || result.State is null || string.IsNullOrWhiteSpace(result.Path))
        {
            EditorStatus = result.ErrorMessage ?? Text.HexEditorFileOperationFailedDetail;
            return;
        }

        SourcePath = result.Path;
        ViewportAddress = "0x000000";
        RangeStartAddress = "0x000000";
        RangeEndAddress = "0x000000";
        RangeValue = string.Empty;
        ClearSelection();
        UpdateState(result.State);
        RefreshViewport();
    }

    /// <summary>Exports the current memory work buffer as a new BIN and never overwrites the opened source BIN.</summary>
    public async Task SaveAsAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!CanSave)
        {
            return;
        }

        WorkbenchRawBinaryEditorFileResult result = await _session.SaveAsAsync(outputPath, cancellationToken);
        if (!result.Succeeded || result.State is null || string.IsNullOrWhiteSpace(result.Path))
        {
            EditorStatus = result.ErrorMessage ?? Text.HexEditorFileOperationFailedDetail;
            return;
        }

        UpdateState(result.State);
        EditorStatus = string.Format(CultureInfo.InvariantCulture, Text.HexEditorSaveCompletedDetail, FirmwarePathDisplay.Normalize(result.Path));
    }

    /// <summary>Updates user-facing labels after a shell language change.</summary>
    public void ApplyTextResources(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(WorkingLengthLabel));
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
        RefreshViewport();
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
        cell.InlineValidationMessage = string.Empty;
        cell.IsEditing = true;
        _activeInlineEdit = cell;
    }

    private void CommitByteEdit(HexEditorByteCellViewModel? cell)
    {
        if (cell is null || !cell.IsEditable || !cell.IsEditing)
        {
            return;
        }

        WorkbenchRawBinaryEditorOperationResult result = _session.OverwriteByte(cell.Address, cell.EditValue);
        if (!result.Succeeded)
        {
            cell.InlineValidationMessage = DescribeIssue(result.Issue!);
            return;
        }

        cell.IsEditing = false;
        _activeInlineEdit = null;
        ApplySuccessfulOperation(result, cell.Address);
    }

    private void CancelByteEdit(HexEditorByteCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        cell.EditValue = cell.ValueHex;
        cell.InlineValidationMessage = string.Empty;
        cell.IsEditing = false;
        if (ReferenceEquals(_activeInlineEdit, cell))
        {
            _activeInlineEdit = null;
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
            ApplyOperation(_session.InsertZeroAfter(cell.Address), cell.Address);
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
        ApplyOperation(_session.OverwriteRange(RangeStartAddress, RangeEndAddress, RangeValue), RangeStartAddress);
    }

    private void ApplyFillRange()
    {
        ApplyOperation(_session.FillRange(RangeStartAddress, RangeEndAddress, RangeValue), RangeStartAddress);
    }

    private void Undo()
    {
        ApplyOperation(_session.Undo(), _selectedCell?.Address);
    }

    private void Redo()
    {
        ApplyOperation(_session.Redo(), _selectedCell?.Address);
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

    private void ApplyOperation(WorkbenchRawBinaryEditorOperationResult result, string? selectedAddress)
    {
        if (!result.Succeeded)
        {
            EditorStatus = DescribeIssue(result.Issue!);
            return;
        }

        ApplySuccessfulOperation(result, selectedAddress);
    }

    private void ApplySuccessfulOperation(WorkbenchRawBinaryEditorOperationResult result, string? selectedAddress)
    {
        UpdateState(result.State);
        if (!string.IsNullOrWhiteSpace(selectedAddress))
        {
            ViewportAddress = selectedAddress;
        }

        RefreshViewport();
        if (!string.IsNullOrWhiteSpace(selectedAddress))
        {
            UpdateSelection(selectedAddress);
        }
    }

    private void RefreshViewport()
    {
        string? selectedAddress = _selectedCell?.Address;
        WorkbenchRawBinaryEditorViewport viewport = _session.CreateViewport(ViewportAddress);
        UpdateState(viewport.State);
        if (!viewport.Succeeded)
        {
            ViewportRows.ReplaceAll([]);
            ClearSelection();
            HasMoreRows = false;
            EditorStatus = DescribeIssue(viewport.Issue!);
            return;
        }

        if (!TryReconcileViewport(viewport))
        {
            ReplaceViewportRows(CreateViewportRows(viewport));
        }

        _nextUnrenderedAddress = checked(viewport.Start + viewport.Length);
        HasMoreRows = _nextUnrenderedAddress < viewport.State.WorkingLength;
        if (!string.IsNullOrWhiteSpace(selectedAddress))
        {
            UpdateSelection(selectedAddress);
        }

        EditorStatus = CreateReadyStatus();
    }

    private void LoadNextPage()
    {
        if (!HasMoreRows)
        {
            return;
        }

        WorkbenchRawBinaryEditorViewport page = _session.CreatePage(_nextUnrenderedAddress, ProgressiveRowsPerPage);
        if (!page.Succeeded)
        {
            HasMoreRows = false;
            EditorStatus = DescribeIssue(page.Issue!);
            return;
        }

        List<HexEditorViewportRowViewModel> rows = CreateViewportRows(page);
        ViewportRows.AppendAll(rows);
        IndexAuthoringRows(rows);
        _nextUnrenderedAddress = checked(page.Start + page.Length);
        HasMoreRows = _nextUnrenderedAddress < page.State.WorkingLength;
        EditorStatus = CreateReadyStatus();
    }

    private bool TryReconcileViewport(WorkbenchRawBinaryEditorViewport viewport)
    {
        if (IsOriginalRowsVisible)
        {
            return false;
        }

        List<HexEditorViewportRowViewModel> currentRows = [.. ViewportRows.Where(row => !row.IsReferenceRow)];
        if (currentRows.Count != viewport.Rows.Count)
        {
            return false;
        }

        for (int index = 0; index < currentRows.Count; index++)
        {
            if (!string.Equals(currentRows[index].Address, FormatAddress(viewport.Rows[index].Address), StringComparison.Ordinal) ||
                currentRows[index].Bytes.Count != viewport.Rows[index].Bytes.Count)
            {
                return false;
            }
        }

        for (int rowIndex = 0; rowIndex < currentRows.Count; rowIndex++)
        {
            HexEditorViewportRowViewModel target = currentRows[rowIndex];
            WorkbenchRawBinaryEditorViewportRow source = viewport.Rows[rowIndex];
            for (int cellIndex = 0; cellIndex < target.Bytes.Count; cellIndex++)
            {
                HexEditorByteCellViewModel cell = target.Bytes[cellIndex];
                WorkbenchRawBinaryEditorByte value = source.Bytes[cellIndex];
                cell.OriginalHex = value.HasOriginalValue ? value.OriginalValue.ToString("X2", CultureInfo.InvariantCulture) : "--";
                cell.ValueHex = value.CurrentValue.ToString("X2", CultureInfo.InvariantCulture);
                cell.HasOriginalValue = value.HasOriginalValue;
                cell.IsChanged = value.IsChanged;
                if (!cell.IsEditing)
                {
                    cell.EditValue = cell.ValueHex;
                }
            }

            target.OriginalAscii = source.OriginalAscii;
            target.CurrentAscii = source.CurrentAscii;
            target.HasChanges = source.HasChanges;
        }

        return true;
    }

    private List<HexEditorViewportRowViewModel> CreateViewportRows(WorkbenchRawBinaryEditorViewport viewport)
    {
        var rows = new List<HexEditorViewportRowViewModel>();
        foreach (WorkbenchRawBinaryEditorViewportRow row in viewport.Rows)
        {
            HexEditorViewportRowViewModel authoringRow = CreateCurrentRow(row);
            rows.Add(authoringRow);
            if (IsOriginalRowsVisible && authoringRow.HasChanges)
            {
                rows.Add(CreateOriginalReferenceRow(authoringRow));
            }
        }

        return rows;
    }

    private static HexEditorViewportRowViewModel CreateCurrentRow(WorkbenchRawBinaryEditorViewportRow row)
    {
        return new HexEditorViewportRowViewModel(
            FormatAddress(row.Address),
            [.. row.Bytes.Select(value => new HexEditorByteCellViewModel(
                FormatAddress(value.Address),
                value.HasOriginalValue ? value.OriginalValue.ToString("X2", CultureInfo.InvariantCulture) : "--",
                value.CurrentValue.ToString("X2", CultureInfo.InvariantCulture),
                value.HasOriginalValue,
                value.IsChanged,
                isReference: false))],
            row.OriginalAscii,
            row.CurrentAscii,
            isReferenceRow: false,
            row.HasChanges);
    }

    private static HexEditorViewportRowViewModel CreateOriginalReferenceRow(HexEditorViewportRowViewModel currentRow)
    {
        return new HexEditorViewportRowViewModel(
            currentRow.Address,
            [.. currentRow.Bytes.Select(cell => new HexEditorByteCellViewModel(
                cell.Address,
                cell.OriginalHex,
                cell.OriginalHex,
                cell.HasOriginalValue,
                isChanged: false,
                isReference: true))],
            currentRow.OriginalAscii,
            currentRow.OriginalAscii,
            isReferenceRow: true,
            hasChanges: false);
    }

    private void ReplaceViewportRows(IReadOnlyList<HexEditorViewportRowViewModel> rows)
    {
        ViewportRows.ReplaceAll(rows);
        _authoringRows.Clear();
        IndexAuthoringRows(rows);
        _selectedCell = null;
        _selectedRow = null;
    }

    private void IndexAuthoringRows(IEnumerable<HexEditorViewportRowViewModel> rows)
    {
        foreach (HexEditorViewportRowViewModel row in rows.Where(row => !row.IsReferenceRow))
        {
            _authoringRows[row.Address] = row;
        }
    }

    private void UpdateSelection(string address)
    {
        HexEditorViewportRowViewModel? row = null;
        HexEditorByteCellViewModel? selected = null;
        if (TryParseAddressLabel(address, out long offset) &&
            _authoringRows.TryGetValue(FormatAddress(offset & ~0xFL), out row))
        {
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
        if (selected is { })
        {
            selected.IsSelected = true;
        }

        if (row is { })
        {
            row.IsSelected = true;
        }

        int selectedColumn = TryParseAddressLabel(address, out long selectedOffset)
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
        foreach (HexEditorColumnHeaderViewModel header in ColumnHeaders)
        {
            header.IsSelected = false;
        }
    }

    private void UpdateState(WorkbenchRawBinaryEditorState state)
    {
        _state = state;
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(WorkingLengthLabel));
        OnPropertyChanged(nameof(ChangeCount));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        RequestSaveCommand.NotifyCanExecuteChanged();
        LoadNextPageCommand.NotifyCanExecuteChanged();
    }

}
