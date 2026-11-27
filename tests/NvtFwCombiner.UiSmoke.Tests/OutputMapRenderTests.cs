// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Synthetic map-card captures for the approved v7 demo, English, Light/Dark, at 960/1280.</summary>
public sealed class OutputMapRenderTests
{
    /// <summary>Both themes retain geometry without horizontal overflow at half and full window widths.</summary>
    [AvaloniaTheory]
    [InlineData(960, false)]
    [InlineData(1280, false)]
    [InlineData(960, true)]
    [InlineData(1280, true)]
    public void DemoCardRendersAtBothWidthsAndThemes(int width, bool dark)
    {
        using var host = new OutputMapTestHost(OutputMapTestHost.Example(includeHighlight: true), width: width, dark: dark);
        AssertWithinWindow(host);
        Assert.Equal(4, host.Bar.Children.OfType<Button>().Count());
        Assert.Equal(3, host.Map.Model!.Sources.Count);
        _ = Assert.IsType<DrawingBrush>(Assert.Single(host.Region("customer").GetVisualDescendants().OfType<Border>(),
            b => b.Background is DrawingBrush).Background);
        Capture(host, $"output-map-{(dark ? "dark" : "light")}-{width}");
        host.Map.SelectedKey = "patch";
        host.Layout();
        AssertWithinWindow(host);
        Assert.Equal(2, Assert.Single(host.Map.GetVisualDescendants().OfType<Canvas>(),
            c => c.Classes.Contains("outputMapConnectors")).Children.OfType<Line>().Count());
        Capture(host, $"output-map-{(dark ? "dark" : "light")}-{width}-zoom");
    }

    /// <summary>Source reads use their own source length while output focus uses the output viewport.</summary>
    [AvaloniaFact]
    public void CopyZoomShowsTheDeclaredSourceRangeWithoutRebasingIt()
    {
        using var host = new OutputMapTestHost(new OutputMapModel("out", 1000,
            blocks: [new("copy", new ByteRange(400, 100), OutputMapBlockKind.Copy, "Copy", "src", new ByteRange(200, 100))],
            sources: [new("src", "input.bin", 1000)]));
        host.Map.SelectedKey = "copy";
        host.Layout();
        Canvas source = host.Find<Canvas>("OutputMap.SourceStrip");
        Border read = Assert.Single(source.Children.OfType<Border>());
        Assert.InRange(Math.Abs(read.Bounds.Left - (source.Bounds.Width * 0.2)), 0, 1);
        Assert.InRange(Math.Abs(read.Bounds.Width - (source.Bounds.Width * 0.1)), 0, 1);
        Canvas lines = Assert.Single(host.Map.GetVisualDescendants().OfType<Canvas>(), c => c.Classes.Contains("outputMapConnectors"));
        Line first = Assert.IsType<Line>(lines.Children[0]);
        Assert.InRange(Math.Abs(first.StartPoint.X - (lines.Bounds.Width * 0.2)), 0, 1);
        Assert.InRange(Math.Abs(first.EndPoint.X - (lines.Bounds.Width * 0.05)), 0, 1);
        Assert.InRange(Math.Abs(host.Bounds(lines).Top + first.StartPoint.Y - host.Bounds(source).Bottom), 0, 1);
        Assert.InRange(Math.Abs(first.EndPoint.Y - lines.Bounds.Height), 0, 1);
        TextBlock caption = Assert.Single(host.Map.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Classes.Contains("outputMapZoomCaption"));
        Assert.True(host.Bounds(lines).Bottom <= host.Bounds(caption).Top);
    }

    /// <summary>Lane labels built before the control is attached still get the caption style.</summary>
    [AvaloniaFact]
    public void LaneLabelsBuiltBeforeAttachmentKeepTheCaptionStyle()
    {
        using var host = new OutputMapTestHost();
        TextBlock region = Assert.Single(host.Region("tp-region").GetVisualDescendants().OfType<TextBlock>());
        TextBlock crc = Assert.Single(host.Map.FindControl<Canvas>("MapCrc")!.Children.OfType<TextBlock>());
        Assert.Equal(region.FontSize, crc.FontSize);
        Assert.Equal(region.Foreground, crc.Foreground);
    }

    /// <summary>Mutating the caller's list cannot mutate a published map snapshot.</summary>
    [Fact]
    public void SnapshotOwnsItsCollections()
    {
        var blocks = new List<OutputMapBlock> { new("a", new ByteRange(0, 1), OutputMapBlockKind.Fill, "A") };
        var ranges = new List<ByteRange> { new(0, 1) };
        var model = new OutputMapModel("out", 10, blocks: blocks,
            highlight: new OutputMapHighlight(ranges, OutputMapHighlightSeverity.Error));
        blocks.Clear();
        ranges.Clear();
        _ = Assert.Single(model.Blocks);
        _ = Assert.Single(model.Highlight!.Ranges);
    }

    private static void AssertWithinWindow(OutputMapTestHost host)
    {
        Assert.InRange(host.Bounds(host.Map).Right, 0, host.Window.ClientSize.Width);
        Assert.All(host.Map.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("outputMapTrack")),
            b => Assert.InRange(host.Bounds(b).Right, 0, host.Window.ClientSize.Width));
        global::Avalonia.Rect mapBounds = host.Bounds(host.Map);
        Assert.All(host.Map.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible), c =>
        {
            global::Avalonia.Rect bounds = host.Bounds(c);
            Assert.InRange(bounds.Left, mapBounds.Left - 1, mapBounds.Right + 1);
            Assert.InRange(bounds.Right, mapBounds.Left - 1, mapBounds.Right + 1);
        });
    }

    private static void Capture(OutputMapTestHost host, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        _ = Directory.CreateDirectory(directory);
        using global::Avalonia.Media.Imaging.WriteableBitmap? frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.SavePng(System.IO.Path.Combine(directory, name + ".png"));
    }
}
