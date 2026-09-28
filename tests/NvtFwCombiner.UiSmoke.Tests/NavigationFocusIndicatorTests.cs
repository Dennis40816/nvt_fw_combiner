using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>BUG-20260926-nav-focus-looks-selected, owner decision 32 (docs/handoff/1.1.12.md): only the
/// selected page's nav tab shows the blue underline; keyboard focus stays visible via a separate ring but
/// must never look like selection on the wrong tab, must never hide the underline on the right one
/// (F-1, review of 6dee964a0), and the two indicators must stay visibly separate rather than merging into
/// one continuous line (F-4, review of edd9a71b4).</summary>
public sealed class NavigationFocusIndicatorTests
{
    /// <summary>Matches Border.navSelectedUnderline's Height in MainWindowStyles.axaml.</summary>
    private const double UnderlineHeight = 2;

    /// <summary>Matches Border.navSelectedUnderline's bottom Margin in MainWindowStyles.axaml: the gap
    /// that keeps the underline clear of the focus ring drawn just outside the tab's own bottom edge.</summary>
    private const double UnderlineBottomGap = 3;


    /// <summary>Startup focuses Home; once Merge becomes selected, only Merge shows the selected underline
    /// and Home's focus ring is a separate, visibly distinct indicator, in both themes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HomeKeepsStartupFocusWhileMergeBecomesSelectedWithADistinctIndicator(bool dark)
    {
        using var workspace = TempWorkspace.Create("nav-focus-indicator");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Dispatcher.UIThread.RunJobs();
            var shell = (MainWindowViewModel)window.DataContext!;

            ToggleButton home = window.FindControl<ToggleButton>("HomeNavigationButton")!;
            Assert.NotNull(home);

            // Startup leaves real keyboard focus on Home before any other page opens.
            Assert.True(home.IsChecked);
            Assert.True(home.IsFocused);
            Assert.Contains(":focus-visible", home.Classes);
            Capture(window, "nav-focus-startup-home-selected", dark);

            // Owner decision 32's actual repro: startup opens another page directly while the shell
            // never moves real keyboard focus away from Home, so Home stays keyboard-focused after it
            // is no longer the selected page.
            shell.ShowMergeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            ToggleButton merge = Assert.Single(
                window.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Classes.Contains("nav") && ReferenceEquals(button.Command, shell.ShowMergeCommand));

            Assert.True(shell.IsMergeVisible);
            Assert.True(merge.IsChecked);
            Assert.False(home.IsChecked);
            // Home never lost real keyboard focus; this is exactly the reported startup sequencing.
            Assert.True(home.IsFocused);
            Assert.Contains(":focus-visible", home.Classes);
            Assert.False(merge.IsFocused);
            // Capture the exact reported state (Home focused, Merge selected) before the shape
            // assertions below, so the bug's before/after evidence exists whether or not they pass.
            Capture(window, "nav-focus-home-unselected-merge-selected", dark);

            ContentPresenter homePresenter = Assert.Single(
                home.GetVisualDescendants().OfType<ContentPresenter>(), item => item.Name == "PART_ContentPresenter");
            ContentPresenter mergePresenter = Assert.Single(
                merge.GetVisualDescendants().OfType<ContentPresenter>(), item => item.Name == "PART_ContentPresenter");

            Assert.True(window.TryFindResource("NfcAccentBrush", window.ActualThemeVariant, out object? selectedBrushResource));
            Assert.True(window.TryFindResource("NfcNavFocusRingShadow", window.ActualThemeVariant, out object? focusRingResource));
            Color selectedColor = Assert.IsType<ISolidColorBrush>(selectedBrushResource, exactMatch: false).Color;
            BoxShadows focusRing = Assert.IsType<BoxShadows>(focusRingResource);

            // The selected page keeps its separate underline Border in the selected accent color; since
            // it is not focused, its ContentPresenter shows no focus ring. Home, unselected, shows none.
            Border mergeUnderline = UnderlineFor(merge);
            Assert.True(mergeUnderline.IsVisible);
            Assert.Equal(
                selectedColor,
                Assert.IsType<ISolidColorBrush>(mergeUnderline.Background, exactMatch: false).Color);
            Assert.Equal(default, mergePresenter.BoxShadow);
            Assert.False(UnderlineFor(home).IsVisible);

            // The focused-but-unselected Home tab draws its focus ring as a separate BoxShadow layer
            // instead of reusing the underline's Border properties, so it stays visibly distinct in both
            // themes (in the dark theme NfcAccentBrush and NfcAccentBorderStrongBrush resolve to the
            // identical color) without ever suppressing a genuinely selected tab's underline
            // (owner decision 32; F-1, review of 6dee964a0).
            Assert.Equal(
                Colors.Transparent,
                Assert.IsType<ISolidColorBrush>(homePresenter.BorderBrush, exactMatch: false).Color);
            Assert.Equal(focusRing, homePresenter.BoxShadow);
            Assert.NotEqual(default, homePresenter.BoxShadow);

            // F-3 (review of 91214ef81): assert the rendered pixels, not just the BoxShadow property
            // value, which can be set correctly yet never reach the screen. Home's ring must be visible
            // outside its own top edge; Merge's underline must be visible in its own separate band; both
            // at once. F-5 (review of edd9a71b4): sample the ring only above the tab (never overlapping
            // the underline's band), and separately confirm Merge's own gap band between its underline
            // and its bottom edge stays clear, proving the two indicators do not merge into one line.
            Assert.True(
                window.TryFindResource("NfcAccentBorderStrongBrush", window.ActualThemeVariant, out object? focusColorResource));
            Color focusColor = Assert.IsType<ISolidColorBrush>(focusColorResource, exactMatch: false).Color;
            int homeRingPixels = CountColorPixels(window, RingHaloRegion(window, home, outset: 3), focusColor, tolerance: 24);
            int mergeUnderlinePixels = CountColorPixels(window, UnderlineRegion(window, merge), selectedColor, tolerance: 24);
            int mergeGapPixels = CountColorPixels(window, GapRegion(window, merge, outset: 3), selectedColor, tolerance: 24) +
                CountColorPixels(window, GapRegion(window, merge, outset: 3), focusColor, tolerance: 24);
            Assert.True(homeRingPixels > 20, $"Expected a rendered focus ring around Home; found {homeRingPixels} matching pixels.");
            Assert.True(
                mergeUnderlinePixels > 20,
                $"Expected a rendered selected underline on Merge; found {mergeUnderlinePixels} matching pixels.");
            Assert.True(
                mergeGapPixels == 0,
                $"Expected Merge's gap band between its underline and its own edge to stay clear; found {mergeGapPixels} matching pixels.");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Owner decision 32: startup keyboard focus lands on the selected page's own nav tab, not
    /// always Home, when startup inputs open Merge or Replace directly; the focus/selection shape
    /// distinction from the previous test still holds on whichever tab is focused.</summary>
    [AvaloniaTheory]
    [InlineData("home", false)]
    [InlineData("home", true)]
    [InlineData("merge", false)]
    [InlineData("merge", true)]
    [InlineData("replace", false)]
    [InlineData("replace", true)]
    public async Task StartupFocusLandsOnTheSelectedPagesOwnNavTab(string page, bool dark)
    {
        using var workspace = TempWorkspace.Create("nav-focus-startup-target");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        UiLaunchOptions options = UiLaunchOptions.Parse(["--page", page]);
        Assert.Empty(options.Issues);
        using var window = new MainWindow(options, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Dispatcher.UIThread.RunJobs();
            var shell = (MainWindowViewModel)window.DataContext!;

            ToggleButton home = window.FindControl<ToggleButton>("HomeNavigationButton")!;
            ToggleButton replace = Assert.Single(
                window.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Classes.Contains("nav") && ReferenceEquals(button.Command, shell.ShowReplaceCommand));
            ToggleButton merge = Assert.Single(
                window.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Classes.Contains("nav") && ReferenceEquals(button.Command, shell.ShowMergeCommand));
            (ToggleButton expected, ToggleButton[] others) = page switch
            {
                "merge" => (merge, new[] { home, replace }),
                "replace" => (replace, new[] { home, merge }),
                _ => (home, new[] { replace, merge }),
            };

            Assert.Equal(page == "merge", shell.IsMergeVisible);
            Assert.Equal(page == "replace", shell.IsReplaceVisible);
            Assert.Equal(page == "home", shell.IsHomeVisible);

            // Capture the actual startup state before the assertions below, so before/after evidence
            // exists whether or not they pass.
            Capture(window, $"nav-focus-startup-{page}-selected", dark);

            // The selected page's own tab, and only that tab, receives real startup keyboard focus.
            Assert.True(expected.IsChecked);
            Assert.True(expected.IsFocused);
            Assert.Contains(":focus-visible", expected.Classes);
            Assert.All(others, other => Assert.False(other.IsFocused));
            Assert.All(others, other => Assert.False(other.IsChecked));

            // F-1 (review of 6dee964a0): a tab that is both selected and keyboard-focused (the normal
            // startup case here) must keep its blue underline *and* show a focus ring at the same time;
            // the ring is a separate BoxShadow layer, so it never hides the underline it is drawn over.
            ContentPresenter expectedPresenter = Assert.Single(
                expected.GetVisualDescendants().OfType<ContentPresenter>(), item => item.Name == "PART_ContentPresenter");
            Assert.True(window.TryFindResource("NfcAccentBrush", window.ActualThemeVariant, out object? selectedBrushResource));
            Assert.True(window.TryFindResource("NfcNavFocusRingShadow", window.ActualThemeVariant, out object? focusRingResource));
            Color selectedColor = Assert.IsType<ISolidColorBrush>(selectedBrushResource, exactMatch: false).Color;
            BoxShadows focusRing = Assert.IsType<BoxShadows>(focusRingResource);
            Border expectedUnderline = UnderlineFor(expected);
            Assert.True(expectedUnderline.IsVisible);
            Assert.Equal(
                selectedColor,
                Assert.IsType<ISolidColorBrush>(expectedUnderline.Background, exactMatch: false).Color);
            Assert.Equal(focusRing, expectedPresenter.BoxShadow);
            Assert.NotEqual(default, expectedPresenter.BoxShadow);

            // Neither of the other two tabs shows a focus ring or the selected underline; only the one
            // real focus/selection target does.
            Assert.All(others, other =>
            {
                ContentPresenter otherPresenter = Assert.Single(
                    other.GetVisualDescendants().OfType<ContentPresenter>(), item => item.Name == "PART_ContentPresenter");
                Assert.Equal(default, otherPresenter.BoxShadow);
                Assert.False(UnderlineFor(other).IsVisible);
            });

            // F-3 (review of 91214ef81): a BoxShadow property value is not evidence the ring is actually
            // painted on screen — the first attempt at this fix set it correctly but an ancestor's default
            // ClipToBounds="True" (inherited from FluentTheme's ToggleButton/Button) clipped it away
            // entirely. F-4/F-5 (review of edd9a71b4): sampling the ring over the whole button (including
            // its interior and bottom edge) let a dark-theme underline — the identical color as the ring —
            // count as ring pixels even if the ring itself were missing, and the two indicators sat flush
            // against each other with no visible gap. RingHaloRegion samples only above the tab's own top
            // edge (never near the underline); UnderlineRegion samples exactly where the underline itself
            // is drawn (inset from the bottom edge by its own margin); GapRegion is the band between them,
            // which must stay clear in both themes for the two indicators to read as visibly separate.
            Assert.True(
                window.TryFindResource("NfcAccentBorderStrongBrush", window.ActualThemeVariant, out object? focusColorResource));
            Color focusColor = Assert.IsType<ISolidColorBrush>(focusColorResource, exactMatch: false).Color;
            int expectedRingPixels = CountColorPixels(window, RingHaloRegion(window, expected, outset: 3), focusColor, tolerance: 24);
            int expectedUnderlinePixels = CountColorPixels(window, UnderlineRegion(window, expected), selectedColor, tolerance: 24);
            int expectedGapPixels = CountColorPixels(window, GapRegion(window, expected, outset: 3), selectedColor, tolerance: 24) +
                CountColorPixels(window, GapRegion(window, expected, outset: 3), focusColor, tolerance: 24);
            Assert.True(
                expectedRingPixels > 20,
                $"Expected a rendered focus ring around the selected+focused '{page}' tab; found {expectedRingPixels} matching pixels.");
            Assert.True(
                expectedUnderlinePixels > 20,
                $"Expected a rendered selected underline on the '{page}' tab; found {expectedUnderlinePixels} matching pixels.");
            Assert.True(
                expectedGapPixels == 0,
                $"Expected a clear gap between the '{page}' tab's underline and its focus ring; found {expectedGapPixels} matching pixels " +
                "(they may be merged into one line).");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>F-7 (review of edd9a71b4, round 4): UnderlineRegion and GapRegion must meet at exactly
    /// one shared pixel boundary and never overlap, including at a non-integer RenderScaling, where the
    /// previous independent Floor/Ceiling rounding on each region could put the same row in both (e.g.
    /// bounds.Bottom = 50 logical, scale = 1.25 put pixel row 58 inside both). Avalonia's headless window
    /// supports setting RenderScaling directly, so this is verified against an actually rendered frame at
    /// 1.25x, not only unit arithmetic on the region formulas.</summary>
    [AvaloniaTheory]
    [InlineData(1.0, false)]
    [InlineData(1.0, true)]
    [InlineData(1.25, false)]
    [InlineData(1.25, true)]
    public async Task UnderlineAndGapRegionsNeverOverlapAtAnyRenderScaling(double scale, bool dark)
    {
        using var workspace = TempWorkspace.Create("nav-focus-scale");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Dispatcher.UIThread.RunJobs();
            window.SetRenderScaling(scale);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            ToggleButton home = window.FindControl<ToggleButton>("HomeNavigationButton")!;
            Assert.True(home.IsChecked);
            Assert.True(home.IsFocused);

            // The two regions share the exact same boundary row by construction, at any scale; neither
            // collapses to an empty band, which would make that equality trivially true.
            PixelRect underline = UnderlineRegion(window, home);
            PixelRect gap = GapRegion(window, home, outset: 3);
            Assert.True(underline.Height > 0, $"scale={scale}: UnderlineRegion collapsed to an empty band.");
            Assert.True(gap.Height > 0, $"scale={scale}: GapRegion collapsed to an empty band.");
            Assert.Equal(underline.Y + underline.Height, gap.Y);

            // Real rendered pixels confirm the shared boundary reflects what is actually painted, not
            // just the geometry: the ring and underline are each detected in their own band, and the
            // gap band between them stays clear, at this exact RenderScaling.
            Assert.True(window.TryFindResource("NfcAccentBrush", window.ActualThemeVariant, out object? selectedBrushResource));
            Assert.True(
                window.TryFindResource("NfcAccentBorderStrongBrush", window.ActualThemeVariant, out object? focusColorResource));
            Color selectedColor = Assert.IsType<ISolidColorBrush>(selectedBrushResource, exactMatch: false).Color;
            Color focusColor = Assert.IsType<ISolidColorBrush>(focusColorResource, exactMatch: false).Color;
            int ringPixels = CountColorPixels(window, RingHaloRegion(window, home, outset: 3), focusColor, tolerance: 24);
            int underlinePixels = CountColorPixels(window, underline, selectedColor, tolerance: 24);
            int gapPixels = CountColorPixels(window, gap, selectedColor, tolerance: 24) +
                CountColorPixels(window, gap, focusColor, tolerance: 24);
            Assert.True(ringPixels > 20, $"scale={scale}: expected a rendered focus ring around Home; found {ringPixels} matching pixels.");
            Assert.True(
                underlinePixels > 20,
                $"scale={scale}: expected a rendered selected underline on Home; found {underlinePixels} matching pixels.");
            Assert.True(gapPixels == 0, $"scale={scale}: expected Home's gap band to stay clear; found {gapPixels} matching pixels.");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>F-7: pure arithmetic reproduction of the reviewer's own counter-example (review of
    /// edd9a71b4, round 4) -- a control bottom edge at 50 logical px and RenderScaling 1.25 -- proving the
    /// previous formula (each region rounding its own start/height independently) put pixel row 58 inside
    /// both the underline band and the gap band, while the current PixelBoundary-based formula makes the
    /// two bands share exactly one boundary row. Needs no Avalonia rendering at all, so it holds
    /// independently of what any particular control's real bounds happen to be.</summary>
    [Fact]
    public void UnderlineAndGapBoundaryMathNeverOverlapsAtTheReviewersExampleScale()
    {
        const double bottom = 50;
        const double scale = 1.25;
        double underlineTop = bottom - UnderlineBottomGap - UnderlineHeight;
        double sharedBoundary = bottom - UnderlineBottomGap;

        int oldUnderlineEnd = (int)Math.Floor(underlineTop * scale) + (int)Math.Ceiling(UnderlineHeight * scale);
        int oldGapStart = (int)Math.Floor(sharedBoundary * scale);
        Assert.True(
            oldUnderlineEnd > oldGapStart,
            $"Expected the pre-F7 formula to reproduce the reported overlap at bottom={bottom}, scale={scale}, " +
            $"but underline end {oldUnderlineEnd} did not exceed gap start {oldGapStart}.");

        int newUnderlineEnd = PixelBoundary(sharedBoundary, scale);
        int newGapStart = PixelBoundary(sharedBoundary, scale);
        Assert.Equal(newUnderlineEnd, newGapStart);
    }

    private static void Capture(Window window, string name, bool dark)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        string directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR") ??
            Path.Combine(
                Assert.IsType<string>(Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")),
                "evidence",
                "nav-focus-underline");
        _ = Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, $"{name}-{(dark ? "dark" : "light")}.png"));
    }

    /// <summary>The dedicated sibling Border (see MainWindow.axaml) that draws a nav tab's selected
    /// underline, kept off the ToggleButton's own ContentPresenter so its :focus-visible BoxShadow ring
    /// never gets clipped (F-3, review of 91214ef81).</summary>
    private static Border UnderlineFor(ToggleButton button)
    {
        Visual parent = button.GetVisualParent()!;
        return parent.GetVisualChildren().OfType<Border>().Single(item => item.Classes.Contains("navSelectedUnderline"));
    }

    /// <summary>A control's bounds translated into the window's coordinate space.</summary>
    private static Rect BoundsRelativeToWindow(Visual control, Visual window)
    {
        Point origin = control.TranslatePoint(default, window)!.Value;
        return new Rect(origin, control.Bounds.Size);
    }

    /// <summary>The pixel band directly outside a control's own top edge: wide enough to catch the
    /// BoxShadow focus ring drawn there, but nowhere near the bottom edge where the selected underline
    /// lives, so this region can never count underline pixels as ring pixels (F-5, review of edd9a71b4;
    /// the previous HaloRegion covered the whole perimeter and interior, so a dark-theme underline —
    /// the identical color as the ring — could push a missing ring's pixel count past the threshold).
    /// All coordinates are derived from the control's own Bounds and the window's RenderScaling, not
    /// literal pixel positions, so the region is correct at any DPI scale factor.</summary>
    private static PixelRect RingHaloRegion(Window window, Control control, double outset)
    {
        Rect bounds = BoundsRelativeToWindow(control, window);
        double scale = window.RenderScaling;
        Rect outer = new(bounds.X - outset, bounds.Y - outset, bounds.Width + (2 * outset), outset);
        return new PixelRect(
            (int)Math.Floor(outer.X * scale),
            (int)Math.Floor(outer.Y * scale),
            (int)Math.Ceiling(outer.Width * scale),
            (int)Math.Ceiling(outer.Height * scale));
    }

    /// <summary>Converts one logical Y coordinate to the pixel row where a half-open region touching that
    /// boundary starts or ends, using the same rounding rule (Floor) every caller shares. UnderlineRegion
    /// and GapRegion meet at the identical logical boundary (bounds.Bottom - UnderlineBottomGap); computing
    /// it through this one method for both regions makes them agree on exactly the same pixel row, so they
    /// can never overlap or leave a gap between them, at any RenderScaling including a non-integer factor
    /// like 1.25x (F-7, review of edd9a71b4-round-4: UnderlineRegion previously derived its end from
    /// Floor(top) + Ceiling(height) while GapRegion derived its start from Floor(bottom) directly; those
    /// two computations do not generally agree once scale is non-integer, e.g. bounds.Bottom = 50, scale =
    /// 1.25 put row 58 inside both regions).</summary>
    private static int PixelBoundary(double logicalY, double scale)
    {
        return (int)Math.Floor(logicalY * scale);
    }

    /// <summary>The exact pixel band where Border.navSelectedUnderline itself is drawn: inset from the
    /// control's own bottom edge by its UnderlineBottomGap margin, so this region is entirely separate
    /// from RingHaloRegion and from GapRegion below. Its bottom edge is computed through PixelBoundary
    /// from the same logical value GapRegion uses for its top edge, so the two never overlap (F-7).</summary>
    private static PixelRect UnderlineRegion(Window window, Control control)
    {
        Rect bounds = BoundsRelativeToWindow(control, window);
        double scale = window.RenderScaling;
        double top = bounds.Bottom - UnderlineBottomGap - UnderlineHeight;
        double bottom = bounds.Bottom - UnderlineBottomGap;
        int y0 = PixelBoundary(top, scale);
        int y1 = PixelBoundary(bottom, scale);
        return new PixelRect(
            (int)Math.Ceiling(bounds.X * scale),
            y0,
            (int)Math.Floor(bounds.Width * scale),
            y1 - y0);
    }

    /// <summary>The pixel band between the underline and the control's own bottom edge (where the focus
    /// ring is drawn just outside): neither indicator should ever paint here. Used to prove the two stay
    /// visibly separate instead of merging into one line, in both themes (F-4/F-5, review of edd9a71b4).
    /// Inset horizontally by the same outset as RingHaloRegion: the ring is a full rectangular outline, so
    /// its left/right side bars run the control's entire height, including alongside this band, near the
    /// two outer corners; only the middle span between those two side bars is expected to stay clear. Its
    /// top edge is computed through PixelBoundary from the same logical value UnderlineRegion uses for its
    /// bottom edge, so the two regions share one boundary and can never overlap (F-7, review of
    /// edd9a71b4-round-4).</summary>
    private static PixelRect GapRegion(Window window, Control control, double outset)
    {
        Rect bounds = BoundsRelativeToWindow(control, window);
        double scale = window.RenderScaling;
        double top = bounds.Bottom - UnderlineBottomGap;
        double bottom = bounds.Bottom;
        int y0 = PixelBoundary(top, scale);
        int y1 = PixelBoundary(bottom, scale);
        return new PixelRect(
            (int)Math.Ceiling((bounds.X + outset) * scale),
            y0,
            (int)Math.Floor((bounds.Width - (2 * outset)) * scale),
            y1 - y0);
    }

    /// <summary>Counts rendered pixels in a region of the window's last rendered frame that match a
    /// target color within a per-channel tolerance; verifies the actual painted result, not a property
    /// value that could be clipped or otherwise never reach the screen.</summary>
    private static int CountColorPixels(Window window, PixelRect region, Color target, int tolerance)
    {
        using Avalonia.Media.Imaging.Bitmap frame = Assert.IsType<Avalonia.Media.Imaging.Bitmap>(
            window.GetLastRenderedFrame(), exactMatch: false);
        // GetLastRenderedFrame produces Rgba8888 (verified against a known-present underline pixel);
        // unlike a BGRA assumption, this must match the actual format or every comparison silently
        // reads the wrong channels.
        Assert.Equal(Avalonia.Platform.PixelFormat.Rgba8888, frame.Format);
        int stride = region.Width * 4;
        byte[] pixels = new byte[stride * region.Height];
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(region, handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        int count = 0;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            byte r = pixels[offset];
            byte g = pixels[offset + 1];
            byte b = pixels[offset + 2];
            if (Math.Abs(r - target.R) <= tolerance && Math.Abs(g - target.G) <= tolerance &&
                Math.Abs(b - target.B) <= tolerance)
            {
                count++;
            }
        }

        return count;
    }
}
