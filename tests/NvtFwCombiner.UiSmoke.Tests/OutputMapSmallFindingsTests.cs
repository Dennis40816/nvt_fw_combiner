// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Synthetic regressions for annotation access, snapshot admission and aligned lanes.</summary>
public sealed class OutputMapSmallFindingsTests
{
    /// <summary>An annotation in an unwritten gap has a pointer-reachable tooltip in either output lane.</summary>
    [AvaloniaTheory]
    [InlineData("outputMapVersion", false, "Version · v1 · out [0x001F4, 0x001F7)")]
    [InlineData("outputMapCrcStorage", false, "CRC · storage · out [0x002BC, 0x002C0)")]
    [InlineData("outputMapVersion", true, "Version · v1 · out [0x001F4, 0x001F7)")]
    [InlineData("outputMapCrcStorage", true, "CRC · storage · out [0x002BC, 0x002C0)")]
    public void OrphanAnnotationTooltipIsReachableWithThePointer(string markerClass, bool zoom, string expectedTip)
    {
        using var host = new OutputMapTestHost(new OutputMapModel("out", 1000,
            regions: [new("region", "Region", new ByteRange(0, 1000))],
            blocks: [new("fill", new ByteRange(0, 200), OutputMapBlockKind.Fill, "Fill")],
            crcAnnotations: [new("CRC", StorageRange: new ByteRange(700, 4))],
            version: new("Version", new ByteRange(500, 3), "v1")), selectedKey: "region");
        Canvas lane = zoom ? host.Find<Canvas>("OutputMap.ZoomBar") : host.Bar;
        Border marker = Assert.Single(lane.Children.OfType<Border>(), b => b.Classes.Contains(markerClass));
        // A few pixels beside the 2 px marker, where a user's pointer is likely to rest.
        Point point = host.Bounds(marker).Center + new Vector(3, 0);
        Control target = Assert.IsType<Control>(host.Window.InputHitTest(point), exactMatch: false);
        Assert.Equal(expectedTip, ToolTip.GetTip(target));
        ToolTip.SetShowDelay(target, 0);

        host.Window.MouseMove(point, RawInputModifiers.None);
        host.Layout();

        Assert.True(target.IsPointerOver);
        Assert.True(ToolTip.GetIsOpen(target));
        Assert.Equal("region", host.Map.SelectedKey);
        Assert.Null(host.Map.HoveredKey);
    }

    /// <summary>Reattachment at the old size must measure replacement labels with their attached styles.</summary>
    [AvaloniaFact]
    public void ModelReplacedWhileDetachedRemeasuresLaneLabelsOnSameSizeReattach()
    {
        using var host = new OutputMapTestHost(new OutputMapModel("out", 1000,
            blocks: [new("fill", new ByteRange(900, 500), OutputMapBlockKind.Fill, "Fill")],
            crcAnnotations: [new("CRC", new ByteRange(500, 1))]));
        Border parent = Assert.IsType<Border>(host.Window.Content);
        double previousWidth = host.Bar.Bounds.Width;
        parent.Child = null;
        host.Layout();
        host.Map.Model = new OutputMapModel("out", 1000,
            blocks: [new("fill", new ByteRange(900, 500), OutputMapBlockKind.Fill, "Fill")],
            crcAnnotations: [new("A readable replacement CRC name", new ByteRange(500, 1))]);

        parent.Child = host.Map;
        host.Layout();

        Assert.Equal(previousWidth, host.Bar.Bounds.Width);
        AssertNaturalLabelWidth(Assert.Single(host.Map.FindControl<Canvas>("MapCrc")!.Children.OfType<TextBlock>()));
        AssertNaturalLabelWidth(Assert.Single(host.Bar.Children.OfType<TextBlock>()));
    }

    /// <summary>Selection keys share one namespace, while source keys have their own unique namespace.</summary>
    [AvaloniaFact]
    public void SnapshotRejectsDuplicateRegionBlockAndSourceKeys()
    {
        _ = Assert.Throws<ArgumentException>("regions", () => new OutputMapModel("out", 1000,
            regions: [new("same", "First", new ByteRange(0, 100)), new("same", "Second", new ByteRange(100, 100))]));
        _ = Assert.Throws<ArgumentException>("blocks", () => new OutputMapModel("out", 1000,
            blocks: [new("same", new ByteRange(0, 100), OutputMapBlockKind.Fill, "First"),
                new("same", new ByteRange(100, 100), OutputMapBlockKind.Patch, "Second")]));
        _ = Assert.Throws<ArgumentException>("blocks", () => new OutputMapModel("out", 1000,
            regions: [new("same", "Region", new ByteRange(0, 100))],
            blocks: [new("same", new ByteRange(0, 100), OutputMapBlockKind.Fill, "Block")]));
        _ = Assert.Throws<ArgumentException>("sources", () => new OutputMapModel("out", 1000,
            sources: [new("same", "first.bin", 100), new("same", "second.bin", 200)]));

        var model = new OutputMapModel("out", 1000,
            regions: [new("same", "Region", new ByteRange(0, 100))],
            blocks: [new("Same", new ByteRange(0, 100), OutputMapBlockKind.Copy, "Copy", "same", new ByteRange(0, 100))],
            sources: [new("same", "first.bin", 100), new("Same", "second.bin", 200)]);
        using var host = new OutputMapTestHost(model);
        Assert.True(host.Region("same").IsEffectivelyVisible);
        Assert.True(host.Block("Same").IsEffectivelyVisible);
        Assert.Equal(2, model.Sources.Count);
    }

    /// <summary>All lanes use the inner track edges in overview and zoom, independently of text metrics.</summary>
    [AvaloniaTheory]
    [InlineData(960)]
    [InlineData(1280)]
    public void RegionAndCrcEdgesAlignWithBlocksInsideTheTrackOutline(int width)
    {
        using var host = new OutputMapTestHost(new OutputMapModel("out", 1000,
            regions: [new("region", "Region", new ByteRange(0, 1000))],
            blocks: [new("fill", new ByteRange(0, 1000), OutputMapBlockKind.Fill, "Fill")],
            crcAnnotations: [new("CRC", new ByteRange(0, 1000))]), width: width, selectedKey: "fill");
        Border bracket = Assert.Single(host.Map.FindControl<Canvas>("MapCrc")!.Children.OfType<Border>());

        AssertAlignedEdges(host, host.Block("fill"), host.Region("region"));
        AssertAlignedEdges(host, host.Block("fill"), bracket);
        AssertAlignedEdges(host, host.Find<Button>("OutputMap.ZoomBlock.fill"), host.Find<Button>("OutputMap.ZoomRegion.region"));
        Assert.All(host.Map.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("outputMapTrack")),
            track => Assert.Equal(new Thickness(2), track.BorderThickness));
    }

    private static void AssertNaturalLabelWidth(TextBlock label)
    {
        var natural = new TextBlock { Text = label.Text, FontFamily = label.FontFamily, FontSize = label.FontSize };
        natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.InRange(Math.Abs(label.Bounds.Width - natural.DesiredSize.Width), 0, 1);
    }

    private static void AssertAlignedEdges(OutputMapTestHost host, Control expected, Control actual)
    {
        Assert.InRange(Math.Abs(host.Bounds(expected).Left - host.Bounds(actual).Left), 0, 1);
        Assert.InRange(Math.Abs(host.Bounds(expected).Right - host.Bounds(actual).Right), 0, 1);
    }
}
