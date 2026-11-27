// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Measures the real control, including independent pixel snapping (one-pixel tolerance).</summary>
public sealed class OutputMapGeometryTests
{
    /// <summary>Reference anchors and byte proportions survive both supported widths.</summary>
    [AvaloniaTheory]
    [InlineData(960)]
    [InlineData(1280)]
    public void DeclaredRangesKeepTheirProportionsAndReferenceAnchors(int width)
    {
        using var host = new OutputMapTestHost(width: width);
        Canvas regions = host.Map.FindControl<Canvas>("MapRegions")!;
        Canvas crc = host.Map.FindControl<Canvas>("MapCrc")!;
        Border bracket = Assert.Single(crc.Children.OfType<Border>());
        Border version = Assert.Single(host.Bar.Children.OfType<Border>(), b => b.Classes.Contains("outputMapVersion"));
        Assert.Equal(18, regions.Bounds.Height);
        Assert.InRange(host.Bar.Bounds.Height, 38, 40);
        AssertProportion(host.Region("dp-region"), regions, 0x3E000, 0x2000);
        AssertProportion(host.Block("tp"), host.Bar, 0, 0x3C000);
        AssertProportion(host.Block("dp"), host.Bar, 0x3E000, 0x2000);
        AssertProportion(bracket, crc, 0, 0x3C000);
        Assert.InRange(Math.Abs(version.Bounds.Center.X - (host.Bar.Bounds.Width * 0x3E016 / 0x40000)), 0, 1);
        Assert.InRange(Math.Abs(host.Bounds(regions).Bottom - host.Bounds(host.Bar).Top), 0, 2);
        Assert.True(host.Bounds(crc).Top > host.Bounds(host.Bar).Bottom);
        Assert.True(host.Bounds(host.Map.FindControl<WrapPanel>("MapJump")!).Top > host.Bounds(crc).Bottom);
        Assert.Equal("[0x00000, 0x40000)", host.Map.FindControl<TextBlock>("MapRange")!.Text);
        Assert.Equal("0x20000", host.Map.FindControl<TextBlock>("AxisMiddle")!.Text);
    }

    /// <summary>A wide hit target is separate from its exact visual width, even at output edges.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(499999)]
    [InlineData(999999)]
    public void TinyBlockKeepsMinimumHitWidthAtItsTrueCenter(long start)
    {
        var model = new OutputMapModel("logical", 1000000,
            blocks: [new("tiny", new ByteRange(start, 1), OutputMapBlockKind.Patch, "One byte")]);
        using var host = new OutputMapTestHost(model);
        Button target = host.Block("tiny");
        Border fill = Assert.Single(host.Bar.Children.OfType<Border>());
        Assert.InRange(target.Bounds.Width, 8, 9);
        Assert.InRange(Math.Abs(target.Bounds.Center.X - ((start + 0.5) / 1000000 * host.Bar.Bounds.Width)), 0, 1);
        Assert.InRange(fill.Bounds.Width, 0, 1);
        Assert.InRange(Math.Abs(fill.Bounds.Center.X - target.Bounds.Center.X), 0, 1);
    }

    /// <summary>A logical map needs no physical regions or optional annotations.</summary>
    [AvaloniaFact]
    public void AbsentFactsDoNotCreateRegionsMarkersBracketsOrSourceStrips()
    {
        var model = new OutputMapModel("logical", 1000,
            blocks: [new("fill", new ByteRange(200, 100), OutputMapBlockKind.Fill, "Fill")]);
        using var host = new OutputMapTestHost(model);
        host.Map.SelectedKey = "fill";
        host.Layout();
        Assert.False(host.Map.FindControl<Canvas>("MapRegions")!.IsVisible);
        Assert.False(host.Map.FindControl<WrapPanel>("MapJump")!.IsVisible);
        Assert.Empty(host.Map.FindControl<Canvas>("MapCrc")!.Children);
        Assert.DoesNotContain(host.Bar.Children, c => c.Classes.Contains("outputMapVersion") || c.Classes.Contains("outputMapHighlight"));
        Assert.DoesNotContain(host.Map.GetVisualDescendants(), c => c is Control control &&
            global::Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "OutputMap.SourceStrip");
        Assert.True(host.Bar.IsEffectivelyVisible);
        Assert.True(host.Find<Canvas>("OutputMap.ZoomBar").IsEffectivelyVisible);
    }

    /// <summary>Known storage is displayed without inventing a coverage bracket.</summary>
    [AvaloniaFact]
    public void StorageOnlyCrcDrawsAMarkerWithoutCoverage()
    {
        using var host = new OutputMapTestHost(new OutputMapModel("output", 1000,
            crcAnnotations: [new("CRC", StorageRange: new ByteRange(996, 4))]));
        Assert.Empty(host.Map.FindControl<Canvas>("MapCrc")!.Children);
        _ = Assert.Single(host.Bar.Children, c => c.Classes.Contains("outputMapCrcStorage"));
    }

    /// <summary>Interior focus occupies 90%; edge focus remains inside the output.</summary>
    [AvaloniaTheory]
    [InlineData(400, 100, 0.9)]
    [InlineData(500, 1, 0.9)]
    [InlineData(0, 100, 18d / 19)]
    [InlineData(900, 100, 18d / 19)]
    public void ZoomFillsAboutNinetyPercentAndClampsAtOutputEdges(long start, long length, double ratio)
    {
        using var host = new OutputMapTestHost(new OutputMapModel("logical", 1000,
            blocks: [new("focus", new ByteRange(start, length), OutputMapBlockKind.Patch, "Focus")]));
        host.Map.SelectedKey = "focus";
        host.Layout();
        Canvas zoom = host.Find<Canvas>("OutputMap.ZoomBar");
        Border fill = Assert.Single(zoom.Children.OfType<Border>());
        Assert.InRange(Math.Abs(fill.Bounds.Width - (zoom.Bounds.Width * ratio)), 0, 1);
        Assert.InRange(fill.Bounds.Left, -1, zoom.Bounds.Width);
        Assert.InRange(fill.Bounds.Right, 0, zoom.Bounds.Width + 1);
    }

    /// <summary>Supplied write order determines overlay order, without merging away steps.</summary>
    [AvaloniaFact]
    public void LaterBlocksAreAboveEarlierBlocks()
    {
        using var host = new OutputMapTestHost(OutputMapTestHost.Example(includeHighlight: true));
        Assert.True(host.Bar.Children.IndexOf(host.Block("patch")) > host.Bar.Children.IndexOf(host.Block("tp")));
        _ = Assert.Single(host.Bar.Children, c => c.Classes.Contains("outputMapHighlight"));
    }

    /// <summary>Disconnected issue ranges can drive a logical-output zoom without invented regions.</summary>
    [AvaloniaFact]
    public void IssueHighlightDrivesZoomWithoutASelectedBlock()
    {
        using var host = new OutputMapTestHost(new OutputMapModel("logical", 1000,
            highlight: new OutputMapHighlight([new ByteRange(400, 20), new ByteRange(480, 20)], OutputMapHighlightSeverity.Error)));
        Canvas zoom = host.Find<Canvas>("OutputMap.ZoomBar");
        Assert.Equal(2, zoom.Children.Count(c => c.Classes.Contains("outputMapHighlight")));
        Assert.Contains("Issue", global::Avalonia.Automation.AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
        Assert.Null(host.Map.SelectedKey);
        Assert.False(host.Map.FindControl<WrapPanel>("MapJump")!.IsVisible);
    }

    /// <summary>An empty output has no ranges or fabricated annotations.</summary>
    [AvaloniaFact]
    public void EmptyOutputKeepsAnEmptyBarAndZeroAxis()
    {
        using var host = new OutputMapTestHost(new OutputMapModel("logical", 0));
        Assert.Empty(host.Bar.Children);
        Assert.Equal("[0x00000, 0x00000)", host.Map.FindControl<TextBlock>("MapRange")!.Text);
        Assert.Equal("0x00000", host.Map.FindControl<TextBlock>("AxisEnd")!.Text);
    }

    private static void AssertProportion(Control child, Canvas lane, long start, long length)
    {
        Assert.InRange(Math.Abs(child.Bounds.Left - (start / (double)0x40000 * lane.Bounds.Width)), 0, 1);
        Assert.InRange(Math.Abs(child.Bounds.Width - (length / (double)0x40000 * lane.Bounds.Width)), 0, 1);
    }
}
