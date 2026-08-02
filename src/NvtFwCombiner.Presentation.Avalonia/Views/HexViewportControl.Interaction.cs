using Avalonia;
using Avalonia.Input;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexViewportControl
{
    internal void UpdateHoveredCell(Point point)
    {
        long? nextAddress = (TryHitTest(point, out HexViewportCell cell, out _) ||
                             TryHitTestAscii(point, out cell, out _) ||
                             TryHitTestStructuralAscii(point, out cell, out _))
            ? cell.Address
            : null;
        if (HoveredAddress == nextAddress)
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
        if (Snapshot is null || e.Delta.Y == 0)
        {
            return;
        }

        const int rowsPerWheelStep = 3;
        int rowDelta = e.Delta.Y < 0 ? rowsPerWheelStep : -rowsPerWheelStep;
        RaiseIntent(new HexViewportInteractionIntent(
            HexViewportInteractionTrigger.Scroll,
            null,
            default,
            rowDelta));
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Snapshot is null)
        {
            return;
        }

        Point point = e.GetPosition(this);
        bool isRightButton = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (isRightButton && TryHitTestStructuralAscii(point, out HexViewportCell structuralCell, out Rect structuralBounds))
        {
            _ = Focus();
            RaiseIntent(new HexViewportInteractionIntent(
                HexViewportInteractionTrigger.StructuralContext,
                structuralCell.Address,
                structuralBounds,
                StructuralBlockIndex: structuralCell.StructuralBlockIndex));
            e.Handled = true;
            return;
        }

        if (!TryHitTest(point, out HexViewportCell cell, out Rect bounds) &&
            !TryHitTestAscii(point, out cell, out bounds))
        {
            return;
        }

        _ = Focus();
        RaiseIntent(new HexViewportInteractionIntent(HexViewportInteractionTrigger.Select, cell.Address, bounds));
        if (isRightButton)
        {
            RaiseIntent(new HexViewportInteractionIntent(HexViewportInteractionTrigger.Context, cell.Address, bounds));
        }

        e.Handled = true;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TryHitTest(e.GetPosition(this), out HexViewportCell cell, out Rect bounds))
        {
            RaiseIntent(new HexViewportInteractionIntent(HexViewportInteractionTrigger.Activate, cell.Address, bounds));
            e.Handled = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Snapshot is not { } snapshot)
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
            RaiseIntent(new HexViewportInteractionIntent(
                HexViewportInteractionTrigger.MoveSelection,
                snapshot.SelectedAddress,
                default,
                delta.Value));
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.F2 &&
            snapshot.SelectedAddress is long selected &&
            TryGetCellBounds(selected, out Rect bounds))
        {
            RaiseIntent(new HexViewportInteractionIntent(HexViewportInteractionTrigger.Activate, selected, bounds));
            e.Handled = true;
        }
    }

    private bool TryHitTest(Point point, out HexViewportCell cell, out Rect bounds)
    {
        cell = default;
        bounds = default;
        if (Snapshot is not { } snapshot)
        {
            return false;
        }

        double cellStart = GetByteStart();
        double cellWidth = GetCellWidth();
        double y = 0;
        foreach (HexViewportRow row in snapshot.Rows)
        {
            if (point.Y >= y && point.Y < y + RowHeight)
            {
                int index = ResolveCellIndex(point, cellStart, cellWidth, row.Cells.Count, y, RowHeight);
                if (index >= 0)
                {
                    cell = row.Cells[index];
                    bounds = GetCellRect(index, y);
                    return true;
                }

                return false;
            }

            y += RowHeight;
            if (IsComparisonRowVisible(snapshot, row))
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

    private bool TryHitTestAscii(Point point, out HexViewportCell cell, out Rect bounds)
    {
        cell = default;
        bounds = default;
        double asciiStart = GetAsciiCellRect(0, 0).Left;
        double asciiEnd = GetAsciiCellRect(BytesPerRow - 1, 0).Right;
        if (Snapshot is not { } snapshot || point.X < asciiStart || point.X >= asciiEnd)
        {
            return false;
        }

        double cellWidth = GetAsciiCellRect(0, 0).Width;
        double y = 0;
        foreach (HexViewportRow row in snapshot.Rows)
        {
            if (point.Y >= y && point.Y < y + RowHeight)
            {
                int index = ResolveCellIndex(point, asciiStart, cellWidth, row.Cells.Count, y, RowHeight);
                if (index >= 0)
                {
                    cell = row.Cells[index];
                    bounds = GetAsciiCellRect(index, y);
                    return true;
                }

                return false;
            }

            y += IsComparisonRowVisible(snapshot, row) ? RowHeight * 2 : RowHeight;
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

    private void RaiseIntent(HexViewportInteractionIntent intent)
    {
        InteractionRequested?.Invoke(this, new HexViewportInteractionEventArgs(intent));
    }
}
