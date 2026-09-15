using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Draws memory-overlay connectors from screen geometry; owns no interaction or firmware state.</summary>
internal static class MemoryCoverageConnectorVisuals
{
    internal static Canvas LocalConnector(double anchor, double height)
    {
        var canvas = new Canvas { Name = "MemoryLocalConnector", Height = height, ClipToBounds = false };
        canvas.Children.Add(Connector(new Point(anchor, 0), new Point(anchor, height), "NfcTextStrongBrush"));
        return canvas;
    }

    internal static Line Connector(Point start, Point end, string strokeResource = "NfcBorderMutedBrush")
    {
        var line = new Line { StartPoint = start, EndPoint = end, StrokeThickness = 1, IsHitTestVisible = false };
        _ = line.Bind(Shape.StrokeProperty, new DynamicResourceExtension(strokeResource));
        return line;
    }

    internal static Canvas CardConnector(double anchor, double height, bool above, IReadOnlyList<Rect> labels, string namePrefix = "MemoryCard")
    {
        var canvas = new Canvas { Height = height, ClipToBounds = false };
        double edge = above ? -1 : height + 1;
        double tip = above ? 6 : height - 6;
        double terminal = above ? height : 0;
        var points = new List<Point> { new(anchor - 6, edge), new(anchor, tip), new(anchor + 6, edge) };
        var fill = new Polygon { Name = namePrefix + "Notch", Points = points, IsHitTestVisible = false };
        _ = fill.Bind(Shape.FillProperty, new DynamicResourceExtension("NfcMemoryInteractionSurfaceBrush"));
        canvas.Children.Add(fill);
        var outline = new Polyline { Points = points, StrokeThickness = 1, IsHitTestVisible = false };
        _ = outline.Bind(Shape.StrokeProperty, new DynamicResourceExtension("NfcAccentBorderBrush"));
        canvas.Children.Add(outline);
        // A left-edge slice may align with local text. Interrupt only the decorative
        // stem behind those glyph bounds; its endpoint still identifies the exact slice.
        // Connect the selected leaf directly to the card notch.
        double cursor = Math.Min(tip, terminal);
        double limit = Math.Max(tip, terminal);
        foreach (Rect label in labels.Where(rect => anchor >= rect.Left - 2 && anchor <= rect.Right + 2).OrderBy(static rect => rect.Top))
        {
            double start = Math.Clamp(label.Top - 2, cursor, limit);
            if (start > cursor) { canvas.Children.Add(Connector(new Point(anchor, cursor), new Point(anchor, start), "NfcTextStrongBrush")); }
            cursor = Math.Clamp(label.Bottom + 2, start, limit);
        }
        if (cursor < limit) { canvas.Children.Add(Connector(new Point(anchor, cursor), new Point(anchor, limit), "NfcTextStrongBrush")); }
        var dot = new Ellipse { Name = namePrefix + "Anchor", Width = 4, Height = 4, StrokeThickness = 1, IsHitTestVisible = false };
        _ = dot.Bind(Shape.StrokeProperty, new DynamicResourceExtension("NfcAccentStrongBrush"));
        _ = dot.Bind(Shape.FillProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
        Canvas.SetLeft(dot, anchor - 2);
        Canvas.SetTop(dot, terminal - 2);
        canvas.Children.Add(dot);
        return canvas;
    }
}
