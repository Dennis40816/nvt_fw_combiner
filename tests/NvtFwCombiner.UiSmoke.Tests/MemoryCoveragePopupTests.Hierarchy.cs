using Avalonia.Controls;
using Avalonia;
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>A later legend row must not hide earlier targets when its card opens upward.</summary>
    [AvaloniaFact]
    public void UpperCardFromWrappedLegendKeepsAllLegendRowsVisible()
    {
        MemoryCoverageSegmentViewModel[] slices = [.. Enumerable.Range(0, 4).Select(index =>
            new MemoryCoverageSegmentViewModel("range", $"Source {index}", "detail", MemoryCoverageFillRole.Tp, 1,
                rangeStart: index, rangeEndExclusive: index + 1, contentRole: MemoryContentRole.Tp))];
        Window window = CreateWindow(240, false, slices, out MemoryCoverageBar bar);
        window.Height = 900;
        bar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        bar.Margin = new Thickness(0, 300, 0, 0);
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            WrapPanel legend = FindNamed<WrapPanel>(window, "MemoryLegend")!;
            Control first = legend.Children[0];
            Control last = legend.Children[^1];
            Assert.True(BoundsInWindow(last, window).Top > BoundsInWindow(first, window).Top);
            Assert.True(BoundsInWindow(last, window).Top >= 300);
            Assert.True(last.Focus(NavigationMethod.Tab));
            Render();
            Border card = FindNamed<Border>(window, "MemorySliceCard")!;
            Assert.Same(slices[^1], card.DataContext);
            Assert.False(BoundsInWindow(card, window).Intersects(BoundsInWindow(legend, window)));
        }
        finally { window.Close(); }
    }

    /// <summary>Footer wrapping preserves exact endpoint scale and all disconnected input identities.</summary>
    [AvaloniaTheory]
    [InlineData(240)]
    [InlineData(620)]
    public void LegendWrapsWithoutOverlappingEndpointsAndUpdatesWithInputs(int width)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var region = new MemoryCoverageSegmentViewModel("range", "Normal", "detail", MemoryCoverageFillRole.CtrlRamNormal,
            100, regionGroup: ReplaceRegionGroup.Master, rangeStart: 0, rangeEndExclusive: 100, contentRole: MemoryContentRole.CtrlRam);
        MemoryFocusLaneViewModel lane = Assert.Single(MemoryFocusLaneViewModel.Create(
            [new MemoryCoverageLogicalItemViewModel("normal", [region], text)], text));
        var first = new MemoryCoverageSegmentViewModel("a", "TP FW", "first", MemoryCoverageFillRole.Tp, 200,
            rangeStart: 0, rangeEndExclusive: 200, contentRole: MemoryContentRole.Tp);
        var gap = new MemoryCoverageSegmentViewModel("b", "Unmapped", "gap", MemoryCoverageFillRole.Neutral, 100,
            rangeStart: 200, rangeEndExclusive: 300, contentRole: MemoryContentRole.Unmapped);
        var last = new MemoryCoverageSegmentViewModel("c", "TP FW", "disconnected", MemoryCoverageFillRole.Tp, 200,
            rangeStart: 300, rangeEndExclusive: 500, contentRole: MemoryContentRole.Tp);
        var rows = new ObservableCollection<MemoryCoverageSegmentViewModel> { first, gap, last };
        Window window = CreateWindow(width, false, rows, out MemoryCoverageBar bar);
        bar.ShowLegend = true;
        bar.ShowLabels = true;
        bar.ReducedMotion = true;
        bar.FocusPositions = MemoryFocusLaneViewModel.CreatePositions([lane], 500);
        Render();
        try
        {
            Border endpoint = FindNamed<Border>(window, "MemoryFocusPosition")!;
            WrapPanel legend = FindNamed<WrapPanel>(window, "MemoryLegend")!;
            Rect endpointBounds = BoundsInWindow(endpoint, window);
            Rect legendBounds = BoundsInWindow(legend, window);
            Assert.False(endpointBounds.Intersects(legendBounds));
            Assert.InRange(endpoint.Bounds.Width, (width / 5d) - 1, (width / 5d) + 1);
            Assert.Equal([first, last, gap], legend.Children.Select(child => child.DataContext));
            Assert.All(legend.Children, child => Assert.True(child.Bounds.Right <= legend.Bounds.Width + 1));
            Assert.True(endpoint.Focus(NavigationMethod.Tab));
            Render();
            Border local = FindNamed<Border>(window, "MemoryLocalView")!;
            Assert.True(BoundsInWindow(local, window).Top >= legendBounds.Bottom);
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.True(legend.Children[1].Focus(NavigationMethod.Tab));
            Render();
            Assert.Same(last, FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
            Assert.False(first.Interaction.IsActive);
            Assert.True(legend.Children[^1].Focus(NavigationMethod.Tab));
            Render();
            Border gapCard = FindNamed<Border>(window, "MemorySliceCard")!;
            Assert.Same(gap, gapCard.DataContext);
            Assert.False(BoundsInWindow(gapCard, window).Intersects(legendBounds));
            Assert.True(rows.Remove(last));
            Render();
            AssertNoOverlay(window);
            Assert.Equal([first, gap], legend.Children.Select(child => child.DataContext));
        }
        finally { window.Close(); }
    }

    /// <summary>The declared endpoint is readable below its own address-proportional stroke.</summary>
    [AvaloniaTheory]
    [InlineData(ReplaceRegionGroup.Master, "Master")]
    [InlineData(ReplaceRegionGroup.SlaveRight, "Slave R")]
    [InlineData(ReplaceRegionGroup.SlaveLeft, "Slave L")]
    [InlineData(ReplaceRegionGroup.Base, "•")]
    [InlineData(ReplaceRegionGroup.Other, "•")]
    public void EndpointHasFullNameBelowItsStroke(ReplaceRegionGroup group, string expected)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var slice = new MemoryCoverageSegmentViewModel("range", "Normal", "detail", MemoryCoverageFillRole.CtrlRamNormal,
            100, regionGroup: group, rangeStart: 100, rangeEndExclusive: 200, contentRole: MemoryContentRole.CtrlRam);
        MemoryFocusLaneViewModel lane = Assert.Single(MemoryFocusLaneViewModel.Create(
            [new MemoryCoverageLogicalItemViewModel("normal", [slice], text)], text));
        Window window = CreateWindow(620, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.FocusPositions = MemoryFocusLaneViewModel.CreatePositions([lane], 500);
        Render();
        try
        {
            Border endpoint = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryFocusPosition"));
            TextBlock label = Assert.Single(endpoint.GetVisualDescendants().OfType<TextBlock>());
            Assert.Equal(expected, label.Text);
            Border stroke = Assert.Single(endpoint.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("memoryPositionUnderline"));
            Assert.True(BoundsInWindow(stroke, window).Bottom <= BoundsInWindow(label, window).Top);
            Assert.InRange(endpoint.Bounds.Width, 123, 125);
            AssertNoOverlay(window);
            Assert.True(endpoint.Focus(NavigationMethod.Pointer));
            Render();
            AssertNoOverlay(window);
            window.Focusable = true;
            Assert.True(window.Focus(NavigationMethod.Tab));
            Assert.True(endpoint.Focus(NavigationMethod.Tab));
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
        }
        finally { window.Close(); }
    }

    /// <summary>The short card has explicit labels; technical disclosure never survives closing a card.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApprovedCardNamesAddressSpaceAndResetsDisclosure(bool dark)
    {
        var slice = new MemoryCoverageSegmentViewModel("range", "TP BIN", "Output range will be overlaid from TP BIN.",
            MemoryCoverageFillRole.Tp, 0x35000, rangeStart: 0, rangeEndExclusive: 0x35000,
            addressRangeLabel: "0x00000–0x34FFF", addressSpaceId: "flash");
        Window window = CreateWindow(420, dark, [slice], out MemoryCoverageBar bar);
        bar.ReducedMotion = true;
        try
        {
            Control target = MainTarget(bar, 0);
            Assert.True(target.Focus(NavigationMethod.Tab));
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), b => b.IsEffectivelyVisible && b.Text == "Target Addr");
            Expander disclosure = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
            Assert.False(disclosure.IsExpanded);
            disclosure.IsExpanded = true;
            Render();
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), b => b.IsEffectivelyVisible && b.Text == "Address space");
            Assert.True(BoundsInWindow(card, window).Top >= 0);
            Assert.True(BoundsInWindow(card, window).Bottom <= window.Bounds.Height);
            bar.IsEnabled = false;
            Render();
            bar.IsEnabled = true;
            Assert.True(target.Focus(NavigationMethod.Tab));
            Render();
            card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.False(Assert.Single(card.GetVisualDescendants().OfType<Expander>()).IsExpanded);
            Capture(window, $"approved-card-{dark}");
        }
        finally { window.Close(); }
    }
}
