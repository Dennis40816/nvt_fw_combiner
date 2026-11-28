// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Production-control captures follow artifacts/design/map-1-merge*, map-3-states and map-4-narrow.</summary>
public sealed class OutputLayoutPanelRenderTests
{
    /// <summary>Layer geometry and all six review captures use the actual standalone panel with canonical mock ranges.</summary>
    [AvaloniaTheory]
    [InlineData("light", 1280, false, false, false, false)]
    [InlineData("dark", 1280, true, false, false, false)]
    [InlineData("zh-TW", 1280, false, true, false, false)]
    [InlineData("960", 960, false, false, false, false)]
    [InlineData("expanded", 1280, false, false, true, false)]
    [InlineData("blocked", 1280, false, false, false, true)]
    public void RenderReviewStates(string name, int width, bool dark, bool chinese, bool expanded, bool blocked)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        OutputLayoutPanelModel example = OutputLayoutPanelTestHost.Example(language);
        OutputLayoutPanelModel model = blocked ? new([], example.CapacityLabel,
            notice: new(OutputLayoutNoticeKind.Blocked, "DP BIN needs attention",
                "The selected file changed. Select and inspect DP BIN again to resolve the input issue.")) : example;
        using var host = new OutputLayoutPanelTestHost(model, width, dark, language);
        PrepareCapture(host, expanded);
        Assert.InRange(host.Bounds(host.Panel).Right, width - 30, width - 26);
        Assert.True(host.Bounds(host.Panel.FindControl<TextBlock>("LayoutTitle")!).Bottom <=
            host.Bounds(host.Panel.FindControl<StackPanel>(blocked ? "LayoutNotice" : "LayoutMap")!).Top);
        Assert.All(host.Panel.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.IsEffectivelyVisible), text =>
        {
            Assert.InRange(host.Bounds(text).Left, 28, width - 28);
            Assert.InRange(host.Bounds(text).Right, 28, width - 28);
        });
        Capture(host, $"output-layout-panel-{name}");
    }

    /// <summary>Bar geometry uses declared addresses even when legacy display weights differ.</summary>
    [AvaloniaFact]
    public void BlocksAndPinsFollowPhysicalAddressesRatherThanEqualColumnsOrWeights()
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(1000, 1100, "A", weight: 1),
            OutputLayoutPanelTestHost.Slice(1100, 1300, "B", weight: 1),
            OutputLayoutPanelTestHost.Slice(1300, 2000, "C", weight: 1)]));
        Assert.InRange(Math.Abs(host.Bar.Children[0].Bounds.Width - (host.Bar.Bounds.Width * 0.1)), 0, 0.5);
        Assert.InRange(Math.Abs(host.Bar.Children[1].Bounds.Left - (host.Bar.Bounds.Width * 0.1)), 0, 0.5);
        Assert.InRange(Math.Abs(host.Bar.Children[1].Bounds.Width - (host.Bar.Bounds.Width * 0.2)), 0, 0.5);
        Assert.InRange(Math.Abs(host.Bar.Children[2].Bounds.Width - (host.Bar.Bounds.Width * 0.7)), 0, 0.5);
        Assert.InRange(Math.Abs(host.Pins.Children[1].Bounds.Center.X - (host.Pins.Bounds.Width * 0.2)), 0, 0.5);
    }

    /// <summary>Long titles wrap inside the row while badges and fixed, top-aligned chevrons retain their own columns.</summary>
    [AvaloniaFact]
    public void NarrowLongNamesCannotOverlapBadgesOrChevrons()
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 100, new string('W', 180), severity: MemoryDiagnosticSeverity.Warning)],
            warningCount: 1), width: 960);
        TextBlock badge = Assert.Single(host.Row("1").GetVisualDescendants().OfType<TextBlock>(),
            static text => text.Classes.Contains("outputLayoutBadge"));
        Avalonia.Controls.Shapes.Path chevron = Assert.Single(host.Row("1").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.Equal(new Avalonia.Size(16, 16), chevron.Bounds.Size);
        Assert.Equal(Avalonia.Layout.VerticalAlignment.Top, chevron.VerticalAlignment);
        Assert.True(host.Bounds(badge).Right <= host.Bounds(chevron).Left);
        Assert.All(host.Row("1").GetVisualDescendants().OfType<TextBlock>(),
            text => Assert.True(host.Bounds(text).Right <= host.Bounds(chevron).Left));
    }

    private static void PrepareCapture(OutputLayoutPanelTestHost host, bool expanded)
    {
        if (!expanded) { return; }
        host.Click(host.Row("1"));
        host.Click(host.Technical("1"));
        host.Click(host.Group(OutputLayoutGroupKind.NotWritten));
        host.Layout();
        Assert.True(OutputLayoutPanelTestHost.Body(host.Technical("1")).IsEffectivelyVisible);
    }

    private static void Capture(OutputLayoutPanelTestHost host, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.WriteableBitmap? frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.SavePng(Path.Combine(directory, name + ".png"));
    }
}
