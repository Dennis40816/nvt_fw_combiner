using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>The production rail exposes the shared, display-only small-slice explorer.</summary>
public sealed class MemoryCoverageExplorerTests
{
    /// <summary>Both workflows mount the same explorer without replacing their supporting rows.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProductionMapsExposeSharedSliceExplorer(bool replace, bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("memory-explorer");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Width = 1440;
        window.Height = 900;
        window.RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        try
        {
            if (replace) { shell.ShowReplaceCommand.Execute(null); }
            else { shell.ShowMergeCommand.Execute(null); }
            shell.WorkflowSession.SelectedIc = "NT51929";
            if (darkChinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            Render();
            MemoryCoverageBar bar = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), control => control.IsEffectivelyVisible);
            bar.ReducedMotion = true;
            Border direct = bar.GetVisualDescendants().OfType<Border>().First(control => control.Classes.Contains("memoryCoverageBarSegment"));
            Assert.True(direct.Focus(NavigationMethod.Tab));
            Render();
            Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            Assert.Same(direct.DataContext, card.DataContext);
            Save("direct");
            Border? group = bar.GetVisualDescendants().OfType<Border>().FirstOrDefault(control => control.DataContext is MemoryCoverageBarItem { IsGroup: true } && control.Focusable);
            if (group is not null)
            {
                Assert.True(group.Focus(NavigationMethod.Tab));
                Render();
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                Save("group");
                Control leaf = window.GetVisualDescendants().OfType<Control>().First(control => control.Classes.Contains("memoryLocalSlice"));
                Assert.True(leaf.Focus(NavigationMethod.Tab));
                Render();
                Assert.Same(leaf.DataContext, Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard").DataContext);
                Save("leaf");
            }
        }
        finally { await CloseAndFlushAsync(window); }

        void Save(string state)
        {
            Capture(window, $"memory-hover-{replace}-{darkChinese}-{state}");
        }
    }

    /// <summary>The complete application exposes local slices from the existing NT51950 CtrlRAM fixture through public input loading.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionCtrlRamWindowShowsReferenceAlignedLocalCard(bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("memory-ctrlram-reference");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Width = 1440;
        window.Height = 900;
        window.RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        try
        {
            await XamlControlStyleContractTests.LoadNt51950GoldenCtrlRamInputsAsync(shell, TestContext.Current.CancellationToken);
            if (darkChinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            Render();
            MemoryCoverageBar bar = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), control => control.IsEffectivelyVisible);
            bar.ReducedMotion = true;
            Border group = bar.GetVisualDescendants().OfType<Border>().First(control => control.DataContext is MemoryCoverageBarItem { IsGroup: true } && control.Focusable);
            Assert.True(group.Focus(NavigationMethod.Tab));
            Render();
            Capture(window, $"memory-ctrlram-{darkChinese}-group");
            Control leaf = window.GetVisualDescendants().OfType<Control>().First(control => control.Classes.Contains("memoryLocalSlice"));
            Assert.True(leaf.Focus(NavigationMethod.Tab));
            Render();
            Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            Assert.Same(leaf.DataContext, card.DataContext);
            Capture(window, $"memory-ctrlram-{darkChinese}-leaf");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void Capture(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        // Capture stable Fluent disclosure geometry, not an intermediate chevron frame.
        for (int tick = 0; tick < 4; tick++) { Thread.Sleep(80); Render(); }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{name}.png"));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
