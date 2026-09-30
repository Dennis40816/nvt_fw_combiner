using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Records synchronous opening and render-pump costs; this is not native first-readable latency.</summary>
    [AvaloniaTheory]
    [InlineData(false, 420, false)]
    [InlineData(false, 420, true)]
    [InlineData(true, 420, false)]
    [InlineData(true, 420, true)]
    [InlineData(false, 620, false)]
    [InlineData(false, 620, true)]
    [InlineData(true, 620, false)]
    [InlineData(true, 620, true)]
    public async Task MeasureCardOpeningAfterPassiveFixes(bool plain, int width, bool reduced)
    {
        MemoryCoverageSegmentViewModel[] slices = TransitSlices();
        Window window = CreateWindow(width, false, slices, out MemoryCoverageBar bar);
        bar.IsPlain = plain;
        bar.ShowLegend = true;
        bar.ReducedMotion = reduced;
        Render();
        var samples = new List<(double Handler, double Render)>();
        try
        {
            for (int sample = 0; sample <= 30; sample++)
            {
                window.MouseMove(new Point(2, 2), RawInputModifiers.None);
                bar.IsEnabled = false;
                bar.IsEnabled = true;
                Render();
                Point point = BoundsInWindow(MainTarget(bar, 0), window).Center;
                long started = Stopwatch.GetTimestamp();
                window.MouseMove(point, RawInputModifiers.None);
                double handler = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Render();
                samples.Add((handler, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
                Assert.Same(slices[0], FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
                if (!reduced) { await Task.Delay(160, TestContext.Current.CancellationToken); Render(); }
            }
            string? directory = Environment.GetEnvironmentVariable("NFC_MEMORY_TIMING_OUTPUT_DIR");
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
                double[] warm = [.. samples.Skip(1).Select(static sample => sample.Handler).Order()];
                double[] renders = [.. samples.Skip(1).Select(static sample => sample.Render).Order()];
                string json = JsonSerializer.Serialize(new
                {
                    plain,
                    width,
                    reduced,
                    language = "English",
                    theme = "Light",
                    warmCount = 30,
                    coldHandlerMs = samples[0].Handler,
                    coldRenderPumpMs = samples[0].Render,
                    handlerP50Ms = warm[14],
                    handlerP95Ms = warm[28],
                    renderPumpP50Ms = renders[14],
                    renderPumpP95Ms = renders[28],
                    samples = samples.Select(static sample => new { handlerMs = sample.Handler, renderPumpMs = sample.Render }),
                });
                await File.WriteAllTextAsync(Path.Combine(directory, $"opening-{plain}-{width}-{reduced}.json"), json, TestContext.Current.CancellationToken);
            }
        }
        finally { window.Close(); }
    }

    /// <summary>A tall repeated-name legend stays passive throughout timed transit; reference identity is preserved.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TallPassiveLegendPreservesTimedTransit(bool dark)
    {
        var clock = new MemoryCoverageCloseScheduler();
        MemoryCoverageSegmentViewModel[] slices = [.. Enumerable.Range(0, 16).Select(index =>
            new MemoryCoverageSegmentViewModel($"range-{index}", "Same source", "Exact independent region", MemoryCoverageFillRole.Dp, 1,
                rangeStart: index, rangeEndExclusive: index + 1, addressSpaceId: "flash"))];
        Window window = CreateWindow(420, dark, slices, out MemoryCoverageBar bar, clock.Schedule);
        window.Height = 1300;
        bar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Border[] rows = [.. bar.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "MemoryLegendTarget")];
            window.MouseMove(BoundsInWindow(MainTarget(bar, 0), window).Center, RawInputModifiers.None);
            Render();
            _ = Assert.Single(rows, row => row.Classes.Contains("railActive"));
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            window.MouseMove(BoundsInWindow(rows[7], window).Center, RawInputModifiers.None);
            clock.AdvanceBy(TimeSpan.FromMilliseconds(160));
            window.MouseMove(BoundsInWindow(rows[15], window).Center, RawInputModifiers.None);
            clock.AdvanceBy(TimeSpan.FromMilliseconds(159));
            window.MouseMove(BoundsInWindow(card, window).Center, RawInputModifiers.None);
            clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
            Render();
            Assert.Same(slices[0], card.DataContext);
            // Crossing legend rows never lit them; only the card's own slice stays lit while the pointer is on its card.
            Assert.Same(rows[0], Assert.Single(rows, row => row.Classes.Contains("railActive")));
            window.MouseMove(BoundsInWindow(rows[4], window).Center, RawInputModifiers.None);
            clock.AdvanceBy(TimeSpan.FromMilliseconds(320));
            Render();
            AssertNoOverlay(window);
            window.MouseMove(BoundsInWindow(MainTarget(bar, 15), window).Center, RawInputModifiers.None);
            Render();
            Assert.Same(slices[15], FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
            Assert.Contains("railActive", rows[15].Classes);
            Assert.DoesNotContain("railActive", rows[0].Classes);
        }
        finally { window.Close(); }
    }

    /// <summary>Parent activity emphasizes its typed members without decorating all local leaves.</summary>
    [AvaloniaFact]
    public void GroupRailEmphasisDoesNotDecorateUnfocusedLocalLeaves()
    {
        Window window = CreateWindow(620, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Assert.True(MainTarget(bar, 1).Focus(NavigationMethod.Tab));
            Render();
            Border[] rows = [.. bar.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "MemoryLegendTarget")];
            Assert.Equal(8, rows.Count(row => row.Classes.Contains("railActive")));
            ProportionalStackPanel strip = LocalStrip(FindNamed<Border>(window, "MemoryLocalView")!);
            Assert.All(strip.Children.OfType<Border>(), leaf => Assert.Equal(default, leaf.BorderThickness));
            Assert.True(strip.Children[3].Focus(NavigationMethod.Tab));
            Render();
            _ = Assert.Single(rows, row => row.Classes.Contains("railActive"));
            Assert.Contains("railActive", rows[4].Classes);
            bar.ItemsSource = TransitSlices();
            Render();
            AssertNoOverlay(window);
            Assert.DoesNotContain(bar.GetVisualDescendants().OfType<Border>(), row => row.Classes.Contains("railActive"));
        }
        finally { window.Close(); }
    }
}
