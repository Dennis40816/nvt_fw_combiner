using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Refreshing the rail during a leaf switch invalidates the old local target instead of reopening it.</summary>
    [AvaloniaFact]
    public void RebuildDuringLeafSwitchDoesNotReopenDetachedTarget()
    {
        Window window = CreateBottomWindow(false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ReducedMotion = true;
        try
        {
            OpenGroupedCard(window, bar);
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Control next = FocusableControl(LocalStrip(local).Children[1]);
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            bool detached = false;
            card.DetachedFromVisualTree += (_, _) =>
            {
                if (detached) { return; }
                detached = true;
                bar.StartAddress = "0x00000";
            };
            _ = next.Focus();
            Render();
            Assert.True(detached);
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>A full-close request during a leaf-only close must also dismiss its local parent after detachment.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidationDuringLeafEscapeAlsoClosesLocalParent(bool hide)
    {
        Window window = CreateBottomWindow(false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ReducedMotion = true;
        try
        {
            OpenGroupedCard(window, bar);
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            bool detached = false;
            card.DetachedFromVisualTree += (_, _) =>
            {
                if (detached) { return; }
                detached = true;
                if (hide) { bar.IsVisible = false; }
                else { bar.IsEnabled = false; }
            };
            Assert.True(Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>()).Focus());
            PressEscape(window);
            Render();
            Assert.True(detached);
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Model synchronous native input/lifecycle callbacks during overlay detachment, not a synthetic byte failure.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CardDetachmentRejectsReentrantCloseAndPointerOpen(bool pointerEntry, bool above)
    {
        MemoryCoverageSegmentViewModel[] slices = TransitSlices();
        Window window = CreateScrolledWindow(slices, out MemoryCoverageBar bar, out ScrollViewer scroll);
        bar.ReducedMotion = true;
        if (above)
        {
            Assert.IsType<StackPanel>(scroll.Content).Children.Insert(0, new Border { Height = 200 });
            Render();
        }
        try
        {
            Control first = MainTarget(bar, 0);
            Control second = MainTarget(bar, 1);
            Point secondPoint = BoundsInWindow(second, window).Center;
            window.MouseMove(BoundsInWindow(first, window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            bool callbackRan = false;
            card.DetachedFromVisualTree += (_, _) =>
            {
                if (callbackRan) { return; }
                callbackRan = true;
                if (pointerEntry) { window.MouseMove(secondPoint, RawInputModifiers.None); }
                else { bar.IsEnabled = false; }
            };
            scroll.Offset = new Vector(0, 1);
            Render();
            Assert.True(callbackRan);
            AssertNoOverlay(window);
            bar.IsEnabled = true;
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            window.MouseMove(BoundsInWindow(second, window).Center, RawInputModifiers.None);
            Render();
            Assert.Same(slices[1], Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard")).DataContext);
        }
        finally { window.Close(); }
    }

    /// <summary>Wheel input at either end of an expanded popup stays inside the popup, never moving the page behind it.</summary>
    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void CardWheelDoesNotScrollTheAncestorPage(int direction)
    {
        Window window = CreateScrolledWindow(TransitSlices(), out MemoryCoverageBar bar, out ScrollViewer scroll);
        bar.ReducedMotion = true;
        scroll.Offset = new Vector(0, 1);
        Render();
        try
        {
            Control target = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Expander disclosure = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
            ToggleButton toggle = Assert.Single(disclosure.GetVisualDescendants().OfType<ToggleButton>());
            Vector beforeExpansion = scroll.Offset;
            Point togglePoint = BoundsInWindow(toggle, window).Center;
            window.MouseMove(togglePoint, RawInputModifiers.None);
            window.MouseDown(togglePoint, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(togglePoint, MouseButton.Left, RawInputModifiers.None);
            Render();
            Assert.True(disclosure.IsExpanded);
            Assert.Equal(beforeExpansion, scroll.Offset);
            if (direction == 0)
            {
                card.MaxHeight = 120;
                Render();
            }
            ScrollViewer inner = Assert.IsType<ScrollViewer>(card.Child);
            double maximum = inner.Extent.Height - inner.Viewport.Height;
            if (direction == 0)
            {
                Assert.True(maximum > 0);
                ScrollBar scrollbar = Assert.Single(inner.GetVisualDescendants().OfType<ScrollBar>(),
                    candidate => candidate.Orientation == Avalonia.Layout.Orientation.Vertical && candidate.IsVisible);
                Assert.False(inner.AllowAutoHide);
                Assert.Equal(14, scrollbar.Bounds.Width);
                Assert.Equal(6, Assert.Single(scrollbar.GetVisualDescendants().OfType<Thumb>()).Bounds.Width);
            }
            inner.Offset = new Vector(0, direction < 0 ? maximum : direction == 0 ? maximum / 2 : 0);
            Render();
            Vector original = scroll.Offset;
            Vector originalInner = inner.Offset;
            Point point = BoundsInWindow(card, window).Center;
            window.MouseMove(point, RawInputModifiers.None);
            window.MouseWheel(point, new Vector(0, direction == 0 ? -1 : direction), RawInputModifiers.None);
            Render();
            Assert.Equal(original, scroll.Offset);
            if (direction == 0) { Assert.True(inner.Offset.Y > originalInner.Y); }
            Assert.Same(card, FindNamed<Border>(window, "MemorySliceCard"));
            Assert.True(disclosure.IsExpanded);
        }
        finally { window.Close(); }
    }

    /// <summary>Both pointer corridors cross footer legend rows without switching the selected source.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CardTransitDoesNotActivateCrossedLegend(bool dark, bool above)
    {
        MemoryCoverageSegmentViewModel[] slices = TransitSlices();
        Window window = above ? CreateBottomWindow(dark, slices, out MemoryCoverageBar bar) : CreateWindow(420, dark, slices, out bar);
        bar.ShowLegend = true;
        bar.Heading = "Flash overview";
        bar.StartAddress = "0x00000";
        bar.EndAddress = "0x3FFFF";
        bar.ReducedMotion = true;
        Render();
        try
        {
            MemoryCoverageSegmentViewModel selected = above ? slices[2] : slices[0];
            Control tp = above ? Assert.Single(bar.GetVisualDescendants().OfType<Border>(),
                control => control.Name == "MemoryLegendTarget" && ReferenceEquals(control.DataContext, selected)) : MainTarget(bar, 0);
            Border dpLegend = Assert.Single(bar.GetVisualDescendants().OfType<Border>(),
                control => control.Name == "MemoryLegendTarget" && ReferenceEquals(control.DataContext, slices[1]));
            Point crossing = BoundsInWindow(dpLegend, window).Center;
            Rect tpBounds = BoundsInWindow(tp, window);
            Assert.InRange(crossing.X, tpBounds.Left, tpBounds.Right);
            window.MouseMove(new Point(crossing.X, tpBounds.Center.Y), RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(selected, card.DataContext);
            Rect cardBounds = BoundsInWindow(card, window);
            Assert.True(above ? cardBounds.Bottom < crossing.Y : cardBounds.Top > crossing.Y);
            window.MouseMove(crossing, RawInputModifiers.None);
            Render();
            Assert.Same(selected, card.DataContext);
            Assert.False(dpLegend.IsPointerOver);
            window.MouseMove(BoundsInWindow(card, window).Center, RawInputModifiers.None);
            Render();
            Assert.Same(selected, card.DataContext);
            Capture(window, $"content-transit-{dark}-{above}");
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
            Render();
            AssertNoOverlay(window);
            window.MouseMove(BoundsInWindow(dpLegend, window).Center, RawInputModifiers.None);
            Render();
            Assert.Same(slices[1], Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard")).DataContext);
        }
        finally { window.Close(); }
    }

    private static MemoryCoverageSegmentViewModel[] TransitSlices()
    {
        return [
        new("tp", "TP FW", "Kept TP", MemoryCoverageFillRole.Tp, 900,
            rangeStart: 0, rangeEndExclusive: 900, contentRole: MemoryContentRole.Tp),
        new("dp", "DP", "Kept DP", MemoryCoverageFillRole.Dp, 50,
            rangeStart: 900, rangeEndExclusive: 950, contentRole: MemoryContentRole.Dp),
        new("unmapped", "Unmapped", "Kept range", MemoryCoverageFillRole.Neutral, 50,
            rangeStart: 950, rangeEndExclusive: 1000, contentRole: MemoryContentRole.Unmapped),
        ];
    }
}
