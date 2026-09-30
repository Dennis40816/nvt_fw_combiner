using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Empty popup spacing never intercepts the visible legend under it.</summary>
    [AvaloniaFact]
    public void TransparentPopupSpacingDoesNotInterceptLegend()
    {
        Window window = CreateWindow(420, false, TransitSlices(), out MemoryCoverageBar bar);
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            window.MouseMove(BoundsInWindow(MainTarget(bar, 0), window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Control frame = Assert.IsType<Control>(card.GetVisualParent(), exactMatch: false);
            Border row = bar.GetVisualDescendants().OfType<Border>().First(control => control.Name == "MemoryLegendTarget");
            window.MouseMove(BoundsInWindow(row, window).Center, RawInputModifiers.None);
            Render();
            Assert.False(frame.IsPointerOver);
            Assert.False(BoundsInWindow(card, window).Intersects(BoundsInWindow(MainTarget(bar, 0), window)));
        }
        finally { window.Close(); }
    }

    /// <summary>Legend input is inert; only the corresponding rail input emphasizes its row.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PassiveLegendDoesNotOpenOrRetainCards(bool dark)
    {
        var clock = new MemoryCoverageCloseScheduler();
        MemoryCoverageSegmentViewModel[] slices = TransitSlices();
        Window window = CreateWindow(420, dark, slices, out MemoryCoverageBar bar, clock.Schedule);
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Border[] rows = [.. bar.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "MemoryLegendTarget")];
            foreach (Border row in rows)
            {
                Point point = BoundsInWindow(row, window).Center;
                window.MouseMove(point, RawInputModifiers.None);
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                Assert.False(row.Focus(NavigationMethod.Tab));
                Assert.False(MemoryCoverageInteractionBehavior.GetIsEnabled(row));
                Assert.Null(ToolTip.GetTip(row));
                Render();
                AssertNoOverlay(window);
            }
            Control rail = MainTarget(bar, 0);
            Rect[] original = [.. rows.Select(row => row.Bounds)];
            window.MouseMove(BoundsInWindow(rail, window).Center, RawInputModifiers.None);
            Render();
            Assert.Contains("railActive", rows[0].Classes);
            Assert.DoesNotContain("railActive", rows[1].Classes);
            Border card = FindNamed<Border>(window, "MemorySliceCard")!;
            window.MouseMove(BoundsInWindow(card, window).Center, RawInputModifiers.None);
            Render();
            // The card belongs to its slice, so the slice's legend row stays lit while the pointer is on the card.
            Assert.Contains("railActive", rows[0].Classes);
            Assert.DoesNotContain("railActive", rows[1].Classes);
            Assert.Same(slices[0], card.DataContext);
            Assert.Equal(original, rows.Select(row => row.Bounds));
            window.MouseMove(BoundsInWindow(rows[1], window).Center, RawInputModifiers.None);
            clock.AdvanceBy(TimeSpan.FromMilliseconds(319));
            Render();
            Assert.Same(card, FindNamed<Border>(window, "MemorySliceCard"));
            clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
            Render();
            AssertNoOverlay(window);
            Assert.True(rail.Focus(NavigationMethod.Tab));
            Render();
            Assert.Contains("railActive", rows[0].Classes);
            // Passive legend motion must not take keyboard ownership away from the rail.
            window.MouseMove(BoundsInWindow(rows[2], window).Center, RawInputModifiers.None);
            Render();
            Assert.Contains("railActive", rows[0].Classes);
        }
        finally { window.Close(); }
    }

    /// <summary>A completed passive close rearms one real move, but Escape never rebounds.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PassiveCloseReopensOnceOnRealSameSliceMove(bool plain)
    {
        var clock = new MemoryCoverageCloseScheduler();
        Window window = CreateWindow(420, false, TransitSlices(), out MemoryCoverageBar bar, clock.Schedule);
        bar.IsPlain = plain;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Control target = MainTarget(bar, 0);
            Point point = BoundsInWindow(target, window).Center;
            window.MouseMove(point, RawInputModifiers.None);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            bar.Margin = new Thickness(1, 0, 0, 0);
            Render();
            clock.AdvanceBy(TimeSpan.FromMilliseconds(320));
            AssertNoOverlay(window);
            Assert.True(target.IsPointerOver);
            window.MouseMove(point, RawInputModifiers.None);
            Render();
            AssertNoOverlay(window);
            window.MouseMove(point + new Vector(1, 0), RawInputModifiers.None);
            Render();
            Border reopened = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Control? content = reopened.Child;
            for (int move = 0; move < 200; move++)
            {
                window.MouseMove(point + new Vector(2 + (move % 10), 0), RawInputModifiers.None);
            }
            Render();
            Assert.Same(content, reopened.Child);
            Assert.True(target.Focus(NavigationMethod.Tab));
            PressEscape(window);
            window.MouseMove(point + new Vector(15, 0), RawInputModifiers.None);
            Render();
            clock.AdvanceBy(TimeSpan.FromMilliseconds(320));
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Extent-only publication below the rail does not invalidate its anchored card.</summary>
    [AvaloniaFact]
    public void PassiveExtentChangeKeepsStationaryCard()
    {
        Window window = CreateScrolledWindow(TransitSlices(), out MemoryCoverageBar bar, out ScrollViewer scroll);
        bar.ReducedMotion = true;
        try
        {
            Control target = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(target, window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Rect before = BoundsInWindow(bar, window);
            Assert.IsType<StackPanel>(scroll.Content).Children.Add(new Border { Height = 50 });
            Render();
            Assert.Equal(before, BoundsInWindow(bar, window));
            Assert.Same(card, FindNamed<Border>(window, "MemorySliceCard"));
        }
        finally { window.Close(); }
    }

    /// <summary>Reset, detachment and disable invalidate a completed passive-close rearm.</summary>
    [AvaloniaTheory]
    [InlineData("reset", false)]
    [InlineData("detach", false)]
    [InlineData("disable", false)]
    [InlineData("reset", true)]
    [InlineData("detach", true)]
    [InlineData("disable", true)]
    public void PassiveCloseInvalidationDoesNotReopen(string invalidation, bool plain)
    {
        var clock = new MemoryCoverageCloseScheduler();
        Window window = CreateWindow(420, false, TransitSlices(), out MemoryCoverageBar bar, clock.Schedule);
        bar.IsPlain = plain;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Control target = MainTarget(bar, 0);
            Point point = BoundsInWindow(target, window).Center;
            window.MouseMove(point, RawInputModifiers.None);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            bar.Margin = new Thickness(1, 0, 0, 0);
            Render();
            clock.AdvanceBy(TimeSpan.FromMilliseconds(320));
            AssertNoOverlay(window);
            Assert.True(target.IsPointerOver);
            switch (invalidation)
            {
                case "reset": bar.ItemsSource = Array.Empty<MemoryCoverageSegmentViewModel>(); break;
                case "detach": window.Content = null; break;
                case "disable": bar.IsEnabled = false; bar.IsEnabled = true; break;
                default: throw new ArgumentOutOfRangeException(nameof(invalidation));
            }
            window.MouseMove(point + new Vector(1, 0), RawInputModifiers.None);
            Render();
            clock.AdvanceBy(TimeSpan.FromSeconds(1));
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    /// <summary>Actual Tab and Shift+Tab reach a three-byte leaf; Escape returns to its opener.</summary>
    [AvaloniaFact]
    public void TinySliceIsReachableByRealKeyboardTraversal()
    {
        MemoryCoverageSegmentViewModel[] slices =
        [
            new("before", "Before", "before", MemoryCoverageFillRole.Dp, 0x401A, rangeStart: 0, rangeEndExclusive: 0x401A, addressSpaceId: "flash"),
            new("tiny", "Tiny", "tiny", MemoryCoverageFillRole.Neutral, 3, rangeStart: 0x401A, rangeEndExclusive: 0x401D, addressSpaceId: "flash"),
            new("after", "After", "after", MemoryCoverageFillRole.Tp, 0x3BFE3, rangeStart: 0x401D, rangeEndExclusive: 0x40000, addressSpaceId: "flash"),
        ];
        Window window = CreateWindow(420, false, slices, out MemoryCoverageBar bar);
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Control first = MainTarget(bar, 0);
            Control tiny = MainTarget(bar, 1);
            Assert.True(first.Focus(NavigationMethod.Tab));
            PressEscape(window);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Render();
            Assert.True(tiny.IsFocused);
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(slices[1], card.DataContext);
            Assert.Equal(0x401A, slices[1].RangeStart);
            Assert.Equal(0x401D, slices[1].RangeEndExclusive);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Render();
            Assert.True(Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>()).IsFocused);
            PressEscape(window);
            Assert.True(tiny.IsFocused);
            AssertNoOverlay(window);
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            Render();
            Assert.True(first.IsFocused);
        }
        finally { window.Close(); }
    }
}
