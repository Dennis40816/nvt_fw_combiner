using Avalonia;
using Avalonia.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexEditorViewportControl
{
    internal void UpdateHoveredCell(Point point)
    {
        string? nextAddress = (TryHitTest(point, out HexEditorByteCellViewModel? cell, out _) ||
                               TryHitTestAscii(point, out cell, out _) ||
                               TryHitTestStructuralAscii(point, out cell, out _))
            ? cell!.Address
            : null;
        if (string.Equals(HoveredAddress, nextAddress, StringComparison.Ordinal))
        {
            return;
        }

        HoveredAddress = nextAddress;
        InvalidateVisual();
    }

    internal void ClearHoveredCell()
    {
        if (HoveredAddress is null)
        {
            return;
        }

        HoveredAddress = null;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerWheelChanged(e);
        if (_workspace is null || e.Delta.Y == 0)
        {
            return;
        }

        const int rowsPerWheelStep = 3;
        int rowDelta = e.Delta.Y < 0 ? rowsPerWheelStep : -rowsPerWheelStep;
        ScrollRequested?.Invoke(this, new HexEditorViewportScrollEventArgs(rowDelta));
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_workspace is not { } workspace)
        {
            return;
        }

        Point point = e.GetPosition(this);
        bool isRightButton = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (isRightButton &&
            TryHitTestStructuralAscii(point, out HexEditorByteCellViewModel? structuralCell, out Rect structuralBounds))
        {
            _ = Focus();
            StructuralBlockContextMenuRequested?.Invoke(
                this,
                new HexEditorViewportCellEventArgs(structuralCell!, structuralBounds));
            e.Handled = true;
            return;
        }

        if (!TryHitTest(point, out HexEditorByteCellViewModel? cell, out Rect bounds) &&
            !TryHitTestAscii(point, out cell, out bounds))
        {
            return;
        }

        _ = Focus();
        workspace.SelectByteCommand.Execute(cell);
        InvalidateVisual();
        if (isRightButton)
        {
            ContextMenuRequested?.Invoke(this, new HexEditorViewportCellEventArgs(cell!, bounds));
        }

        e.Handled = true;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TryHitTest(e.GetPosition(this), out HexEditorByteCellViewModel? cell, out Rect bounds))
        {
            EditRequested?.Invoke(this, new HexEditorViewportCellEventArgs(cell!, bounds));
            e.Handled = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_workspace is not { } workspace)
        {
            return;
        }

        int? delta = null;
        if (e.Key == Key.Left)
        {
            delta = -1;
        }
        else if (e.Key == Key.Right)
        {
            delta = 1;
        }
        else if (e.Key == Key.Up)
        {
            delta = -BytesPerRow;
        }
        else if (e.Key == Key.Down)
        {
            delta = BytesPerRow;
        }

        if (delta is not null)
        {
            workspace.MoveSelectionCommand.Execute(delta.Value);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.F2 && FindSelectedCell() is { } selected &&
            TryGetCellBounds(selected, out Rect bounds))
        {
            EditRequested?.Invoke(this, new HexEditorViewportCellEventArgs(selected, bounds));
            e.Handled = true;
        }
    }

    private bool TryHitTest(Point point, out HexEditorByteCellViewModel? cell, out Rect bounds)
    {
        cell = null;
        bounds = default;
        if (_workspace is null)
        {
            return false;
        }

        double cellStart = GetByteStart();
        double cellWidth = GetCellWidth();
        double y = 0;
        foreach (HexEditorViewportRowViewModel row in _workspace.ViewportRows)
        {
            if (point.Y >= y && point.Y < y + RowHeight)
            {
                int index = ResolveCellIndex(point, cellStart, cellWidth, row.Bytes.Count, y, RowHeight);
                if (index >= 0)
                {
                    cell = row.Bytes[index];
                    bounds = GetCellRect(index, y);
                    return true;
                }

                return false;
            }

            y += RowHeight;
            if (row.IsOriginalRowVisible)
            {
                if (point.Y >= y && point.Y < y + RowHeight)
                {
                    return false;
                }

                y += RowHeight;
            }
        }

        return false;
    }

    private bool TryHitTestAscii(
        Point point,
        out HexEditorByteCellViewModel? cell,
        out Rect bounds)
    {
        cell = null;
        bounds = default;
        double asciiStart = GetAsciiCellRect(0, 0).Left;
        double asciiEnd = GetAsciiCellRect(BytesPerRow - 1, 0).Right;
        if (_workspace is null || point.X < asciiStart || point.X >= asciiEnd)
        {
            return false;
        }

        double cellWidth = GetAsciiCellRect(0, 0).Width;
        double y = 0;
        foreach (HexEditorViewportRowViewModel row in _workspace.ViewportRows)
        {
            if (point.Y >= y && point.Y < y + RowHeight)
            {
                int index = ResolveCellIndex(point, asciiStart, cellWidth, row.Bytes.Count, y, RowHeight);
                if (index >= 0)
                {
                    cell = row.Bytes[index];
                    bounds = GetAsciiCellRect(index, y);
                    return true;
                }

                return false;
            }

            y += row.IsOriginalRowVisible ? RowHeight * 2 : RowHeight;
        }

        return false;
    }

    internal static int ResolveCellIndex(
        Point point,
        double cellStart,
        double cellWidth,
        int cellCount,
        double rowTop,
        double rowHeight)
    {
        return cellWidth <= 0 || cellCount <= 0 || rowHeight <= 0 ||
            point.X < cellStart || point.X >= cellStart + (cellWidth * cellCount) ||
            point.Y < rowTop || point.Y >= rowTop + rowHeight
            ? -1
            : (int)((point.X - cellStart) / cellWidth);
    }

    private HexEditorByteCellViewModel? FindSelectedCell()
    {
        return _workspace?.ViewportRows
            .SelectMany(row => row.Bytes)
            .FirstOrDefault(cell => cell.IsSelected);
    }
}
