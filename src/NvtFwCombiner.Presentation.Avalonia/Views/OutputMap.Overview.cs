// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class OutputMap
{
    private void Rebuild()
    {
        if (_parts is not { } parts) { return; }
        (string Key, string? Id, NavigationMethod Navigation)? savedFocus = CaptureFocus(this);
        _zoomRebuildDepth++;
        try
        {
            this.FindControl<TextBlock>("MapTitle")!.Text = Text.Title;
            long length = Model?.OutputLength ?? 0;
            string rangeLabel = Model is null ? string.Empty : $"[{Hex(0)}, {Hex(length)})";
            this.FindControl<TextBlock>("MapRange")!.Text = rangeLabel;
            this.FindControl<TextBlock>("AxisStart")!.Text = Model is null ? string.Empty : Hex(0);
            this.FindControl<TextBlock>("AxisMiddle")!.Text = Model is null ? string.Empty : Hex(ScaleEnd / 2);
            this.FindControl<TextBlock>("AxisEnd")!.Text = Model is null ? string.Empty : Hex(ScaleEnd);
            AutomationProperties.SetName(parts.Bar, Model is null ? Text.Title : $"{Text.Title} · {Model.AddressSpaceId} {rangeLabel}");
            AutomationProperties.SetName(parts.Zoom, Text.ZoomStrip);
            BuildRegions(parts.Regions, Overview, "Region");
            BuildBar(parts.Bar, Overview, "Block");
            BuildCrc();
            parts.Jump.Children.Clear();
            parts.Jump.IsVisible = Model?.Regions.Count > 0;
            if (Model is { Regions.Count: > 0 } model)
            {
                parts.Jump.Children.Add(Label(Text.ZoomTo, "outputMapCaption"));
                foreach (OutputMapRegion region in model.Regions)
                {
                    Button button = Target(region.Key, region.Name, $"OutputMap.ZoomTo.{region.Key}", RegionDescription(region));
                    button.Classes.Add("outputMapJump");
                    parts.Jump.Children.Add(button);
                }
            }
            LayoutOverview();
        }
        finally { _zoomRebuildDepth--; }
        RefreshInteraction();
        RestoreFocus(savedFocus);
    }

    private void BuildRegions(Canvas lane, Viewport viewport, string idPrefix)
    {
        lane.Children.Clear();
        lane.IsVisible = Model?.Regions.Count > 0;
        if (Model is null || viewport.Length <= 0) { return; }
        foreach (OutputMapRegion region in Model.Regions.Where(r => Intersects(r.Range, viewport)))
        {
            double visibleLength = Math.Min(region.Range.EndExclusive, viewport.End) - Math.Max(region.Range.Start, viewport.Start);
            var content = new Grid();
            if (region.IsReadOnly == true)
            {
                var hatch = new Border { IsHitTestVisible = false };
                Resource(hatch, Border.BackgroundProperty, "OutputMapReadOnlyHatch");
                content.Children.Add(hatch);
            }
            TextBlock label = Label(visibleLength / viewport.Length > 0.08 ? region.Name : string.Empty, "outputMapCaption");
            label.TextWrapping = TextWrapping.NoWrap;
            label.Margin = new Thickness(4, 0, 0, 0);
            content.Children.Add(label);
            Button button = Target(region.Key, content, $"OutputMap.{idPrefix}.{region.Key}", RegionDescription(region),
                previews: !IsStrip(idPrefix));
            button.Classes.Add("outputMapRegion");
            AddRange(lane, button, region.Range, 18);
        }
    }

    private void BuildBar(Canvas lane, Viewport viewport, string idPrefix)
    {
        lane.Children.Clear();
        if (Model is null || viewport.Length <= 0) { return; }
        foreach (OutputMapBlock block in Model.Blocks.Where(b => Intersects(b.TargetRange, viewport)))
        {
            var fill = new Border { IsHitTestVisible = false };
            fill.Classes.Add("outputMapFill");
            Resource(fill, Border.BackgroundProperty, SourceBrush(block));
            AddRange(lane, fill, block.TargetRange, 30, top: 4);
            Button hit = Target(block.Key, null, $"OutputMap.{idPrefix}.{block.Key}", BlockDescription(block), previews: !IsStrip(idPrefix));
            hit.Classes.Add("outputMapBlock");
            AddRange(lane, hit, block.TargetRange, 38, minimumWidth: 8);
        }
        if (Model.Highlight is { } highlight)
        {
            foreach (ByteRange range in highlight.Ranges.Where(r => Intersects(r, viewport)))
            {
                var overlay = new Border { IsHitTestVisible = false };
                overlay.Classes.Add("outputMapHighlight");
                bool error = highlight.Severity == OutputMapHighlightSeverity.Error;
                Resource(overlay, Border.BorderBrushProperty, error ? "NfcCriticalBorderBrush" : "NfcWarningAccentStrongBrush");
                Resource(overlay, Border.BackgroundProperty, error ? "OutputMapErrorHatch" : "OutputMapWarningHatch");
                AutomationProperties.SetName(overlay, $"{(error ? Text.Error : Text.Warning)} · {OutputRange(range)}");
                AddRange(lane, overlay, range, 40);
            }
        }
        foreach (OutputMapCrc crc in Model.CrcAnnotations)
        {
            if (crc.StorageRange is { } storage && Intersects(storage, viewport))
            {
                AddMarker(lane, storage.Start, $"{crc.Name} · {Text.Storage} · {OutputRange(storage)}", "outputMapCrcStorage");
            }
        }
        if (Model.Version is { } version && Intersects(version.Range, viewport))
        {
            AddMarker(lane, version.Range.Start, VersionDescription(version), "outputMapVersion");
        }
        AddOutputEnd(lane, viewport);
    }

    private string BlockDescription(OutputMapBlock block)
    {
        string description = $"{block.Label} · {Text.Kind(block.Kind)} · {OutputRange(block.TargetRange)}";
        if (Model is null) { return description; }
        foreach (OutputMapCrc crc in Model.CrcAnnotations)
        {
            if (crc.StorageRange is { } storage && storage.Overlaps(block.TargetRange))
            {
                description += $" · {crc.Name} · {Text.Storage} · {OutputRange(storage)}";
            }
        }
        if (Model.Version is { } version && version.Range.Overlaps(block.TargetRange))
        {
            description += $" · {VersionDescription(version)}";
        }
        return description;
    }

    private void AddOutputEnd(Canvas lane, Viewport viewport)
    {
        if (Model is not { } model || model.OutputLength >= ScaleEnd ||
            model.OutputLength < viewport.Start || model.OutputLength > viewport.End) { return; }
        string description = $"{Text.End} {Hex(model.OutputLength)}";
        AddMarker(lane, model.OutputLength, description, "outputMapEnd");
        TextBlock label = Label(description, "outputMapCaption");
        label.TextWrapping = TextWrapping.NoWrap;
        label.IsHitTestVisible = false;
        label.DataContext = new Placement(model.OutputLength, model.OutputLength, 0, false);
        Canvas.SetTop(label, -16);
        lane.Children.Add(label);
    }

    private static bool IsStrip(string idPrefix)
    {
        return idPrefix.StartsWith("Zoom", StringComparison.Ordinal);
    }

    private void BuildCrc()
    {
        if (_parts is not { } parts) { return; }
        parts.Crc.Children.Clear();
        if (Model is null) { return; }
        int row = 0;
        // The filter has its own line: coverage evidence cannot pair two branching methods on one source line.
        IEnumerable<OutputMapCrc> covered = Model.CrcAnnotations.Where(
            c => c.CoveredRange is { } r && Intersects(r, Overview));
        foreach (OutputMapCrc crc in covered)
        {
            ByteRange range = crc.CoveredRange!.Value;
            var bracket = new Border { BorderThickness = new Thickness(1, 0, 1, 1), IsHitTestVisible = false };
            bracket.Classes.Add("outputMapCrcBracket");
            Resource(bracket, Border.BorderBrushProperty, "NfcTextMutedBrush");
            AddRange(parts.Crc, bracket, range, 7, top: row * 20);
            TextBlock label = Label(crc.Name, "outputMapCaption");
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextAlignment = global::Avalonia.Media.TextAlignment.Center;
            ToolTip.SetTip(label, $"{crc.Name} · {OutputRange(range)}");
            AddRange(parts.Crc, label, range, 16, top: (row * 20) + 5);
            row++;
        }
        parts.Crc.IsVisible = row > 0;
        parts.Crc.Height = 18 + (Math.Max(0, row - 1) * 20);
    }

    private string RegionDescription(OutputMapRegion region)
    {
        string description = $"{region.Name} · {OutputRange(region.Range)}";
        return region.IsReadOnly switch
        {
            true => $"{description} · {Text.ReadOnly}",
            false => $"{description} · {Text.Writable}",
            null => description,
        };
    }

    private string VersionDescription(OutputMapVersion version)
    {
        string name = version.ValueText is null ? version.Name : $"{version.Name} · {version.ValueText}";
        return $"{name} · {OutputRange(version.Range)}";
    }

    private string SourceBrush(OutputMapBlock block)
    {
        if (block.Kind == OutputMapBlockKind.Fill || Model is null) { return "NfcMemoryNeutralFillBrush"; }
        int index = Model.Sources.ToList().FindIndex(s => string.Equals(s.Key, block.SourceKey, StringComparison.Ordinal));
        return index < 0 ? "NfcMemoryNeutralFillBrush" : (index % 4) switch
        {
            0 => "NfcMemorySourceFillBrush",
            1 => "NfcMemoryTpBackupFillBrush",
            2 => "NfcMemoryLdcFillBrush",
            _ => "NfcMemoryDpFillBrush",
        };
    }

    private static TextBlock Label(string text, string style)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add(style);
        return label;
    }

    private static void Resource(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }

    private static bool Intersects(ByteRange range, Viewport viewport)
    {
        return range.Length > 0 && range.Start < viewport.End && range.EndExclusive > viewport.Start;
    }

    private static void AddRange(Canvas lane, Control child, ByteRange range, double height,
        double minimumWidth = 0, double top = 0)
    {
        child.DataContext = new Placement(range.Start, range.EndExclusive, minimumWidth, false);
        child.Height = height;
        Canvas.SetTop(child, top);
        lane.Children.Add(child);
    }

    private static void LayoutLane(Canvas lane, Viewport viewport)
    {
        foreach (Control child in lane.Children)
        {
            if (child.DataContext is not Placement placement) { continue; }
            double left = viewport.Length > 0
                ? (Math.Clamp(placement.Start, viewport.Start, viewport.End) - viewport.Start) / viewport.Length * lane.Bounds.Width : lane.Bounds.Width;
            double right = viewport.Length > 0
                ? (Math.Clamp(placement.End, viewport.Start, viewport.End) - viewport.Start) / viewport.Length * lane.Bounds.Width : lane.Bounds.Width;
            double width = placement.Marker ? placement.MinimumWidth : Math.Max(placement.MinimumWidth, right - left);
            double position = placement.Marker ? left - (width / 2) : ((left + right - width) / 2);
            // Measuring before attachment would freeze the label without its styles.
            if (child is TextBlock && TopLevel.GetTopLevel(lane) is not null)
            {
                child.Width = double.NaN;
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = Math.Min(child.DesiredSize.Width, lane.Bounds.Width);
                position = Math.Clamp((left + right - width) / 2, 0, Math.Max(0, lane.Bounds.Width - width));
            }
            else if (placement.Marker) { position = Math.Clamp(position, 0, Math.Max(0, lane.Bounds.Width - width)); }
            Canvas.SetLeft(child, position);
            child.Width = Math.Max(0, width);
        }
    }

    private static void AddMarker(Canvas lane, long address, string description, string style, double height = 40, double width = 2)
    {
        var marker = new Border { IsHitTestVisible = false, Height = height, DataContext = new Placement(address, address, width, true) };
        marker.Classes.Add(style);
        ToolTip.SetTip(marker, description);
        AutomationProperties.SetName(marker, description);
        if (style is "outputMapVersion" or "outputMapCrcStorage")
        {
            // The visual stays above the writes; its tooltip target yields to every block hit target.
            var target = new Border
            {
                Background = Brushes.Transparent,
                Height = height,
                DataContext = new Placement(address, address, 8, true),
                ZIndex = -1,
            };
            ToolTip.SetTip(target, description);
            lane.Children.Add(target);
        }
        lane.Children.Add(marker);
    }

    private void LayoutOverview()
    {
        if (_parts is not { } parts) { return; }
        LayoutLane(parts.Regions, Overview);
        LayoutLane(parts.Bar, Overview);
        LayoutLane(parts.Crc, Overview);
    }
}
