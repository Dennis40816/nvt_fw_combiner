using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    private const double HexViewportRowHeight = 25;
    private IReadOnlyList<WorkbenchRawBinaryEditorChangedRange> _changedRanges = [];
    private int _currentViewportRowCapacity = CurrentViewportRowCount;
    private int _selectedChangedRangeIndex = -1;

    /// <summary>Display rows for every contiguous edited block in current address order.</summary>
    public IReadOnlyList<HexEditorChangedBlockViewModel> ChangedBlocks { get; private set; } = [];

    /// <summary>Logical raw-document rows projected per viewport; original rows share the same fixed height.</summary>
    private int ViewportRowCount => CalculateViewportRowCount();

    /// <summary>Current physical height reserved for the bounded document and compact change inspector.</summary>
    [ObservableProperty]
    public partial double HexViewportHeight { get; private set; } = 300;

    /// <summary>Number of contiguous current-buffer regions that differ from the loaded document.</summary>
    public int ChangedBlockCount => _changedRanges.Count;

    /// <summary>True when a changed region can be selected from the compact inspector control.</summary>
    public bool HasChangedBlocks => ChangedBlockCount > 0;

    /// <summary>True before the first retained in-memory difference exists.</summary>
    public bool HasNoChangedBlocks => !HasChangedBlocks;

    /// <summary>Cycles through edited regions and frames the next region in the document viewport.</summary>
    public IRelayCommand SelectNextChangedBlockCommand { get; }

    /// <summary>Frames one selected edited block from the inspector list.</summary>
    public IRelayCommand<HexEditorChangedBlockViewModel> SelectChangedBlockCommand { get; }

    /// <summary>Moves the document viewport to the first current address in one edited block.</summary>
    public IRelayCommand<HexEditorChangedBlockViewModel> GoToChangedBlockStartCommand { get; }

    /// <summary>Moves the document viewport to the last current address in one edited block.</summary>
    public IRelayCommand<HexEditorChangedBlockViewModel> GoToChangedBlockEndCommand { get; }

    /// <summary>Fits the read-only drawing window into the available workbench height without materializing the document.</summary>
    public void SetViewportHeight(double availableHeight)
    {
        double nextHeight = Math.Clamp(Math.Floor(availableHeight), 300, 720);
        int displayRows = Math.Max(8, (int)Math.Floor(nextHeight / HexViewportRowHeight));
        int nextCurrentCapacity = displayRows;
        if (Math.Abs(HexViewportHeight - nextHeight) < 1 &&
            _currentViewportRowCapacity == nextCurrentCapacity)
        {
            return;
        }

        HexViewportHeight = nextHeight;
        _currentViewportRowCapacity = nextCurrentCapacity;
        OnPropertyChanged(nameof(VisibleRowCount));
        OnPropertyChanged(nameof(DocumentScrollMaximum));
        if (HasDocument)
        {
            ViewportStartRow = Math.Min(ViewportStartRow, DocumentScrollMaximum);
            RefreshViewportRows();
        }
    }

    private void RefreshChangeTracking()
    {
        _changedRanges = HasDocument ? _session.GetChangedRanges() : [];
        ChangedBlocks = [.. _changedRanges.Select(CreateChangedBlock)];
        if (_selectedChangedRangeIndex >= _changedRanges.Count)
        {
            _selectedChangedRangeIndex = -1;
        }

        OnPropertyChanged(nameof(ChangedBlockCount));
        OnPropertyChanged(nameof(HasChangedBlocks));
        OnPropertyChanged(nameof(HasNoChangedBlocks));
        OnPropertyChanged(nameof(ChangedBlocks));
        OnPropertyChanged(nameof(VisibleRowCount));
        OnPropertyChanged(nameof(DocumentScrollMaximum));
        SelectNextChangedBlockCommand.NotifyCanExecuteChanged();
    }

    private HexEditorChangedBlockViewModel CreateChangedBlock(
        WorkbenchRawBinaryEditorChangedRange range,
        int index)
    {
        HexEditorStructuralBoundaryInfo boundary = GetStructuralBoundary(range);
        long displayStart = boundary.IsValid ? boundary.StartAddress : range.Start;
        long displayEnd = boundary.IsValid ? boundary.EndAddress : Math.Max(range.Start, range.EndExclusive - 1);
        return new HexEditorChangedBlockViewModel(
            index,
            FormatAddress(displayStart),
            FormatAddress(displayEnd),
            FormattableString.Invariant($"0x{range.Length:X} bytes"),
            range.ChangeKind,
            CreateChangedBlockReason(range));
    }

    private void ResetSearchAndChanges()
    {
        ClearAsciiSearchResults(refreshViewport: false);
        _changedRanges = [];
        ChangedBlocks = [];
        _selectedChangedRangeIndex = -1;
        OnPropertyChanged(nameof(ChangedBlockCount));
        OnPropertyChanged(nameof(HasChangedBlocks));
        OnPropertyChanged(nameof(HasNoChangedBlocks));
        OnPropertyChanged(nameof(ChangedBlocks));
        OnPropertyChanged(nameof(VisibleRowCount));
        OnPropertyChanged(nameof(DocumentScrollMaximum));
        SelectNextChangedBlockCommand.NotifyCanExecuteChanged();
    }

    private int CalculateViewportRowCount()
    {
        int remainingRows = Math.Max(0, TotalRowCount - ViewportStartRow);
        if (remainingRows == 0)
        {
            return 0;
        }

        int physicalRows = 0;
        int logicalRows = 0;
        while (logicalRows < remainingRows)
        {
            int rowIndex = ViewportStartRow + logicalRows;
            int rowCost = IsOriginalRowsVisible && ShouldShowOriginalRow(rowIndex) ? 2 : 1;
            if (physicalRows + rowCost > _currentViewportRowCapacity)
            {
                break;
            }

            physicalRows += rowCost;
            logicalRows++;
        }

        return Math.Max(1, logicalRows);
    }

    private int CalculateDocumentScrollMaximum()
    {
        if (TotalRowCount == 0)
        {
            return 0;
        }

        int physicalRows = 0;
        int trailingLogicalRows = 0;
        for (int rowIndex = TotalRowCount - 1; rowIndex >= 0; rowIndex--)
        {
            int rowCost = IsOriginalRowsVisible && ShouldShowOriginalRow(rowIndex) ? 2 : 1;
            if (physicalRows + rowCost > _currentViewportRowCapacity)
            {
                break;
            }

            physicalRows += rowCost;
            trailingLogicalRows++;
        }

        return Math.Max(0, TotalRowCount - Math.Max(1, trailingLogicalRows));
    }

    private bool ShouldShowOriginalRow(int rowIndex)
    {
        long rowStart = checked((long)rowIndex * BytesPerRow);
        long rowEnd = rowStart + BytesPerRow;
        foreach (WorkbenchRawBinaryEditorChangedRange range in _changedRanges)
        {
            if (range.ValueChanges.Any(change => change.Start < rowEnd && change.EndExclusive > rowStart))
            {
                return true;
            }

            HexEditorStructuralBoundaryInfo boundary = GetStructuralBoundary(range);
            if (boundary.IsValid &&
                boundary.StartAddress < rowEnd &&
                boundary.EndAddress >= rowStart)
            {
                return true;
            }
        }

        return false;
    }

    private HexEditorStructuralBoundaryInfo GetStructuralBoundary(long address)
    {
        for (int index = 0; index < _changedRanges.Count; index++)
        {
            WorkbenchRawBinaryEditorChangedRange range = _changedRanges[index];
            HexEditorStructuralBoundaryInfo boundary = GetStructuralBoundary(range);
            if (boundary.IsValid && address >= boundary.StartAddress && address <= boundary.EndAddress)
            {
                return boundary with
                {
                    Index = index,
                    IsStart = address == boundary.StartAddress,
                    IsEnd = address == boundary.EndAddress,
                    Label = FormattableString.Invariant($"{index + 1:00}"),
                };
            }
        }

        return HexEditorStructuralBoundaryInfo.None;
    }

    private HexEditorStructuralBoundaryInfo GetStructuralBoundary(WorkbenchRawBinaryEditorChangedRange range)
    {
        if ((range.ChangeKind & WorkbenchRawBinaryEditorChangeKind.Structural) == 0 || _state.WorkingLength == 0)
        {
            return HexEditorStructuralBoundaryInfo.None;
        }

        long lastWorkingAddress = _state.WorkingLength - 1;
        return new HexEditorStructuralBoundaryInfo(
            Math.Clamp(range.Start, 0, lastWorkingAddress),
            Math.Clamp(range.EndExclusive - 1, 0, lastWorkingAddress),
            false,
            false,
            -1,
            string.Empty);
    }

    private string CreateChangedBlockReason(WorkbenchRawBinaryEditorChangedRange range)
    {
        var reasons = new List<string>();
        long valueChangeCount = range.ValueChanges.Sum(change => change.Length);
        if (valueChangeCount > 0)
        {
            WorkbenchRawBinaryEditorValueChange first = range.ValueChanges[0];
            string address = FormatAddress(first.Start);
            string before = first.FirstOriginalValue.ToString("X2", CultureInfo.InvariantCulture);
            string after = first.FirstCurrentValue.ToString("X2", CultureInfo.InvariantCulture);
            reasons.Add(valueChangeCount == 1
                ? string.Format(CultureInfo.InvariantCulture, Text.HexEditorChangedBlockValueReasonSingleTemplate, address, before, after)
                : string.Format(CultureInfo.InvariantCulture, Text.HexEditorChangedBlockValueReasonMultipleTemplate, valueChangeCount, address, before, after));
        }

        foreach (WorkbenchRawBinaryEditorStructuralChange change in range.StructuralChanges)
        {
            string template = change.Kind == WorkbenchRawBinaryEditorStructuralChangeKind.Insert
                ? Text.HexEditorChangedBlockInsertReasonTemplate
                : Text.HexEditorChangedBlockDeleteReasonTemplate;
            reasons.Add(string.Format(
                CultureInfo.InvariantCulture,
                template,
                change.Count,
                FormatAddress(change.Address)));
        }

        return reasons.Count == 0 ? Text.HexEditorChangedBlockReasonFallback : string.Join(Environment.NewLine, reasons);
    }

    private void SelectNextChangedBlock()
    {
        if (_changedRanges.Count == 0 || _state.WorkingLength == 0)
        {
            return;
        }

        _selectedChangedRangeIndex = (_selectedChangedRangeIndex + 1) % _changedRanges.Count;
        SelectChangedBlockAt(_selectedChangedRangeIndex);
    }

    private void SelectChangedBlock(HexEditorChangedBlockViewModel? block)
    {
        if (block is not null)
        {
            SelectChangedBlockAt(block.Index);
        }
    }

    private void GoToChangedBlockStart(HexEditorChangedBlockViewModel? block)
    {
        GoToChangedBlockBoundary(block, useEnd: false);
    }

    private void GoToChangedBlockEnd(HexEditorChangedBlockViewModel? block)
    {
        GoToChangedBlockBoundary(block, useEnd: true);
    }

    private void GoToChangedBlockBoundary(HexEditorChangedBlockViewModel? block, bool useEnd)
    {
        if (block is null || _state.WorkingLength == 0)
        {
            return;
        }

        string address = useEnd ? block.EndAddress : block.StartAddress;
        if (!TryParseAddressLabel(address, out long targetAddress) ||
            !TryGetRowIndex(targetAddress, out int rowIndex))
        {
            return;
        }

        _selectedChangedRangeIndex = block.Index;
        ViewportAddress = address;
        SetViewportStartRow(Math.Max(0, rowIndex - 4));
        UpdateSelection(address);
        EditorStatus = string.Format(
            CultureInfo.InvariantCulture,
            Text.HexEditorChangedBlockSelectedDetail,
            block.Index + 1,
            _changedRanges.Count,
            block.StartAddress,
            block.EndAddress);
    }

    private void SelectChangedBlockAt(int index)
    {
        if (index < 0 || index >= _changedRanges.Count || _state.WorkingLength == 0)
        {
            return;
        }

        _selectedChangedRangeIndex = index;
        WorkbenchRawBinaryEditorChangedRange range = _changedRanges[index];
        long selectedAddress = Math.Clamp(range.Start, 0, _state.WorkingLength - 1);
        int row = checked((int)(selectedAddress / BytesPerRow));
        SetViewportStartRow(Math.Max(0, row - 4));

        string start = FormatAddress(selectedAddress);
        long lastAddress = Math.Clamp(range.EndExclusive - 1, selectedAddress, _state.WorkingLength - 1);
        RangeStartAddress = start;
        RangeEndAddress = FormatAddress(lastAddress);
        UpdateSelection(start);
        EditorStatus = string.Format(
            CultureInfo.InvariantCulture,
            Text.HexEditorChangedBlockSelectedDetail,
            _selectedChangedRangeIndex + 1,
            _changedRanges.Count,
            start,
            RangeEndAddress);
    }
}

/// <summary>One human-readable navigation row for a contiguous raw-memory change.</summary>
public sealed record HexEditorChangedBlockViewModel(
    int Index,
    string StartAddress,
    string EndAddress,
    string LengthLabel,
    WorkbenchRawBinaryEditorChangeKind ChangeKind,
    string ReasonTooltip)
{
    /// <summary>One-based compact row identifier.</summary>
    public string IndexLabel => FormattableString.Invariant($"{Index + 1:00}");

    /// <summary>Inclusive hexadecimal address span shown in the inspector.</summary>
    public string RangeLabel => $"{StartAddress} - {EndAddress}";

    /// <summary>True when this block includes same-address byte value differences.</summary>
    public bool HasDataChanges => (ChangeKind & WorkbenchRawBinaryEditorChangeKind.Data) != 0;

    /// <summary>True when this block includes insert/delete source-address shifting.</summary>
    public bool HasStructuralChanges => (ChangeKind & WorkbenchRawBinaryEditorChangeKind.Structural) != 0;
}

internal sealed record HexEditorStructuralBoundaryInfo(
    long StartAddress,
    long EndAddress,
    bool IsStart,
    bool IsEnd,
    int Index,
    string Label)
{
    public static HexEditorStructuralBoundaryInfo None { get; } = new(-1, -1, false, false, -1, string.Empty);

    public bool IsValid => StartAddress >= 0 && EndAddress >= 0;
}
