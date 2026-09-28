using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>F-6, review of edd9a71b4 (docs/handoff/1.1.12.md owner decision 32): Avalonia's ToggleButton
/// always toggles its own IsChecked locally on a real click, regardless of the one-way binding to
/// IsHomeVisible/IsMergeVisible/IsReplaceVisible. Two interactions can leave that local toggle stuck out of
/// sync with the real SelectedPage even though the underline (bound directly, unaffected by ToggleButton's
/// own click handling) stays correct: re-clicking the already-selected tab, and clicking a different tab
/// while the navigation-clear confirmation opens and is then cancelled. Both are exercised here with a real
/// simulated click (Focus + Space), not by invoking a Command directly, since only a real click drives
/// ToggleButton's own local-toggle behavior.</summary>
public sealed class NavigationCheckedStateTests
{
    /// <summary>Re-clicking the current page's own nav tab must not desync its IsChecked (and hence its
    /// bold-text :checked style and assistive-technology checked state) from the unchanged selection, even
    /// though ApplySelectedPage takes no action when the target page does not actually change.</summary>
    [AvaloniaFact]
    public async Task ReclickingTheCurrentPagesTabKeepsItCheckedAndUnderlined()
    {
        using var workspace = TempWorkspace.Create("nav-checked-state-reclick");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Dispatcher.UIThread.RunJobs();
            var shell = (MainWindowViewModel)window.DataContext!;
            ToggleButton home = window.FindControl<ToggleButton>("HomeNavigationButton")!;

            Assert.True(shell.IsHomeVisible);
            Assert.True(home.IsChecked);

            Click(window, home);

            Assert.True(shell.IsHomeVisible);
            Assert.True(home.IsChecked, "Home's IsChecked drifted from the real selection after re-clicking its own tab.");
            Assert.True(UnderlineFor(home).IsVisible);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Clicking a different tab while a selected input blocks the switch opens the navigation-clear
    /// confirmation without changing SelectedPage; cancelling it must leave every tab's IsChecked matching
    /// the unchanged real selection, not the momentary local toggle from the click that opened the modal.</summary>
    [AvaloniaFact]
    public async Task CancellingAPageSwitchKeepsCheckedStateMatchingTheRealSelection()
    {
        using var workspace = TempWorkspace.Create("nav-checked-state-cancel");
        string path = workspace.Write("selected.bin", [0x12, 0x34]);
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Dispatcher.UIThread.RunJobs();
            var shell = (MainWindowViewModel)window.DataContext!;
            shell.ShowMergeCommand.Execute(null);
            shell.Merge.MergeDpSlot.FilePath = path;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            ToggleButton home = window.FindControl<ToggleButton>("HomeNavigationButton")!;
            ToggleButton merge = Assert.Single(
                window.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Classes.Contains("nav") && ReferenceEquals(button.Command, shell.ShowMergeCommand));

            Assert.True(shell.IsMergeVisible);
            Assert.True(merge.IsChecked);
            Assert.False(home.IsChecked);

            // A real click on Home while Merge has a selected input opens the navigation-clear
            // confirmation instead of switching pages.
            Click(window, home);

            Assert.True(shell.Navigation.IsNavigationClearConfirmationOpen);
            Assert.True(shell.IsMergeVisible);

            shell.Navigation.CancelNavigationClearCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.False(shell.Navigation.IsNavigationClearConfirmationOpen);
            Assert.True(shell.IsMergeVisible);
            Assert.True(merge.IsChecked);
            Assert.True(UnderlineFor(merge).IsVisible);
            Assert.False(
                home.IsChecked,
                "Home's IsChecked stayed stuck on 'checked' after the page-switch attempt to it was cancelled.");
            Assert.False(UnderlineFor(home).IsVisible);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>A real simulated click (focus, then Space down/up), matching this repo's existing
    /// convention for activating a ToggleButton in a headless test (see ShellScreenInventoryTests): only a
    /// real click drives Avalonia's own local IsChecked-toggle behavior, unlike invoking a bound Command
    /// directly.</summary>
    private static void Click(Window window, ToggleButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The dedicated sibling Border (see MainWindow.axaml) that draws a nav tab's selected
    /// underline (F-3, review of 91214ef81).</summary>
    private static Border UnderlineFor(ToggleButton button)
    {
        Avalonia.Visual parent = button.GetVisualParent()!;
        return parent.GetVisualChildren().OfType<Border>().Single(item => item.Classes.Contains("navSelectedUnderline"));
    }
}
