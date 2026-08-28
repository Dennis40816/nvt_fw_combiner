using System.Globalization;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class HexEditorWorkspaceViewModel
{
    private HexViewportRow CreateViewportRow(RawBinaryEditorViewportRow row)
    {
        var cells = new HexViewportCell[row.Bytes.Count];
        for (int index = 0; index < cells.Length; index++)
        {
            cells[index] = CreateViewportCell(row.Bytes[index]);
        }

        return HexViewportRow.CreateOwned(row.Address, cells);
    }

    private HexViewportCell CreateViewportCell(RawBinaryEditorByte value)
    {
        HexEditorStructuralBoundaryInfo boundary = GetStructuralBoundary(value.Address);
        HexViewportCellDecoration decorations = HexViewportCellDecoration.None;
        if (value.IsDataChanged)
        {
            decorations |= HexViewportCellDecoration.DataChange;
        }

        if (value.IsStructuralChanged)
        {
            decorations |= HexViewportCellDecoration.StructuralChange;
        }

        if (boundary.IsStart)
        {
            decorations |= HexViewportCellDecoration.StructuralBoundaryStart;
        }

        if (boundary.IsEnd)
        {
            decorations |= HexViewportCellDecoration.StructuralBoundaryEnd;
        }

        if (IsAsciiSearchMatch(value.Address))
        {
            decorations |= HexViewportCellDecoration.Search;
        }

        if (HistoryFeedbackAddresses.Contains(value.Address))
        {
            decorations |= HexViewportCellDecoration.HistoryFeedback;
        }

        return new HexViewportCell(
            value.Address,
            value.CurrentValue,
            value.OriginalValueAtAddress,
            decorations,
            boundary.Index);
    }

    private void PublishViewportSnapshot(HexViewportSnapshot snapshot)
    {
        if (ReferenceEquals(CurrentViewportSnapshot, snapshot))
        {
            return;
        }

        if (!ReferenceEquals(CurrentViewportSnapshot.Rows, snapshot.Rows) ||
            CurrentViewportSnapshot.DecorationVersion != snapshot.DecorationVersion)
        {
            _selectionSnapshots = null;
            _selectionAddressLabels = null;
            _unselectedSnapshot = null;
        }

        CurrentViewportSnapshot = snapshot;
        OnPropertyChanged(nameof(ViewportSnapshot));
    }

    private HexViewportSnapshot GetSelectionSnapshot(long? selectedAddress)
    {
        if (CurrentViewportSnapshot.SelectedAddress == selectedAddress)
        {
            return CurrentViewportSnapshot;
        }

        if (selectedAddress is null)
        {
            return _unselectedSnapshot ??= CurrentViewportSnapshot.WithSelectedAddress(null);
        }

        _selectionSnapshots ??= [];
        if (CurrentViewportSnapshot.SelectedAddress is long currentAddress)
        {
            _ = _selectionSnapshots.TryAdd(currentAddress, CurrentViewportSnapshot);
        }

        if (!_selectionSnapshots.TryGetValue(selectedAddress.Value, out HexViewportSnapshot? snapshot))
        {
            snapshot = CurrentViewportSnapshot.WithSelectedAddress(selectedAddress);
            _selectionSnapshots.Add(selectedAddress.Value, snapshot);
        }

        return snapshot;
    }

    private string GetSelectionAddressLabel(long address)
    {
        _selectionAddressLabels ??= [];
        if (!_selectionAddressLabels.TryGetValue(address, out string? label))
        {
            label = FormatAddress(address);
            _selectionAddressLabels.Add(address, label);
        }

        return label;
    }

    internal bool TryGetViewportCell(long address, out HexViewportCell cell)
    {
        foreach (HexViewportRow row in CurrentViewportSnapshot.Rows)
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

    private bool TryGetViewportCell(string? address, out HexViewportCell cell)
    {
        cell = default;
        return TryParseAddressLabel(address ?? string.Empty, out long parsed) &&
               TryGetViewportCell(parsed, out cell);
    }

    internal string GetCurrentHex(long address)
    {
        return TryGetViewportCell(address, out HexViewportCell cell)
            ? cell.PrimaryValue.ToString("X2", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string CreateAccessibleLabel(HexViewportCell cell)
    {
        string address = FormatAddress(cell.Address);
        string current = cell.PrimaryValue.ToString("X2", CultureInfo.InvariantCulture);
        string original = cell.ComparisonValue?.ToString("X2", CultureInfo.InvariantCulture) ?? "--";
        return cell.IsDataChanged
            ? $"{address}: {original} changed to {current}"
            : cell.IsStructuralChanged
                ? $"{address}: {current}, source address shifted"
                : $"{address}: {current}";
    }
}
