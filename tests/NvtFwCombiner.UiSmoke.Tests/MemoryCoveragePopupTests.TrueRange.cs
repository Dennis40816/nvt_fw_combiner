using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
    /// <summary>A CtrlRAM focus lane is the exception to the true-range rule: its stem leaves from below its own label.</summary>
    [AvaloniaTheory]
    [InlineData(240)]
    [InlineData(620)]
    public void LaneStemStartsBelowItsPositionLabel(int width)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var region = new MemoryCoverageSegmentViewModel("range", "Normal", "detail", MemoryCoverageFillRole.CtrlRamNormal,
            100, regionGroup: ReplaceRegionGroup.Master, rangeStart: 0, rangeEndExclusive: 100, contentRole: MemoryContentRole.CtrlRam);
        MemoryFocusLaneViewModel lane = Assert.Single(MemoryFocusLaneViewModel.Create(
            [new MemoryCoverageLogicalItemViewModel("normal", [region], text)], text));
        MemoryCoverageSegmentViewModel[] slices =
        [
            new("a", "TP FW", "first", MemoryCoverageFillRole.Tp, 200, rangeStart: 0, rangeEndExclusive: 200, contentRole: MemoryContentRole.Tp),
            new("b", "Unmapped", "gap", MemoryCoverageFillRole.Neutral, 300, rangeStart: 200, rangeEndExclusive: 500, contentRole: MemoryContentRole.Unmapped),
        ];
        Window window = CreateWindow(width, false, slices, out MemoryCoverageBar bar);
        bar.ShowLegend = true;
        bar.ShowLabels = true;
        bar.ReducedMotion = true;
        bar.FocusPositions = MemoryFocusLaneViewModel.CreatePositions([lane], 500);
        Render();
        try
        {
            Border endpoint = FindNamed<Border>(window, "MemoryFocusPosition")!;
            Rect label = BoundsInWindow(endpoint.GetVisualDescendants().OfType<TextBlock>().Single(static text => text.Text == "Master"), window);
            Rect row = BoundsInWindow(endpoint.GetVisualAncestors().OfType<ItemsControl>().First(), window);
            Assert.True(endpoint.Focus(NavigationMethod.Tab));
            Render();
            Border local = FindNamed<Border>(window, "MemoryLocalView")!;
            Assert.True(BoundsInWindow(local, window).Top > row.Bottom);
            AssertStemStartsAt(window, bar, (Control)local.GetVisualParent()!, "MemoryLocalAnchor", row.Bottom, label.Center.X);
        }
        finally { window.Close(); }
    }

    /// <summary>A lone tiny marker keeps its 24 px handle, but its card stem starts at the slice's true range.</summary>
    [AvaloniaTheory]
    [InlineData(240)]
    [InlineData(420)]
    public void SingleTinyMarkerCardStemStartsAtTheTrueRange(int width)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryCoverageSegmentViewModel[] slices = TinyMarkerSlices(text);
        Window window = CreateWindow(width, false, slices, out MemoryCoverageBar bar);
        window.Height = 1000;
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Rect rail = BoundsInWindow(FindNamed<ItemsControl>(window, "MemoryMainRail")!, window);
            Border marker = bar.GetVisualDescendants().OfType<Border>().First(control => control.Name == "MemoryTinyMarker");
            window.MouseMove(BoundsInWindow(marker, window).Center, RawInputModifiers.None);
            Render();
            Border card = FindNamed<Border>(window, "MemorySliceCard")!;
            Assert.Same(slices[1], card.DataContext);
            AssertStemStartsAtTrueRange(window, bar, (Control)card.GetVisualParent()!, "MemoryCardAnchor",
                rail, rail.Left + (rail.Width * SliceCenterFraction(slices, 1, 1)));
        }
        finally { window.Close(); }
    }

    /// <summary>A collision list's stem starts at the middle of the range its markers stand for.</summary>
    [AvaloniaTheory]
    [InlineData(240)]
    [InlineData(420)]
    public void GroupedTinyMarkerStemStartsAtTheTrueRange(int width)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryCoverageSegmentViewModel[] slices = TinyMarkerSlices(text);
        Window window = CreateWindow(width, false, slices, out MemoryCoverageBar bar);
        window.Height = 1000;
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Rect rail = BoundsInWindow(FindNamed<ItemsControl>(window, "MemoryMainRail")!, window);
            Border marker = bar.GetVisualDescendants().OfType<Border>().Last(control => control.Name == "MemoryTinyMarker");
            window.MouseMove(BoundsInWindow(marker, window).Center, RawInputModifiers.None);
            Render();
            Border local = FindNamed<Border>(window, "MemoryLocalView")!;
            double fraction = (SliceCenterFraction(slices, 3, 3) + SliceCenterFraction(slices, 5, 5)) / 2;
            AssertStemStartsAtTrueRange(window, bar, (Control)local.GetVisualParent()!, "MemoryLocalAnchor",
                rail, rail.Left + (rail.Width * fraction));
        }
        finally { window.Close(); }
    }

    /// <summary>A handle clamped inside the rail still points its stem at the true range at either end.</summary>
    [AvaloniaTheory]
    [InlineData(240)]
    [InlineData(420)]
    public void EdgeTinyMarkersKeepTheTrueRangeCenterInsteadOfTheClampedHandle(int width)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        long[] starts = [0, 3, 0x27FFD, 0x28000];
        MemoryCoverageSegmentViewModel[] slices = [.. Enumerable.Range(0, starts.Length - 1).Select(index =>
            new MemoryCoverageSegmentViewModel($"flash [0x{starts[index]:X},0x{starts[index + 1]:X})",
                $"Field {index}", "Declared display fixture", MemoryCoverageFillRole.Dp, starts[index + 1] - starts[index],
                text: text, rangeStart: starts[index], rangeEndExclusive: starts[index + 1], addressSpaceId: "flash",
                addressRangeLabel: $"[0x{starts[index]:X},0x{starts[index + 1]:X})"))];
        Window window = CreateWindow(width, false, slices, out MemoryCoverageBar bar);
        window.Height = 1000;
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Rect rail = BoundsInWindow(FindNamed<ItemsControl>(window, "MemoryMainRail")!, window);
            Border[] markers = [.. bar.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "MemoryTinyMarker")];
            Assert.Equal(2, markers.Length);
            Assert.True(BoundsInWindow(markers[0], window).Center.X - rail.Left >= 11.5);
            foreach ((Border marker, int index) in markers.Zip([0, 2]))
            {
                window.MouseMove(BoundsInWindow(marker, window).Center, RawInputModifiers.None);
                Render();
                Border card = FindNamed<Border>(window, "MemorySliceCard")!;
                Assert.Same(slices[index], card.DataContext);
                AssertStemStartsAtTrueRange(window, bar, (Control)card.GetVisualParent()!, "MemoryCardAnchor",
                    rail, rail.Left + (rail.Width * SliceCenterFraction(slices, index, index)));
            }
        }
        finally { window.Close(); }
    }

    private static double SliceCenterFraction(MemoryCoverageSegmentViewModel[] slices, int first, int last)
    {
        double total = slices.Sum(static slice => slice.BarWidth);
        double before = slices.Take(first).Sum(static slice => slice.BarWidth);
        double span = slices.Skip(first).Take(last - first + 1).Sum(static slice => slice.BarWidth);
        return (before + (span / 2)) / total;
    }

    private static void AssertStemStartsAtTrueRange(Window window, MemoryCoverageBar bar, Control frame, string anchorName, Rect rail, double expectedX)
    {
        AssertStemStartsAt(window, bar, frame, anchorName, rail.Bottom, expectedX);
    }

    private static void AssertStemStartsAt(Window window, MemoryCoverageBar bar, Control frame, string anchorName, double expectedY, double expectedX)
    {
        Ellipse dot = Assert.Single(frame.GetVisualDescendants().OfType<Ellipse>(), item => item.Name == anchorName);
        Point anchor = BoundsInWindow(dot, window).Center;
        Assert.InRange(Math.Abs(anchor.X - expectedX), 0, 1);
        Assert.InRange(Math.Abs(anchor.Y - expectedY), 0, 1.5);
        Rect[] strokes = AssertStemsClearOfOverviewGlyphs(window, bar, frame);
        Assert.NotEmpty(strokes);
        Assert.InRange(Math.Abs(strokes.Min(static stroke => stroke.Top) - expectedY), 0, 1.5);
        Assert.All(strokes, stroke => Assert.InRange(Math.Abs(stroke.Center.X - expectedX), 0, 1.5));
    }
}
