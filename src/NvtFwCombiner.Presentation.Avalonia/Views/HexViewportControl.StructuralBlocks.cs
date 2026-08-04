using Avalonia;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexViewportControl
{
    private void CollectStructuralVisualSegments(
        Dictionary<int, List<HexViewportStructuralVisualSegment>> blocks,
        HexViewportRow row,
        double y)
    {
        int start = 0;
        while (start < row.Cells.Count)
        {
            HexViewportCell firstCell = row.Cells[start];
            if (!firstCell.HasStructuralBlock)
            {
                start++;
                continue;
            }

            int blockIndex = firstCell.StructuralBlockIndex;
            int end = start + 1;
            while (end < row.Cells.Count && row.Cells[end].StructuralBlockIndex == blockIndex)
            {
                end++;
            }

            Rect first = GetAsciiCellRect(start, y).Deflate(1);
            Rect last = GetAsciiCellRect(end - 1, y).Deflate(1);
            bool showComparison = Snapshot is { } snapshot && IsComparisonRowVisible(snapshot, row);
            double height = (showComparison ? RowHeight * 2 : RowHeight) - 2;
            var segment = new HexViewportStructuralVisualSegment(
                new Rect(first.X, first.Y, last.Right - first.X, height),
                FormattableString.Invariant($"{blockIndex + 1:00}"),
                firstCell);
            if (!blocks.TryGetValue(blockIndex, out List<HexViewportStructuralVisualSegment>? segments))
            {
                segments = [];
                blocks.Add(blockIndex, segments);
            }

            segments.Add(segment);
            start = end;
        }
    }

    private void DrawAsciiStructuralBlocks(
        DrawingContext context,
        IReadOnlyDictionary<int, List<HexViewportStructuralVisualSegment>> blocks)
    {
        foreach (List<HexViewportStructuralVisualSegment> segments in blocks.Values)
        {
            if (segments.Count == 0)
            {
                continue;
            }

            if (segments.Count == 1)
            {
                DrawRoundedRectangle(context, null, StructuralPen, segments[0].Bounds, 3);
            }
            else
            {
                DrawWrappedStructuralOutline(context, segments);
            }

            DrawStructuralBlockLabel(context, segments[0].Label, segments[0].Bounds);
        }
    }

    private void DrawWrappedStructuralOutline(
        DrawingContext context,
        List<HexViewportStructuralVisualSegment> segments)
    {
        Rect first = segments[0].Bounds;
        Rect last = segments[^1].Bounds;
        double left = GetAsciiCellRect(0, 0).Deflate(1).Left;
        double right = GetAsciiCellRect(BytesPerRow - 1, 0).Deflate(1).Right;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext outline = geometry.Open())
        {
            outline.BeginFigure(first.TopLeft, isFilled: false);
            outline.LineTo(new Point(right, first.Top));
            outline.LineTo(new Point(right, last.Top));
            outline.LineTo(last.TopRight);
            outline.LineTo(last.BottomRight);
            outline.LineTo(new Point(left, last.Bottom));
            outline.LineTo(new Point(left, first.Bottom));
            outline.LineTo(first.BottomLeft);
            outline.EndFigure(isClosed: true);
        }

        context.DrawGeometry(null, StructuralPen, geometry);
    }

    private static void DrawStructuralBlockLabel(DrawingContext context, string label, Rect outline)
    {
        FormattedText text = CreateBoundaryText(label);
        Rect badge = new(outline.X + 2, outline.Y + 2, text.Width + 6, text.Height + 2);
        DrawRoundedRectangle(context, StructuralLabelBrush, null, badge, 3);
        context.DrawText(text, new Point(badge.X + 3, badge.Y + 1));
    }

    private bool TryHitTestStructuralAscii(Point point, out HexViewportCell cell, out Rect bounds)
    {
        cell = default;
        bounds = default;
        if (Snapshot is not { } snapshot)
        {
            return false;
        }

        var blocks = new Dictionary<int, List<HexViewportStructuralVisualSegment>>();
        double y = 0;
        foreach (HexViewportRow row in snapshot.Rows)
        {
            CollectStructuralVisualSegments(blocks, row, y);
            y += IsComparisonRowVisible(snapshot, row) ? RowHeight * 2 : RowHeight;
        }

        foreach (List<HexViewportStructuralVisualSegment> segments in blocks.Values)
        {
            if (segments.Count == 0)
            {
                continue;
            }

            Rect first = segments[0].Bounds;
            Rect last = segments[^1].Bounds;
            double left = GetAsciiCellRect(0, 0).Deflate(1).Left;
            double right = GetAsciiCellRect(BytesPerRow - 1, 0).Deflate(1).Right;
            Rect[] segmentBounds = [.. segments.Select(segment => segment.Bounds)];
            if (!ContainsStructuralPoint(segmentBounds, point, left, right))
            {
                continue;
            }

            cell = segments[0].Cell;
            bounds = new Rect(left, first.Top, right - left, last.Bottom - first.Top);
            return true;
        }

        return false;
    }

    private static bool ContainsStructuralPoint(Rect[] segments, Point point, double left, double right)
    {
        Rect first = segments[0];
        if (segments.Length == 1)
        {
            return ContainsInclusive(first, point);
        }

        Rect last = segments[^1];
        return point.Y >= first.Top && point.Y <= first.Bottom
            ? point.X >= first.Left && point.X <= right
            : point.Y > first.Bottom && point.Y < last.Top
                ? point.X >= left && point.X <= right
                : point.Y >= last.Top && point.Y <= last.Bottom && point.X >= left && point.X <= last.Right;
    }

    private static bool ContainsInclusive(Rect bounds, Point point)
    {
        return point.X >= bounds.Left && point.X <= bounds.Right &&
               point.Y >= bounds.Top && point.Y <= bounds.Bottom;
    }
}

internal sealed record HexViewportStructuralVisualSegment(
    Rect Bounds,
    string Label,
    HexViewportCell Cell);
