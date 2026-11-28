// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Full-surface synthetic captures of all approved states, languages and themes.</summary>
public sealed class RecalculationConfirmRenderTests
{
    /// <summary>Writes twelve state/theme/language captures and one seven-offset case when explicitly requested.</summary>
    [AvaloniaTheory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(0, true, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    [InlineData(3, false, false)]
    public void ApprovedStatesRenderAtMinimumWidth(int state, bool dark, bool chinese)
    {
        using RecalculationConfirmTestHost host = CreateHost(state, dark, chinese);
        Assert.True(host.Modal.IsEffectivelyVisible);
        Assert.InRange(host.Bounds(host.Find<global::Avalonia.Controls.Border>("DialogSurface")).Left, 179, 181);
        Assert.InRange(host.Bounds(host.Find<global::Avalonia.Controls.Grid>("ButtonRow")).Bottom, 0, host.Window.ClientSize.Height);
        Capture(host, state, dark, chinese);
    }

    private static RecalculationConfirmTestHost CreateHost(int state, bool dark, bool chinese)
    {
        return new(canRecalculate: state != 2, expanded: state is 1 or 3, dark: dark, chinese: chinese,
            height: state is 1 or 3 ? 760 : 640,
            offsets: state == 3 ? [0x23C, 0x24C, 0x26C, 0x27C, 0x28C, 0x29C, 0x2AC] : null,
            icName: state == 2 ? "NT51xxx" : "NT51927");
    }

    private static void Capture(RecalculationConfirmTestHost host, int state, bool dark, bool chinese)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        string stateName = state switch
        {
            0 => "1-collapsed",
            1 => "2-expanded",
            2 => "3-no-rule",
            _ => "4-seven-offsets",
        };
        string theme = dark ? "dark" : "light";
        string language = chinese ? "zh-TW" : "en";
        _ = Directory.CreateDirectory(directory);
        using global::Avalonia.Media.Imaging.WriteableBitmap? frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.SavePng(System.IO.Path.Combine(directory, $"gr-confirm-{stateName}-{language}-{theme}.png"));
    }
}
