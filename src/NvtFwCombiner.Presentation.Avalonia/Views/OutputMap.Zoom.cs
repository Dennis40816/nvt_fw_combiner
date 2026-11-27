// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class OutputMap
{
    private void RenderZoom()
    {
        if (_parts is null || _zoomRebuildDepth > 0) { return; }
        FocusTarget? focus = FocusedTarget;
        var identity = new ZoomIdentity(Model, Labels, focus?.Key, focus?.Block?.Key);
        if (_zoomIdentity == identity)
        {
            if (focus is not null) { UpdateZoomCaption(focus); }
            return;
        }
        (string Key, string? Id, NavigationMethod Navigation)? savedFocus = CaptureFocus(_parts.Zoom);
        _zoomRebuildDepth++;
        try
        {
            BuildZoom(focus);
            // One invalidation point; pinning an existing preview must not replace the pressed button.
            _zoomIdentity = identity;
            RestoreFocus(savedFocus);
        }
        finally { _zoomRebuildDepth--; }
    }

    private void BuildZoom(FocusTarget? focus)
    {
        if (_parts is not { } parts) { return; }
        parts.ZoomContent.Children.Clear();
        AutomationProperties.SetName(parts.Zoom, Text.ZoomStrip);
        if (focus is null || Model is null || Overview.Length == 0 || !focus.Ranges.Any(r => Intersects(r, Overview)))
        {
            parts.ZoomContent.Children.Add(Label(Text.Hint, "outputMapCaption"));
            return;
        }
        Viewport viewport = FocusViewport(focus);
        OutputMapBlock? block = focus.Block;
        OutputMapSource? source = block is { Kind: OutputMapBlockKind.Copy, SourceKey: { } sourceKey }
            ? Model.Sources.FirstOrDefault(s => string.Equals(s.Key, sourceKey, StringComparison.Ordinal)) : null;
        Canvas output = Lane("OutputMap.ZoomBar", 40);
        if (source is { Length: >= 0 } && block is { SourceRange: { } readRange })
        {
            parts.ZoomContent.Children.Add(Label($"{source.FileName} · {Text.Read} {RangeLabel(readRange)}", "outputMapAddress"));
            Canvas sourceLane = Lane("OutputMap.SourceStrip", 22);
            var sourceFill = new Border { IsHitTestVisible = false };
            sourceFill.Classes.Add("outputMapFill");
            Resource(sourceFill, Border.BackgroundProperty, SourceBrush(block));
            AddRange(sourceLane, sourceFill, readRange, 12, top: 4);
            if (readRange.EndExclusive > source.Length)
            {
                AddMarker(sourceLane, source.Length, $"{Text.Error} · {Text.Read} {RangeLabel(readRange)}",
                    "outputMapSourceOverflow", height: 22, width: 8);
            }
            AutomationProperties.SetName(sourceLane, $"{source.FileName} · {Text.Read} {RangeLabel(readRange)}");
            parts.ZoomContent.Children.Add(Track(sourceLane, 22));
            var sourceViewport = new Viewport(0, source.Length);
            sourceLane.SizeChanged += (_, _) => LayoutLane(sourceLane, sourceViewport);
            Canvas connectors = Connectors(sourceLane, output, source, readRange, block.TargetRange, viewport);
            parts.ZoomContent.Children.Add(connectors);
        }
        else
        {
            string detail = focus.Region is { } region ? RegionDescription(region) : focus.Name;
            parts.ZoomContent.Children.Add(Label(detail, "outputMapAddress"));
        }
        TextBlock caption = Label(string.Empty, "outputMapAddress");
        caption.Classes.Add("outputMapZoomCaption");
        parts.ZoomContent.Children.Add(caption);
        Canvas regions = Lane("OutputMap.ZoomRegions", 18);
        BuildRegions(regions, viewport, "ZoomRegion");
        parts.ZoomContent.Children.Add(regions);
        BuildBar(output, viewport, "ZoomBlock");
        parts.ZoomContent.Children.Add(Track(output, 40));
        regions.SizeChanged += (_, _) => LayoutLane(regions, viewport);
        output.SizeChanged += (_, _) => LayoutLane(output, viewport);
        UpdateZoomCaption(focus);
    }

    private Viewport FocusViewport(FocusTarget focus)
    {
        // Fractional display coordinates keep even a one-byte focus at 90%, without inventing byte ranges.
        double start = Math.Max(0, focus.Ranges.Min(r => r.Start));
        double end = Math.Min(ScaleEnd, focus.Ranges.Max(r => r.EndExclusive));
        double padding = (end - start) / 18d;
        return new(Math.Max(0, start - padding), Math.Min(ScaleEnd, end + padding));
    }

    private void UpdateZoomCaption(FocusTarget focus)
    {
        if (_parts is not { } parts) { return; }
        TextBlock? caption = parts.ZoomContent.Children.OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("outputMapZoomCaption"));
        if (caption is null) { return; }
        Viewport viewport = FocusViewport(focus);
        string mode = focus.Key is null ? Text.Issue :
            string.Equals(focus.Key, SelectedKey, StringComparison.Ordinal) ? Text.Pinned : Text.Preview;
        caption.Text = $"{Text.Output} [{CoordinateLabel(Math.Floor(viewport.Start))}, {CoordinateLabel(Math.Ceiling(viewport.End))}) · {mode}";
        AutomationProperties.SetName(parts.Zoom, $"{Text.ZoomStrip} · {focus.Name} · {string.Join("; ", focus.Ranges.Select(OutputRange))} · {mode}");
    }

    private static Canvas Lane(string id, double height)
    {
        var canvas = new Canvas { Height = height };
        AutomationProperties.SetAutomationId(canvas, id);
        AutomationProperties.SetAccessibilityView(canvas, AccessibilityView.Content);
        return canvas;
    }

    private static Border Track(Canvas lane, double height)
    {
        var border = new Border { Height = height, Child = lane };
        border.Classes.Add("outputMapTrack");
        return border;
    }

    private static Canvas Connectors(Canvas sourceLane, Canvas outputLane, OutputMapSource source, ByteRange readRange,
        ByteRange targetRange, Viewport viewport)
    {
        var canvas = new Canvas { Height = 18, IsHitTestVisible = false };
        canvas.Classes.Add("outputMapConnectors");
        Line left = AddLine();
        Line right = AddLine();
        void ArrangeLines()
        {
            PlaceLine(left, source.Length > 0 ? Math.Clamp(readRange.Start / (double)source.Length, 0, 1) : 1,
                (Math.Max(targetRange.Start, viewport.Start) - viewport.Start) / viewport.Length);
            PlaceLine(right, source.Length > 0 ? Math.Clamp(readRange.EndExclusive / (double)source.Length, 0, 1) : 1,
                (Math.Min(targetRange.EndExclusive, viewport.End) - viewport.Start) / viewport.Length);
        }
        void PlaceLine(Line line, double sourceFraction, double targetFraction)
        {
            Point? start = sourceLane.TranslatePoint(new Point(sourceFraction * sourceLane.Bounds.Width, sourceLane.Bounds.Height), canvas);
            Point? end = outputLane.TranslatePoint(new Point(targetFraction * outputLane.Bounds.Width, 0), canvas);
            if (start is not { } a || end is not { } target) { return; }
            // The lines stop in their own row, so they never cross the output caption or region names.
            var b = new Point(target.X, canvas.Bounds.Height);
            if (line.StartPoint != a) { line.StartPoint = a; }
            if (line.EndPoint != b) { line.EndPoint = b; }
        }
        Line AddLine()
        {
            var line = new Line { StrokeThickness = 1 };
            Resource(line, Shape.StrokeProperty, "NfcTextMutedBrush");
            canvas.Children.Add(line);
            return line;
        }
        outputLane.LayoutUpdated += (_, _) => ArrangeLines();
        return canvas;
    }
}
