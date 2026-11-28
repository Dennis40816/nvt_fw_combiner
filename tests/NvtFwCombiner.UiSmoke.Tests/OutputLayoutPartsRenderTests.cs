// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Captures standalone CtrlRAM, bank and hint surfaces at 960 px with synthetic inputs.</summary>
public sealed class OutputLayoutPartsRenderTests
{
    /// <summary>Both themes and languages retain the same anchors, inline details and unclipped tiny targets.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StandalonePartsRenderAt960WithoutHorizontalOverflow(bool dark, bool chinese)
    {
        using var host = new OutputLayoutPartsTestHost(
            CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(chinese: chinese)), dark: dark, chinese: chinese);
        host.Banks.IsViewingBankB = true;
        host.Zoom.SelectedIndex = 1;
        host.Layout();
        Assert.Equal(34, host.Rail.Bounds.Height);
        Assert.Equal(912, host.Rail.Bounds.Width);
        Assert.True(host.Bounds(host.Banks).Bottom < host.Bounds(host.Rail).Top);
        Assert.True(host.Bounds(host.Zoom).Bottom < host.Bounds(host.Hint).Top);
        AssertSurfaceFits(host);
        Capture(host, $"ctrlparts-960-{(dark ? "dark" : "light")}-{(chinese ? "zh-TW" : "en")}-detail");
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(tiny: true, chinese: chinese));
        host.Zoom.SelectedIndex = 0;
        host.Layout();
        AssertSurfaceFits(host);
        Capture(host, $"ctrlparts-960-{(dark ? "dark" : "light")}-{(chinese ? "zh-TW" : "en")}-tiny");
    }

    private static void AssertSurfaceFits(OutputLayoutPartsTestHost host)
    {
        Assert.All(host.Window.GetVisualDescendants().OfType<Control>()
            .Where(static control => control.IsEffectivelyVisible &&
                control is TextBlock or Button or Canvas), control =>
        {
            Avalonia.Rect bounds = host.Bounds(control);
            Assert.InRange(bounds.Left, -1, host.Window.ClientSize.Width + 1);
            Assert.InRange(bounds.Right, -1, host.Window.ClientSize.Width + 1);
            Assert.InRange(bounds.Bottom, 0, host.Window.ClientSize.Height + 1);
        });
    }

    private static void Capture(OutputLayoutPartsTestHost host, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.WriteableBitmap? frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.SavePng(Path.Combine(directory, name + ".png"));
    }
}
