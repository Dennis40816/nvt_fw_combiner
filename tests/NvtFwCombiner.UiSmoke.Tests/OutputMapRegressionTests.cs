// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regressions for the output map's overflow, labels and input lifetime.</summary>
public sealed class OutputMapRegressionTests
{
    /// <summary>Writes and issues extend the scale while the actual output end stays explicit.</summary>
    [AvaloniaTheory]
    [InlineData(1000, false, "end 0x003E8")]
    [InlineData(0, true, "結尾 0x00000")]
    public void OverflowRangesExtendTheScaleAndKeepOutputEndVisible(long outputLength, bool chinese, string endLabel)
    {
        var model = new OutputMapModel("out", outputLength,
            blocks: [new("past", new ByteRange(900, 500), OutputMapBlockKind.Fill, "Past end")],
            highlight: new OutputMapHighlight([new ByteRange(1500, 100)], OutputMapHighlightSeverity.Error));
        using var host = new OutputMapTestHost(model, chinese: chinese);
        Button block = host.Block("past");
        Border end = Assert.Single(host.Bar.Children.OfType<Border>(), b => b.Classes.Contains("outputMapEnd"));
        Assert.InRange(Math.Abs(block.Bounds.Width - (host.Bar.Bounds.Width * 500 / 1600)), 0, 1);
        Assert.InRange(Math.Abs(end.Bounds.Center.X - (host.Bar.Bounds.Width * outputLength / 1600)), 0, 1);
        Assert.Contains(host.Bar.Children.OfType<TextBlock>(), t => t.Text == endLabel);
        Assert.Equal("0x00320", host.Map.FindControl<TextBlock>("AxisMiddle")!.Text);
        Assert.Equal("0x00640", host.Map.FindControl<TextBlock>("AxisEnd")!.Text);
        Assert.Contains(host.Map.GetVisualDescendants().OfType<TextBlock>(), t =>
            t.Classes.Contains("outputMapZoomCaption") && t.Text!.Contains("0x00640", StringComparison.Ordinal));
        host.Map.HoveredKey = "past";
        host.Layout();
        Canvas zoom = host.Find<Canvas>("OutputMap.ZoomBar");
        Assert.True(host.Find<Button>("OutputMap.ZoomBlock.past").IsEffectivelyVisible);
        Assert.Contains(host.Map.GetVisualDescendants().OfType<TextBlock>(), t =>
            t.Classes.Contains("outputMapZoomCaption") && t.Text!.Contains("[0x00368, 0x00594)", StringComparison.Ordinal));
        Assert.InRange(zoom.Bounds.Width, 1, host.Map.Bounds.Width);
    }

    /// <summary>Bindable state reaches buttons that have not yet joined the visual tree.</summary>
    [AvaloniaFact]
    public void InitialKeysAndReducedMotionReachOverviewButtons()
    {
        using var host = new OutputMapTestHost(selectedKey: "tp", hoveredKey: "dp");
        Assert.Contains("pinned", host.Block("tp").Classes);
        Assert.Contains("preview", host.Block("dp").Classes);
        Assert.All(host.Map.GetVisualDescendants().OfType<Button>(), b =>
        {
            Assert.Contains("reducedMotion", b.Classes);
            Assert.Empty(b.Transitions!);
        });
    }

    /// <summary>Hover wins over an issue, and an issue wins over an existing pin.</summary>
    [AvaloniaFact]
    public void IssueFocusPrecedesPinUntilAnotherTargetIsHovered()
    {
        using var host = new OutputMapTestHost(OutputMapTestHost.Example(includeHighlight: true), selectedKey: "dp");
        Assert.Contains("Issue", AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
        // A pin outside the issue ranges brings no source strip with it.
        Assert.DoesNotContain(host.Map.GetVisualDescendants().OfType<Canvas>(), c => c.Classes.Contains("outputMapConnectors"));
        host.Map.HoveredKey = "tp";
        host.Layout();
        Assert.Contains("TP code", AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
        host.Map.HoveredKey = null;
        host.Layout();
        Assert.Contains("Issue", AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
        Assert.Equal("dp", host.Map.SelectedKey);
    }

    /// <summary>The mouse can cross empty card space to the zoom strip; pointer focus cannot restore a departed preview.</summary>
    [AvaloniaFact]
    public void PreviewSurvivesTravelToZoomAndClearsOnControlExit()
    {
        using var host = new OutputMapTestHost();
        Point overview = host.Bounds(host.Block("tp")).Center;
        host.Window.MouseMove(overview, RawInputModifiers.None);
        host.Layout();
        host.Window.MouseMove(host.Bounds(host.Map.FindControl<TextBlock>("MapRange")!).Center, RawInputModifiers.None);
        host.Layout();
        Assert.Equal("tp", host.Map.HoveredKey);
        host.Window.MouseMove(overview, RawInputModifiers.None);
        host.Window.MouseDown(overview, MouseButton.Left);
        host.Window.MouseUp(overview, MouseButton.Left);
        host.Map.SelectedKey = null;
        Point zoom = host.Bounds(host.Find<Button>("OutputMap.ZoomBlock.tp")).Center;
        host.Window.MouseMove(zoom, RawInputModifiers.None);
        host.Window.MouseDown(zoom, MouseButton.Left);
        host.Window.MouseUp(zoom, MouseButton.Left);
        Assert.Equal("tp", host.Map.SelectedKey);
        host.Window.MouseMove(overview, RawInputModifiers.None);
        host.Window.MouseDown(overview, MouseButton.Left);
        host.Window.MouseUp(overview, MouseButton.Left);
        host.Map.SelectedKey = null;
        host.Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
        host.Layout();
        Assert.Null(host.Map.HoveredKey);
        Assert.Null(host.Map.SelectedKey);
        Assert.DoesNotContain(host.Map.GetVisualDescendants().OfType<Canvas>(), c =>
            AutomationProperties.GetAutomationId(c) == "OutputMap.ZoomBar");
    }

    /// <summary>A narrow CRC uses natural text width, centered and clamped to its lane; region names stay on one line.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(999)]
    public void NarrowCrcLabelKeepsNaturalWidthInsideItsLane(long start)
    {
        using var host = new OutputMapTestHost(new OutputMapModel("out", 1000,
            regions: [new("region", "A long region name", new ByteRange(0, 1000))],
            crcAnnotations: [new("A readable CRC name", new ByteRange(start, 1))]));
        Canvas lane = host.Map.FindControl<Canvas>("MapCrc")!;
        Border bracket = Assert.Single(lane.Children.OfType<Border>());
        TextBlock label = Assert.Single(lane.Children.OfType<TextBlock>());
        var natural = new TextBlock { Text = label.Text, FontFamily = label.FontFamily, FontSize = label.FontSize };
        natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double expectedLeft = Math.Clamp(bracket.Bounds.Center.X - (natural.DesiredSize.Width / 2),
            0, lane.Bounds.Width - natural.DesiredSize.Width);
        Assert.Equal(TextWrapping.NoWrap, label.TextWrapping);
        Assert.InRange(Math.Abs(label.Bounds.Width - natural.DesiredSize.Width), 0, 1);
        Assert.InRange(Math.Abs(label.Bounds.Left - expectedLeft), 0, 1);
        Assert.InRange(label.Bounds.Right, 0, lane.Bounds.Width + 1);
        Assert.Equal(TextWrapping.NoWrap, Assert.Single(host.Region("region").GetVisualDescendants().OfType<TextBlock>()).TextWrapping);
    }

    /// <summary>Escape removes the zoom button but preserves keyboard focus on the same overview key.</summary>
    [AvaloniaFact]
    public void EscapeFromZoomReturnsFocusToTheSameOverviewKey()
    {
        using var host = new OutputMapTestHost(selectedKey: "tp");
        Assert.True(host.Find<Button>("OutputMap.ZoomBlock.fill").Focus(NavigationMethod.Tab));
        host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        host.Layout();
        Assert.True(host.Block("fill").IsFocused);
        Assert.Null(host.Map.SelectedKey);
        Assert.Null(host.Map.HoveredKey);
    }

    /// <summary>A new snapshot restores the same automation target, falling back to the overview when needed.</summary>
    [AvaloniaTheory]
    [InlineData("OutputMap.Block.fill", 100, "OutputMap.Block.fill")]
    [InlineData("OutputMap.ZoomBlock.fill", 0x3C000, "OutputMap.ZoomBlock.fill")]
    [InlineData("OutputMap.ZoomBlock.fill", 100, "OutputMap.Block.fill")]
    public void ModelReplacementRestoresFocusByKeyAndAutomationId(string focusedId, long tpLength, string expectedId)
    {
        using var host = new OutputMapTestHost(selectedKey: "tp");
        Assert.True(host.Find<Button>(focusedId).Focus(NavigationMethod.Tab));
        host.Map.Model = new OutputMapModel("out", 0x40000,
            blocks: [new("tp", new ByteRange(0, tpLength), OutputMapBlockKind.Fill, "TP code"),
                new("fill", new ByteRange(0x3C000, 0x1000), OutputMapBlockKind.Fill, "Fill")]);
        host.Layout();
        Assert.True(host.Find<Button>(expectedId).IsFocused);
    }

    /// <summary>An idle map lets Escape bubble to its host.</summary>
    [AvaloniaTheory]
    [InlineData(null, null, false)]
    [InlineData("tp", null, true)]
    [InlineData(null, "tp", true)]
    public void EscapeIsHandledOnlyWhenPinOrPreviewWasCleared(string? selectedKey, string? hoveredKey, bool handled)
    {
        using var host = new OutputMapTestHost(selectedKey: selectedKey, hoveredKey: hoveredKey);
        var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        host.Map.RaiseEvent(escape);
        Assert.Equal(handled, escape.Handled);
        Assert.Null(host.Map.SelectedKey);
        Assert.Null(host.Map.HoveredKey);
    }

    /// <summary>Partial, wholly past-end and empty-source reads expose the source boundary error.</summary>
    [AvaloniaTheory]
    [InlineData(1000, 900)]
    [InlineData(1000, 1200)]
    [InlineData(0, 0)]
    public void SourceReadOverflowDrawsAnErrorMarkerAtTheSourceEnd(long sourceLength, long readStart)
    {
        using var host = new OutputMapTestHost(new OutputMapModel("out", 1000,
            blocks: [new("copy", new ByteRange(400, 200), OutputMapBlockKind.Copy, "Copy", "src", new ByteRange(readStart, 200))],
            sources: [new("src", "short.bin", sourceLength)]), selectedKey: "copy");
        Canvas lane = host.Find<Canvas>("OutputMap.SourceStrip");
        Border error = Assert.Single(lane.Children.OfType<Border>(), b => b.Classes.Contains("outputMapSourceOverflow"));
        Assert.True(error.IsEffectivelyVisible);
        Assert.InRange(Math.Abs(error.Bounds.Right - lane.Bounds.Width), 0, 1);
        Assert.InRange(error.Bounds.Width, 7, 9);
        Border read = Assert.Single(lane.Children.OfType<Border>(), b => b.Classes.Contains("outputMapFill"));
        Assert.InRange(read.Bounds.Left, 0, lane.Bounds.Width + 1);
        Assert.InRange(read.Bounds.Right, 0, lane.Bounds.Width + 1);
        Assert.True(error.TryFindResource("NfcCriticalBorderBrush", error.ActualThemeVariant, out object? errorBrush));
        Assert.Same(errorBrush, error.Background);
        Assert.Contains("Error", AutomationProperties.GetName(error), StringComparison.Ordinal);
    }

    /// <summary>Storage and version annotations do not intercept the underlying block's pointer input.</summary>
    [AvaloniaTheory]
    [InlineData("outputMapCrcStorage", "TP CRC", "patch")]
    [InlineData("outputMapVersion", "dp.version", "dp")]
    public void AnnotationMarkersLetTheUnderlyingBlockReceivePointerInput(string markerClass, string annotation, string key)
    {
        using var host = new OutputMapTestHost();
        Border marker = Assert.Single(host.Bar.Children.OfType<Border>(), b => b.Classes.Contains(markerClass));
        Point point = host.Bounds(marker).Center;
        host.Window.MouseMove(point, RawInputModifiers.None);
        Assert.Equal(key, host.Map.HoveredKey);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        Assert.Equal(key, host.Map.SelectedKey);
        Assert.False(marker.IsHitTestVisible);
        Assert.Contains(annotation, AutomationProperties.GetName(host.Block(key)), StringComparison.Ordinal);
        Assert.Equal(AutomationProperties.GetName(host.Block(key)), ToolTip.GetTip(host.Block(key)));
    }
}
