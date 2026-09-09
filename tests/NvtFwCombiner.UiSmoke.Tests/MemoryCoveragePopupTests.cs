using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the actual grouped-memory overlay hierarchy and its terminal-slice cards.</summary>
public sealed class MemoryCoveragePopupTests
{
    /// <summary>Context-only details appear once; distinct explanations remain available.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExpandedContextCardDoesNotRepeatItsSummary(bool dark, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        foreach (bool distinct in new[] { false, true })
        {
            string summary = text.MemorySectionContextDetail;
            string detail = distinct ? text.MemoryDpImageContextDetail : summary;
            var segment = new MemoryCoverageSegmentViewModel("0x0–0xFFFF", "TP FW", detail,
                MemoryCoverageFillRole.Tp, 0x10000, text: text, rangeStart: 0, rangeEndExclusive: 0x10000,
                compactDetail: summary, addressSpaceId: "flash");
            Window window = CreateWindow(388, dark, [segment], out MemoryCoverageBar bar);
            window.DataContext = new ShellFixture(text);
            bar.Labels = text;
            bar.ReducedMotion = true;
            try
            {
                Render();
                Assert.True(MainTarget(bar, 0).Focus(NavigationMethod.Tab));
                Render();
                Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
                Expander disclosure = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
                Assert.False(disclosure.IsExpanded);
                disclosure.IsExpanded = true;
                Render();
                await Task.Delay(250, TestContext.Current.CancellationToken);
                Render();
                TextBlock[] visible = [.. card.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)];
                _ = Assert.Single(visible, block => block.Text == summary);
                _ = Assert.Single(visible, block => block.Text == "flash");
                if (distinct) { _ = Assert.Single(visible, block => block.Text == detail); }
                Assert.Equal(1, segment.AccessibleDetail.Split(summary, StringSplitOptions.None).Length - 1);
                Assert.Contains(detail, segment.AccessibleDetail, StringComparison.Ordinal);
                Capture(window, $"context-detail-{dark}-{chinese}-{distinct}");
                var template = (global::Avalonia.Controls.Templates.IDataTemplate)bar.FindResource("MemoryCoverageTooltipTemplate")!;
                window.Content = new ContentControl { Content = segment, ContentTemplate = template };
                Render();
                visible = [.. window.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)];
                _ = Assert.Single(visible, block => block.Text == summary);
                Assert.Equal(distinct, visible.Any(block => block.Text == text.DetailLabel));
                if (distinct) { _ = Assert.Single(visible, block => block.Text == detail); }
            }
            finally { window.Close(); }
        }
    }

    /// <summary>Rail and local-slice labels retain their glyph proportions throughout the decorative lift.</summary>
    [AvaloniaTheory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task LiftKeepsLabelScaleUnchangedDuringAndAfterAnimation(bool dark, int level)
    {
        Window window = CreateWindow(388, dark, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ShowLabels = true;
        Render();
        try
        {
            Control target;
            if (level == 1)
            {
                Assert.True(MainTarget(bar, 1).Focus(NavigationMethod.Tab));
                Render();
                Border view = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
                target = FocusableControl(LocalStrip(view).Children[5]);
            }
            else { target = MainTarget(bar, level == 2 ? 1 : 0); }
            TextBlock label = Assert.Single(target.GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible);
            Rect originalBounds = target.Bounds;
            double fontSize = label.FontSize;
            string? text = label.Text;
            Assert.True(label.Bounds.Width > 0 && label.Bounds.Height > 0);
            Assert.True(target.Focus(NavigationMethod.Tab));
            Render();
            for (int tick = 0; tick < 8; tick++)
            {
                await Task.Delay(30, TestContext.Current.CancellationToken);
                Render();
                AssertUnscaledLabel();
            }
            Assert.InRange(target.RenderTransform!.Value.M22, 1.179, 1.181);
            Assert.Equal(originalBounds, target.Bounds);
            Capture(window, $"label-lift-{dark}-{level}");
            bar.ReducedMotion = true;
            Render();
            Assert.Equal(Matrix.Identity, target.RenderTransform?.Value ?? Matrix.Identity);
            AssertUnscaledLabel();

            void AssertUnscaledLabel()
            {
                Matrix transform = Assert.IsType<Matrix>(label.TransformToVisual(window));
                Assert.InRange(transform.M11, 0.999, 1.001);
                Assert.InRange(transform.M22, 0.999, 1.001);
                Assert.Equal(fontSize, label.FontSize);
                Assert.Equal(text, label.Text);
            }
        }
        finally { window.Close(); }
    }

    /// <summary>Expanded surfaces have their own boundary; the connector does not paint over the underlying panel.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedSurfacesStayDistinctFromTheMainPanel(bool dark)
    {
        Window window = CreateWindow(388, dark, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        try
        {
            OpenGroupedCard(window, bar);
            Assert.True(bar.TryFindResource("NfcMemoryInteractionSurfaceBrush", bar.ActualThemeVariant, out object? surfaceBrush));
            Assert.True(bar.TryFindResource("NfcAccentBorderBrush", bar.ActualThemeVariant, out object? borderBrush));
            Assert.True(bar.TryFindResource("NfcMemoryPanelSurfaceBrush", bar.ActualThemeVariant, out object? panelBrush));
            Assert.NotEqual(panelBrush, surfaceBrush);
            foreach (string name in new[] { "MemoryLocalView", "MemorySliceCard" })
            {
                Border surface = Assert.IsType<Border>(FindNamed<Border>(window, name));
                Assert.Equal(new Thickness(1), surface.BorderThickness);
                Assert.Equal(surfaceBrush, surface.Background);
                Assert.Equal(borderBrush, surface.BorderBrush);
                Assert.NotEqual(default, surface.BoxShadow);
                StackPanel frame = Assert.IsType<StackPanel>(surface.GetVisualParent());
                Assert.Equal(global::Avalonia.Media.Brushes.Transparent, frame.Background);
            }
            global::Avalonia.Controls.Shapes.Polygon notch = Assert.Single(window.GetVisualDescendants()
                .OfType<global::Avalonia.Controls.Shapes.Polygon>(), item => item.Name == "MemoryCardNotch");
            Assert.Equal(surfaceBrush, notch.Fill);
        }
        finally { window.Close(); }
    }

    /// <summary>Grouped focus exposes all local slices without a preselected card, and leaf Escape unwinds one overlay at a time.</summary>
    [AvaloniaTheory]
    [InlineData(240, false)]
    [InlineData(388, true)]
    public void GroupedFocusKeepsLocalStripStableAndEscapeUnwindsToTheMainGroup(int width, bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateWindow(width, dark, slices, out MemoryCoverageBar bar);
        try
        {
            Control group = MainTarget(bar, 1);

            Assert.True(group.Focus());
            Render();

            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            ProportionalStackPanel strip = LocalStrip(local);
            Assert.Equal(8, strip.Children.Count);
            Border[] localSlices = [.. strip.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("memoryLocalSlice"))];
            Assert.Equal(8, localSlices.Length);
            Assert.All(localSlices,
                slice => Assert.Equal(new Thickness(0, 0, 1, 0), slice.BorderThickness));
            Rect stripBounds = strip.Bounds;

            MemoryCoverageSegmentViewModel selected = slices[6];
            Control localLeaf = FocusableControl(strip.Children[5]);
            Assert.Same(selected, localLeaf.DataContext);
            Assert.True(localLeaf.Focus());
            Render();

            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(selected, card.DataContext);
            Assert.Equal(stripBounds, strip.Bounds);

            Expander technicalDetails = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
            ToggleButton technicalToggle = Assert.Single(
                technicalDetails.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Name == "ExpanderHeader");
            Assert.True(technicalToggle.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Render();
            Assert.True(technicalDetails.IsExpanded);
            Assert.Equal(stripBounds, strip.Bounds);

            PressEscape(window);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.NotNull(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Same(localLeaf, window.FocusManager?.GetFocusedElement());

            PressEscape(window);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Same(group, window.FocusManager?.GetFocusedElement());

            Assert.True(MainTarget(bar, 0).Focus());
            Render();

            Border mainSlice = Assert.IsType<Border>(MainTarget(bar, 0));
            Assert.Equal(new Thickness(2), mainSlice.BorderThickness);
            Border directCard = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(slices[0], directCard.DataContext);
            Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Collection rebuild, disable and window resize immediately dismiss both tiers instead of leaving stale overlay state.</summary>
    [AvaloniaFact]
    public void CollectionResetDisableAndResizeClearOpenOverlays()
    {
        var slices = new ObservableCollection<MemoryCoverageSegmentViewModel>(MemoryCoverageBarProjectionTests.Example());
        Window window = CreateWindow(388, dark: false, slices, out MemoryCoverageBar bar);
        try
        {
            OpenGroupedCard(window, bar);
            slices.Clear();
            Render();
            AssertNoOverlay(window);

            foreach (MemoryCoverageSegmentViewModel slice in MemoryCoverageBarProjectionTests.Example())
            {
                slices.Add(slice);
            }
            Render();
            OpenGroupedCard(window, bar);
            bar.IsEnabled = false;
            Render();
            AssertNoOverlay(window);

            bar.IsEnabled = true;
            Render();
            OpenGroupedCard(window, bar);
            window.Width += 8;
            Render();
            AssertNoOverlay(window);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Normal cards use the approved reveal transition while reduced motion is immediately opaque and transition-free.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CardRevealHonorsReducedMotion(bool reducedMotion)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateWindow(388, dark: reducedMotion, slices, out MemoryCoverageBar bar);
        try
        {
            bar.ReducedMotion = reducedMotion;
            Assert.True(MainTarget(bar, 0).Focus());
            Render();

            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            if (reducedMotion)
            {
                Assert.Null(card.Transitions);
                Assert.Equal(1, card.Opacity);
            }
            else
            {
                Transitions transitions = Assert.IsType<Transitions>(card.Transitions);
                Assert.Equal(2, transitions.Count);
                Assert.Equal(
                    TimeSpan.FromMilliseconds(140),
                    Assert.Single(transitions.OfType<DoubleTransition>()).Duration);
                Assert.Equal(
                    TimeSpan.FromMilliseconds(140),
                    Assert.Single(transitions.OfType<TransformOperationsTransition>()).Duration);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A brief pointer excursion between tiers keeps the existing overlay alive without pinning it after exit.</summary>
    [AvaloniaTheory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task PointerTransitGraceKeepsExistingOverlayUntilArrival(int depth, bool reducedMotion)
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ReducedMotion = reducedMotion;
        try
        {
            Control target = MainTarget(bar, depth == 0 ? 0 : 1);
            window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
            Render();
            if (depth == 2)
            {
                Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
                target = FocusableControl(LocalStrip(local).Children[5]);
                window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
                Render();
            }
            string name = depth == 1 ? "MemoryLocalView" : "MemorySliceCard";
            Border destination = Assert.IsType<Border>(FindNamed<Border>(window, name));
            Point arrival = depth == 1
                ? BoundsInWindow(destination, window).TopLeft + new Vector(10, 10)
                : BoundsInWindow(destination, window).Center;
            // No click or keyboard focus may keep the overlay alive during this excursion.
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(220, TestContext.Current.CancellationToken);
            Render();
            Assert.False(target.IsPointerOver);
            Assert.False(target.IsKeyboardFocusWithin);
            Assert.Same(destination, FindNamed<Border>(window, name));

            window.MouseMove(arrival, RawInputModifiers.None);
            await Task.Delay(400, TestContext.Current.CancellationToken);
            Render();
            Assert.Same(destination, FindNamed<Border>(window, name));

            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(400, TestContext.Current.CancellationToken);
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Local cards stay close to their view without covering its address labels or header.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LocalCardKeepsACompactGapOutsideTheLocalView(bool above, bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = above ? CreateBottomWindow(dark, slices, out MemoryCoverageBar bar) : CreateWindow(388, dark, slices, out bar);
        bar.ReducedMotion = true;
        try
        {
            OpenGroupedCard(window, bar);
            Rect local = BoundsInWindow(Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView")), window);
            Rect card = BoundsInWindow(Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard")), window);
            double gap = above ? local.Top - card.Bottom : card.Top - local.Bottom;
            Assert.InRange(gap, 3.5, 5.5);
            Assert.False(card.Intersects(local));
            Assert.InRange(card.Top, 0, window.Bounds.Height - card.Height);
            Capture(window, $"compact-gap-{above}-{dark}");
        }
        finally { window.Close(); }
    }

    /// <summary>Moving from a main slice into its card keeps it open, while leaving both surfaces dismisses it after the bounded delay.</summary>
    [AvaloniaFact]
    public async Task PointerTraversalBetweenMainSliceAndCardKeepsThenDismissesTheCard()
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateWindow(388, dark: false, slices, out MemoryCoverageBar bar);
        try
        {
            Control mainLeaf = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(mainLeaf, window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));

            window.MouseMove(BoundsInWindow(card, window).Center, RawInputModifiers.None);
            Render();
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));

            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Leaving the complete hover surface dismisses both tiers even after a pointer click.</summary>
    [AvaloniaTheory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task PointerExitDismissesEveryOverlayAfterHoverOrClick(int depth, bool click)
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        try
        {
            Control target = MainTarget(bar, depth == 0 ? 0 : 1);
            window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
            Render();
            if (depth >= 2)
            {
                Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
                target = FocusableControl(LocalStrip(local).Children[5]);
                window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
                Render();
            }
            if (depth == 3)
            {
                Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
                target = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(), control => control.Name == "ExpanderHeader");
                window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
                Render();
            }
            if (click)
            {
                Point point = BoundsInWindow(target, window).Center;
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                Render();
            }
            Assert.NotNull(FindNamed<Border>(window, depth == 1 ? "MemoryLocalView" : "MemorySliceCard"));
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Keyboard focus stays readable until a real pointer traversal takes over dismissal.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task KeyboardOverlayRemainsUntilPointerInteractionTakesOver(int depth)
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        try
        {
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            Control target = MainTarget(bar, depth == 0 ? 0 : 1);
            Assert.True(target.Focus(NavigationMethod.Tab));
            Render();
            if (depth >= 2)
            {
                Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
                target = FocusableControl(LocalStrip(local).Children[5]);
                Assert.True(target.Focus(NavigationMethod.Directional));
                Render();
            }
            if (depth == 3)
            {
                Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
                target = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(), control => control.Name == "ExpanderHeader");
                Assert.True(target.Focus(NavigationMethod.Tab));
                Render();
            }
            window.MouseMove(new Point(6, 6), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            Assert.NotNull(FindNamed<Border>(window, depth == 1 ? "MemoryLocalView" : "MemorySliceCard"));
            window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
            Render();
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Using the keyboard on an already pointer-focused disclosure restores keyboard keep-open.</summary>
    [AvaloniaFact]
    public async Task KeyboardUseAfterPointerClickKeepsFocusedDisclosureReadable()
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        try
        {
            Control main = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(main, window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            ToggleButton toggle = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(), control => control.Name == "ExpanderHeader");
            Point point = BoundsInWindow(toggle, window).Center;
            window.MouseMove(point, RawInputModifiers.None);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Render();
            Assert.Same(toggle, window.FocusManager?.GetFocusedElement());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Render();
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            PressEscape(window);
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Public scroll and detachment lifecycle events close both overlay tiers and discard their stale targets.</summary>
    [AvaloniaFact]
    public void AncestorScrollAndDetachClearOpenOverlays()
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateScrolledWindow(slices, out MemoryCoverageBar bar, out ScrollViewer scroll);
        try
        {
            OpenGroupedCard(window, bar);
            scroll.Offset = new Vector(0, 1);
            Render();
            AssertNoOverlay(window);

            scroll.Offset = default;
            Render();
            window.Focusable = true;
            Assert.True(window.Focus());
            OpenGroupedCard(window, bar);
            scroll.Content = null;
            Render();
            AssertNoOverlay(window);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The 388px reference state can emit a full two-tier upward-overlay capture in both supported language/theme combinations.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BottomAnchoredGroupedOverlayCanBeCaptured(bool darkChinese)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateBottomWindow(darkChinese, slices, out MemoryCoverageBar bar);
        try
        {
            OpenGroupedCard(window, bar, localSliceIndex: 5);
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(slices[6], card.DataContext);
            Assert.True(BoundsInWindow(local, window).Bottom < BoundsInWindow(bar, window).Top);
            Assert.InRange(card.Bounds.Width / bar.Bounds.Width, 0.65, 0.72);
            for (int tick = 0; tick < 4; tick++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(80), TestContext.Current.CancellationToken);
                Render();
            }
            Capture(window, darkChinese ? "388-dark-zh-bottom" : "388-light-en-bottom");
            Assert.Equal(new Thickness(1), local.BorderThickness);
            TextBlock heading = Assert.Single(local.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == ((ShellTextResources)bar.Labels!).MemoryLocalViewLabel);
            Assert.True(BoundsInWindow(heading, window).Bottom < BoundsInWindow(LocalStrip(local), window).Top);
            Assert.True(BoundsInWindow(card, window).Bottom < BoundsInWindow(heading, window).Top);
            Assert.Contains(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "MemoryCardNotch");
            Assert.Contains(card.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemoryHoverTechnicalSeparator" && control.IsEffectivelyVisible);
            Assert.True(BoundsInWindow(card, window).Bottom < BoundsInWindow(LocalStrip(local), window).Top,
                $"Card {BoundsInWindow(card, window)}, strip {BoundsInWindow(LocalStrip(local), window)}, local {BoundsInWindow(local, window)}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The contrasting stem connects the selected leaf directly to the notch in both directions and themes.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CardAnchorConnectsTheLeafDirectlyToTheNotch(bool above, bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = above ? CreateBottomWindow(dark, slices, out MemoryCoverageBar bar) : CreateWindow(388, dark, slices, out bar);
        try
        {
            OpenGroupedCard(window, bar, 5);
            Assert.True(bar.TryFindResource("NfcTextStrongBrush", bar.ActualThemeVariant, out object? expectedStroke));
            global::Avalonia.Controls.Shapes.Ellipse dot = Assert.Single(window.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Ellipse>(),
                item => item.Name == "MemoryCardAnchor");
            global::Avalonia.Controls.Shapes.Polygon notch = Assert.Single(window.GetVisualDescendants()
                .OfType<global::Avalonia.Controls.Shapes.Polygon>(), item => item.Name == "MemoryCardNotch");
            global::Avalonia.Controls.Shapes.Line stem = Assert.Single(dot.GetVisualParent()!
                .GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Line>());
            Point anchor = BoundsInWindow(dot, window).Center;
            Point tip = notch.TranslatePoint(notch.Points[1], window)!.Value;
            Point start = stem.TranslatePoint(stem.StartPoint, window)!.Value;
            Point end = stem.TranslatePoint(stem.EndPoint, window)!.Value;
            Assert.Equal(expectedStroke, stem.Stroke);
            Assert.InRange(Math.Abs(start.X - anchor.X), 0, 0.5);
            Assert.InRange(Math.Abs(end.X - tip.X), 0, 0.5);
            Assert.InRange(Math.Abs(start.Y - Math.Min(anchor.Y, tip.Y)), 0, 0.5);
            Assert.InRange(Math.Abs(end.Y - Math.Max(anchor.Y, tip.Y)), 0, 0.5);
        }
        finally { window.Close(); }
    }

    /// <summary>Edge selections retain exact anchors and never draw their stem through local text.</summary>
    [AvaloniaTheory]
    [InlineData(240, false)]
    [InlineData(240, true)]
    [InlineData(388, false)]
    [InlineData(388, true)]
    public void EdgeCardsKeepMetadataAndAnchorsClear(int width, bool above)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = above ? CreateBottomWindow(false, slices, out MemoryCoverageBar bar) : CreateWindow(width, false, slices, out bar);
        window.Width = width + 32;
        Render();
        bar.ReducedMotion = true;
        try
        {
            foreach (int index in new[] { 0, 5, 7 })
            {
                OpenGroupedCard(window, bar, index);
                Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
                Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
                ProportionalStackPanel strip = LocalStrip(local);
                Rect leaf = BoundsInWindow(strip.Children[index], window);
                Rect body = BoundsInWindow(card, window);
                Rect rail = BoundsInWindow(bar, window);
                Assert.True(body.Left >= rail.Left - 1 && body.Right <= rail.Right + 1,
                    $"Card {body} leaves the memory rail column {rail}");
                global::Avalonia.Controls.Shapes.Ellipse dot = Assert.Single(window.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Ellipse>(), item => item.Name == "MemoryCardAnchor");
                Assert.InRange(Math.Abs(BoundsInWindow(dot, window).Center.X - leaf.Center.X), 0, 1);
                Assert.InRange(Math.Abs(BoundsInWindow(dot, window).Center.Y - (above ? leaf.Top : leaf.Bottom)), 0, 1);
                Assert.InRange(body.Left, 7, window.Bounds.Width);
                Assert.True(body.Right <= window.Bounds.Width - 7);
                Assert.True(body.Top >= 0 && body.Bottom <= window.Bounds.Height);
                foreach (TextBlock label in local.GetVisualDescendants().OfType<TextBlock>())
                {
                    Rect text = BoundsInWindow(label, window);
                    Assert.False(body.Intersects(text), $"Card {body} overlaps {label.Text} at {text}; above={above}");
                    foreach (global::Avalonia.Controls.Shapes.Line line in dot.GetVisualParent()!.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Line>())
                    {
                        Point lineStart = line.TranslatePoint(line.StartPoint, window)!.Value;
                        Point lineEnd = line.TranslatePoint(line.EndPoint, window)!.Value;
                        Assert.False(text.Contains(lineStart) || text.Contains(lineEnd));
                        Assert.False(lineStart.X >= text.Left && lineStart.X <= text.Right && lineStart.Y < text.Top && lineEnd.Y > text.Bottom);
                    }
                }
            }
        }
        finally { window.Close(); }
    }

    /// <summary>The local strip uses the larger available side instead of a fixed absolute Y threshold.</summary>
    [AvaloniaFact]
    public void LocalStripChoosesAvailableSpaceInAShorterViewport()
    {
        Window window = CreateBottomWindow(false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        window.Height = 620;
        Border spacer = Assert.IsType<Border>(Assert.IsType<StackPanel>(window.Content).Children[0]);
        spacer.Height = 330;
        Render();
        try
        {
            OpenGroupedCard(window, bar, 5);
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.True(BoundsInWindow(local, window).Bottom < BoundsInWindow(bar, window).Top);
            Assert.True(BoundsInWindow(card, window).Top >= 0);
        }
        finally { window.Close(); }
    }

    private static void OpenGroupedCard(Window window, MemoryCoverageBar bar, int localSliceIndex = 0)
    {
        Control group = MainTarget(bar, 1);
        Assert.True(group.Focus());
        Render();
        Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
        Control leaf = FocusableControl(LocalStrip(local).Children[localSliceIndex]);
        Assert.True(leaf.Focus());
        Render();
        Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
    }

    /// <summary>The shared flat row consumes the same primary flag as logical Merge/Replace rows.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlatSupportingRowHidesOnlyTechnicalTrace(bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example(withTrace: true);
        Window window = CreateWindow(388, dark, slices, out MemoryCoverageBar bar);
        try
        {
            var template = (global::Avalonia.Controls.Templates.IDataTemplate)bar.FindResource("MemoryCoverageSegmentListTemplate")!;
            Control row = template.Build(slices[2])!;
            row.DataContext = slices[2];
            window.Content = row;
            Render();
            Assert.False(row.IsVisible);
            row.DataContext = slices[0];
            Render();
            Assert.True(row.IsVisible);
            Assert.True(row.Bounds.Height > 0);
        }
        finally { window.Close(); }
    }

    /// <summary>Trace geometry is inert and crossing it dismisses stale direct or local cards.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TechnicalTraceIsInertAndClearsOnlyItsHoveredOverlay(bool plain)
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(withTrace: true), out MemoryCoverageBar bar);
        bar.IsPlain = plain;
        bar.ReducedMotion = true;
        Render();
        try
        {
            ProportionalStackPanel panel = MainPanel(bar);
            Control trace = Assert.Single(panel.GetVisualDescendants().OfType<Control>(), static control => control.Name == "MemoryTraceSpacer");
            Assert.False(trace.Focusable);
            Assert.False(trace.IsHitTestVisible);
            Assert.Null(ToolTip.GetTip(trace));
            Assert.True(string.IsNullOrEmpty(global::Avalonia.Automation.AutomationProperties.GetName(trace)));
            Assert.Equal(0x80000, panel.Children.Sum(ProportionalStackPanel.GetWeight));
            Control primary = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(primary, window).Center, RawInputModifiers.None);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            window.MouseMove(BoundsInWindow(trace, window).Center, RawInputModifiers.None);
            Render();
            AssertNoOverlay(window);
            window.MouseMove(BoundsInWindow(primary, window).Center, RawInputModifiers.None);
            Render();
            Assert.True(MainTarget(bar, 3).Focus());
            Render();
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.True(FocusableControl(LocalStrip(local).Children[0]).Focus());
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            window.MouseMove(BoundsInWindow(trace, window).Center, RawInputModifiers.None);
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    private static Window CreateWindow(
        int width,
        bool dark,
        IEnumerable<MemoryCoverageSegmentViewModel> slices,
        out MemoryCoverageBar bar)
    {
        bar = new MemoryCoverageBar
        {
            ItemsSource = slices,
            Labels = ShellTextResources.For(ShellLanguage.English),
        };
        var window = new Window
        {
            Width = width + 32,
            Height = 620,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = new ShellFixture(ShellTextResources.For(ShellLanguage.English)),
            Content = new Border { Padding = new Thickness(16), Child = bar },
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        return window;
    }

    private static Window CreateScrolledWindow(
        IEnumerable<MemoryCoverageSegmentViewModel> slices,
        out MemoryCoverageBar bar,
        out ScrollViewer scroll)
    {
        bar = new MemoryCoverageBar
        {
            ItemsSource = slices,
            Labels = ShellTextResources.For(ShellLanguage.English),
        };
        scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Padding = new Thickness(16), Child = bar },
                    new Border { Height = 640 },
                },
            },
        };
        var window = new Window
        {
            Width = 420,
            Height = 300,
            DataContext = new ShellFixture(ShellTextResources.For(ShellLanguage.English)),
            Content = scroll,
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        return window;
    }

    private static Window CreateBottomWindow(
        bool darkChinese,
        IEnumerable<MemoryCoverageSegmentViewModel> slices,
        out MemoryCoverageBar bar)
    {
        ShellTextResources text = ShellTextResources.For(
            darkChinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        bar = new MemoryCoverageBar { ItemsSource = slices, Labels = text };
        var window = new Window
        {
            Width = 420,
            Height = 900,
            RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = new ShellFixture(text),
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Height = 650 },
                    new Border { Padding = new Thickness(16), Child = bar },
                },
            },
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        return window;
    }

    private static void AddSharedTemplates(Window window)
    {
        var uri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        window.Resources.MergedDictionaries.Add(new ResourceInclude(uri) { Source = uri });
        foreach (Uri styleUri in new[]
        {
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowStyles.axaml"),
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowButtonStyles.axaml"),
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowVisualStyles.axaml"),
        })
        {
            window.Styles.Add(new StyleInclude(styleUri) { Source = styleUri });
        }
    }

    private static ProportionalStackPanel MainPanel(MemoryCoverageBar bar)
    {
        return Assert.Single(bar.GetVisualDescendants().OfType<ProportionalStackPanel>());
    }

    private static Control MainTarget(MemoryCoverageBar bar, int index)
    {
        return FocusableControl(MainPanel(bar).Children[index]);
    }

    private static Control FocusableControl(Control root)
    {
        return Assert.Single(
            root.GetVisualDescendants().Append(root).OfType<Control>(),
            static control => control.Focusable);
    }

    private static ProportionalStackPanel LocalStrip(Border local)
    {
        return Assert.Single(local.GetVisualDescendants().OfType<ProportionalStackPanel>());
    }

    private static Rect BoundsInWindow(Control control, Window window)
    {
        return new Rect(Assert.IsType<Point>(control.TranslatePoint(default, window)), control.Bounds.Size);
    }

    private static T? FindNamed<T>(Window window, string name)
        where T : Control
    {
        return window.GetVisualDescendants().Append(window).OfType<T>()
            .SingleOrDefault(control => control.Name == name);
    }

    private static void PressEscape(Window window)
    {
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
    }

    private static void AssertNoOverlay(Window window)
    {
        Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
        Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
    }

    private static void Capture(Window window, string state)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"memory-popup-{state}.png"));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed record ShellFixture(ShellTextResources Text);
}
