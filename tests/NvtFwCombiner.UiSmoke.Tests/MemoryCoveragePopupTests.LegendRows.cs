using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Legend rows retain square markers and readable addresses without changing rail geometry.</summary>
    [AvaloniaTheory]
    [InlineData(388, false)]
    [InlineData(388, true)]
    [InlineData(620, false)]
    [InlineData(620, true)]
    public void LegendKeepsOneRangePerRowWithSquareMarker(int width, bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = [
            new("0x00000-0x00FFF", "DP AB", "Display fixture", MemoryCoverageFillRole.Dp, 4096, rangeStart: 0, rangeEndExclusive: 4096),
            new("0x01000-0x01FFF", "TPA", "Display fixture", MemoryCoverageFillRole.Tp, 4096, rangeStart: 4096, rangeEndExclusive: 8192),
            new("0x02000-0x02FFF", "ab-combiner-work", "Display fixture", MemoryCoverageFillRole.Source, 4096, rangeStart: 8192, rangeEndExclusive: 12288)];
        Window window = CreateWindow(width, dark, slices, out MemoryCoverageBar bar);
        try
        {
            double railWidth = bar.Bounds.Width;
            bar.ShowLegend = true;
            Render();
            Border[] rows = [.. bar.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Name == "MemoryLegendTarget")];
            Assert.Equal(slices.Length, rows.Length);
            Assert.Equal(railWidth, bar.Bounds.Width);
            double railLeft = BoundsInWindow(FindNamed<ItemsControl>(window, "MemoryMainRail")!, window).Left;
            Rect? previous = null;
            double? addressLeft = null;
            foreach (Border row in rows)
            {
                Rect bounds = BoundsInWindow(row, window);
                if (previous is { } prior)
                {
                    Assert.True(bounds.Top >= prior.Bottom, $"Legend rows overlap: {prior} / {bounds}");
                    Assert.InRange(Math.Abs(bounds.Left - prior.Left), 0, 1);
                }
                previous = bounds;
                MemoryCoverageSegmentViewModel slice = Assert.IsType<MemoryCoverageSegmentViewModel>(row.DataContext);
                TextBlock title = Assert.Single(row.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == slice.DisplayTitle);
                TextBlock address = Assert.Single(row.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == slice.AddressRangeLabel);
                Rect titleBounds = BoundsInWindow(title, window);
                Rect addressBounds = BoundsInWindow(address, window);
                if (addressLeft is { } expectedLeft) { Assert.InRange(Math.Abs(addressBounds.Left - expectedLeft), 0, 1); }
                addressLeft = addressBounds.Left;
                Assert.Equal(title.FontSize, address.FontSize);
                Assert.True(addressBounds.Left >= titleBounds.Right);
                Assert.True(addressBounds.Right <= bounds.Right + 1);
                Assert.InRange(Math.Abs(addressBounds.Center.Y - titleBounds.Center.Y), 0, 1);
                Border marker = Assert.Single(row.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("memoryCoverageMarker"));
                Assert.Equal(new Size(10, 10), marker.Bounds.Size);
                Assert.Equal(new CornerRadius(2), marker.CornerRadius);
                Assert.InRange(Math.Abs(BoundsInWindow(marker, window).Left - railLeft), 0, 1);
            }
        }
        finally { window.Close(); }
    }
}
