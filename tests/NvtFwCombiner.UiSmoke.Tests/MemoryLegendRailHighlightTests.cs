using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Every rail slice of a loaded image lights its own legend row while hovered.</summary>
public sealed class MemoryLegendRailHighlightTests
{
    /// <summary>Every raw AB block lights its coalesced row and numbered pin without opening a popup.</summary>
    [AvaloniaTheory]
    [InlineData("NT51929", "nt51929-ab-t05-d06")]
    [InlineData("NT51950", "nt51950-ab-boe-d82t80")]
    public async Task HoveringEveryAbRailSliceLightsItsNumberedRow(string ic, string caseId)
    {
        using var workspace = TempWorkspace.Create("legend-rail-highlight");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadAbAsync(ic, caseId);
        Canvas bar = host.Panel.FindControl<Canvas>("LayoutBar")!;
        Assert.NotEmpty(bar.Children);
        foreach (Control slice in bar.Children)
        {
            MemoryCoverageSegmentViewModel segment = Assert.IsType<MemoryCoverageSegmentViewModel>(slice.DataContext);
            Button row = host.RowFor(segment);
            Border highlight = Assert.Single(row.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("outputLayoutRow"));
            host.Window.MouseMove(host.Bounds(slice).Center, RawInputModifiers.None);
            host.Render();
            Assert.Contains("active", highlight.Classes);
            _ = Assert.Single(host.Panel.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("outputLayoutRow") && border.Classes.Contains("active"));
            _ = Assert.Single(host.Panel.FindControl<Canvas>("LayoutPins")!.Children,
                static pin => pin.Classes.Contains("active"));
            Assert.False(OutputLayoutPanelTestHost.Body(row).IsVisible);
            host.Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            host.Render();
            Assert.DoesNotContain("active", highlight.Classes);
        }
        Assert.DoesNotContain(host.Window.GetVisualDescendants(), static control => control.Name is "MemoryLocalView" or "MemorySliceCard");
    }

    /// <summary>A CtrlRAM lane has no legend row: its label and its local view light the section that contains it.</summary>
    [AvaloniaFact]
    public async Task HoveringACtrlRamLaneLightsItsContainingSectionRow()
    {
        using var workspace = TempWorkspace.Create("legend-lane-highlight");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1040, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717");
            string PathFor(string id)
            {
                return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
                    .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
            }
            var options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", "NT51950", "--ic-num", "single",
                "--base", PathFor("expected-output"), "--ctrlram", "replace-ctrlram-nf=" + PathFor("postbuild-nf-ctrlram")]);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, options.CtrlRam!, TestContext.Current.CancellationToken);
            await WaitForAsync(() => window.GetVisualDescendants().OfType<MemoryCoverageBar>().Any(static bar =>
                bar.IsEffectivelyVisible && bar.FocusPositions is not null &&
                bar.GetVisualDescendants().OfType<Border>().Any(static target => target.Name == "MemoryFocusPosition" && target.IsEffectivelyVisible)),
                "CtrlRAM focus rail to become visible");
            MemoryCoverageBar bar = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(),
                static bar => bar.IsEffectivelyVisible && bar.FocusPositions is not null);
            Border[] rows = [.. bar.GetVisualDescendants().OfType<Border>().Where(static row => row.Name == "MemoryLegendTarget")];
            Border tp = Assert.Single(rows, static row => ((MemoryCoverageSegmentViewModel)row.DataContext!).ContentRole == MemoryContentRole.Tp);
            Border master = Assert.Single(bar.GetVisualDescendants().OfType<Border>(), static target => target.Name == "MemoryFocusPosition");
            window.MouseMove(Center(master, window), RawInputModifiers.None);
            await WaitForAsync(() => tp.Classes.Contains("railActive") &&
                window.GetVisualDescendants().OfType<Border>().Any(static view => view.Name == "MemoryLocalView" && view.IsEffectivelyVisible),
                "CtrlRAM lane to light its containing TP section row and open its local view");
            Assert.Contains("railActive", tp.Classes);
            Assert.Equal([tp], rows.Where(static row => row.Classes.Contains("railActive")));
            Border local = Assert.Single(window.GetVisualDescendants().OfType<Border>(), static view => view.Name == "MemoryLocalView");
            window.MouseMove(new Point(Center(local, window).X, local.TranslatePoint(new Point(0, 8), window)!.Value.Y), RawInputModifiers.None);
            // Observe persistence after entering the local view; immediate presence is insufficient.
            await Task.Delay(300, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.Contains("railActive", tp.Classes);
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            await WaitForAsync(() => !rows.Any(static row => row.Classes.Contains("railActive")) &&
                !window.GetVisualDescendants().OfType<Border>().Any(static view => view.Name is "MemoryLocalView" or "MemorySliceCard"),
                "CtrlRAM overlay to close and section highlight to clear after pointer exit");
            Assert.DoesNotContain(rows, static row => row.Classes.Contains("railActive"));
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static Point Center(Control control, Window window)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    }

    private static async Task WaitForAsync(Func<bool> condition, string reason)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (condition()) { return; }
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), $"Timed out after 10 seconds waiting for {reason}.");
            await Task.Yield();
        }
    }
}
