using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Subpixel and adjacent tiny fields have visible minimum targets at actual headless DPI.</summary>
    [AvaloniaTheory]
    [InlineData(240, 1, false, false)]
    [InlineData(240, 1, false, true)]
    [InlineData(240, 1, true, false)]
    [InlineData(240, 1, true, true)]
    [InlineData(240, 1.5, false, false)]
    [InlineData(240, 1.5, false, true)]
    [InlineData(240, 1.5, true, false)]
    [InlineData(240, 1.5, true, true)]
    [InlineData(240, 2, false, false)]
    [InlineData(240, 2, false, true)]
    [InlineData(240, 2, true, false)]
    [InlineData(240, 2, true, true)]
    [InlineData(420, 1, false, false)]
    [InlineData(420, 1, false, true)]
    [InlineData(420, 1, true, false)]
    [InlineData(420, 1, true, true)]
    [InlineData(420, 1.5, false, false)]
    [InlineData(420, 1.5, false, true)]
    [InlineData(420, 1.5, true, false)]
    [InlineData(420, 1.5, true, true)]
    [InlineData(420, 2, false, false)]
    [InlineData(420, 2, false, true)]
    [InlineData(420, 2, true, false)]
    [InlineData(420, 2, true, true)]
    public void TinyMarkersSelectExactFieldsWithoutOverlap(int width, double scale, bool dark, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        MemoryCoverageSegmentViewModel[] slices = TinyMarkerSlices(text);
        Window window = CreateWindow(width, dark, slices, out MemoryCoverageBar bar);
        window.SetRenderScaling(scale);
        window.Height = 1000;
        window.DataContext = new ShellFixture(text);
        bar.Labels = text;
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Assert.Equal(scale, window.RenderScaling);
            Border[] markers = [.. bar.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "MemoryTinyMarker")];
            Assert.Equal(2, markers.Length);
            foreach (Border marker in markers)
            {
                Assert.True(marker.IsEffectivelyVisible);
                Assert.True(marker.Bounds.Width >= 24 && marker.Bounds.Height >= 24);
                Assert.NotNull(marker.Background);
            }
            Assert.False(BoundsInWindow(markers[0], window).Intersects(BoundsInWindow(markers[1], window)));
            window.MouseMove(BoundsInWindow(markers[0], window).Center, RawInputModifiers.None);
            Render();
            Assert.Same(slices[1], FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
            Capture(window, $"tiny-marker-{width}-{scale}-{dark}-{chinese}");
            window.MouseMove(BoundsInWindow(markers[1], window).Center, RawInputModifiers.None);
            Render();
            StackPanel list = Assert.IsType<StackPanel>(FindNamed<StackPanel>(window, "MemoryCollisionList"));
            Assert.Equal(3, list.Children.Count);
            Assert.DoesNotContain(text.MemoryLocalViewLabel, list.GetVisualAncestors().OfType<Border>().First().GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text));
            for (int index = 0; index < list.Children.Count; index++)
            {
                Control entry = list.Children[index];
                Assert.True(entry.Bounds.Width >= 24 && entry.Bounds.Height >= 24);
                if (index > 0) { Assert.True(entry.Bounds.Top >= list.Children[index - 1].Bounds.Bottom); }
                window.MouseMove(BoundsInWindow(entry, window).Center, RawInputModifiers.None);
                Render();
                Assert.Same(slices[index + 3], FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
                Assert.Equal(3, slices[index + 3].RangeEndExclusive - slices[index + 3].RangeStart);
            }
            Capture(window, $"tiny-collision-{width}-{scale}-{dark}-{chinese}");
            Assert.Equal(0x401A, slices[1].RangeStart);
            Assert.Equal(0x401D, slices[1].RangeEndExclusive);
            Assert.Equal([0x20000, 0x20003, 0x20006], slices.Skip(3).Take(3).Select(static slice => slice.RangeStart));
        }
        finally { window.Close(); }
    }

    /// <summary>Collision entries share the existing layered keyboard and teardown lifecycle.</summary>
    [AvaloniaFact]
    public void TinyCollisionKeyboardRestoresMarkerAndDropsOldTargetsOnRebuild()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryCoverageSegmentViewModel[] slices = TinyMarkerSlices(text);
        Window window = CreateWindow(420, false, slices, out MemoryCoverageBar bar);
        bar.ReducedMotion = true;
        Render();
        try
        {
            Border marker = bar.GetVisualDescendants().OfType<Border>().Last(control => control.Name == "MemoryTinyMarker");
            Assert.True(marker.Focus(NavigationMethod.Tab));
            Render();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Render();
            StackPanel list = Assert.IsType<StackPanel>(FindNamed<StackPanel>(window, "MemoryCollisionList"));
            Assert.True(list.Children[0].IsFocused);
            Assert.Same(slices[3], FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            window.KeyRelease(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            Render();
            Assert.True(list.Children[2].IsFocused);
            Assert.Same(slices[5], FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
            PressEscape(window);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            PressEscape(window);
            Render();
            Assert.True(marker.IsFocused);
            Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
            bar.ItemsSource = TransitSlices();
            Render();
            Assert.DoesNotContain(slices, static slice => slice.Interaction.IsRailActive);
            Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.DoesNotContain(marker, bar.GetVisualDescendants());
        }
        finally { window.Close(); }
    }

    /// <summary>Legend emphasis changes only the row background; the transparent border keeps geometry fixed.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PassiveLegendEmphasisChangesOnlyTheBackground(bool dark, bool chinese)
    {
        Window window = CreateWindow(420, dark, TransitSlices(), out MemoryCoverageBar bar);
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        window.DataContext = new ShellFixture(text);
        bar.Labels = text;
        bar.ShowLegend = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Border row = bar.GetVisualDescendants().OfType<Border>().First(control => control.Name == "MemoryLegendTarget");
            Rect bounds = row.Bounds;
            Thickness border = row.BorderThickness;
            Rect[] textBounds = [.. row.GetVisualDescendants().OfType<TextBlock>().Select(block => BoundsInWindow(block, window))];
            Assert.Null(row.Background);
            window.MouseMove(BoundsInWindow(MainTarget(bar, 0), window).Center, RawInputModifiers.None);
            Render();
            Assert.Contains("railActive", row.Classes);
            Assert.Equal(bounds, row.Bounds);
            Assert.Equal(textBounds, row.GetVisualDescendants().OfType<TextBlock>().Select(block => BoundsInWindow(block, window)));
            Assert.Equal(border, row.BorderThickness);
            Assert.Equal(new Thickness(2), border);
            Assert.Equal(Colors.Transparent, Assert.IsType<ISolidColorBrush>(row.BorderBrush, exactMatch: false).Color);
            Assert.NotNull(row.Background);
            Assert.Equal(Matrix.Identity, row.RenderTransform?.Value ?? Matrix.Identity);
            Capture(window, $"passive-highlight-{dark}-{chinese}");
            window.Resources["NfcMemoryInteractionSurfaceBrush"] = Brushes.Magenta;
            Render();
            Assert.Equal(Colors.Magenta, Assert.IsType<ISolidColorBrush>(row.Background, exactMatch: false).Color);
            Assert.Equal(Colors.Transparent, Assert.IsType<ISolidColorBrush>(row.BorderBrush, exactMatch: false).Color);
        }
        finally { window.Close(); }
    }

    private static MemoryCoverageSegmentViewModel[] TinyMarkerSlices(ShellTextResources text)
    {
        long[] starts = [0, 0x401A, 0x401D, 0x20000, 0x20003, 0x20006, 0x20009, 0x28000];
        return [.. Enumerable.Range(0, starts.Length - 1).Select(index =>
            new MemoryCoverageSegmentViewModel($"flash [0x{starts[index]:X},0x{starts[index + 1]:X})",
                $"Field {index}", "Declared display fixture", MemoryCoverageFillRole.Dp, starts[index + 1] - starts[index],
                text: text, rangeStart: starts[index], rangeEndExclusive: starts[index + 1], addressSpaceId: "flash",
                addressRangeLabel: $"[0x{starts[index]:X},0x{starts[index + 1]:X})"))];
    }

    /// <summary>A dense collision list leaves visible room for its terminal card in a normal-height window.</summary>
    [AvaloniaTheory]
    [InlineData(240)]
    [InlineData(420)]
    public void DenseTinyCollisionKeepsCardAndScrollWithinWindow(int width)
    {
        long[] starts = [0, .. Enumerable.Range(0, 13).Select(static index => 0x10000L + (index * 3)), 0x80000];
        MemoryCoverageSegmentViewModel[] slices = [.. Enumerable.Range(0, starts.Length - 1).Select(index =>
            new MemoryCoverageSegmentViewModel($"range-{index}", $"Field {index}", "Display fixture", MemoryCoverageFillRole.Dp,
                starts[index + 1] - starts[index], rangeStart: starts[index], rangeEndExclusive: starts[index + 1],
                addressSpaceId: "flash", addressRangeLabel: $"[0x{starts[index]:X},0x{starts[index + 1]:X})"))];
        Window window = CreateWindow(width, false, slices, out MemoryCoverageBar bar);
        bar.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Border marker = Assert.Single(bar.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemoryTinyMarker");
            window.MouseMove(BoundsInWindow(marker, window).Center, RawInputModifiers.None);
            Render();
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            ScrollViewer scroll = Assert.Single(local.GetVisualDescendants().OfType<ScrollViewer>());
            StackPanel list = Assert.IsType<StackPanel>(FindNamed<StackPanel>(window, "MemoryCollisionList"));
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            window.MouseMove(BoundsInWindow(list.Children[0], window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Capture(window, $"tiny-dense-{width}");
            Assert.True(BoundsInWindow(card, window).Bottom <= window.Bounds.Height);
            Assert.False(BoundsInWindow(card, window).Intersects(BoundsInWindow(local, window)));
            window.MouseWheel(BoundsInWindow(list.Children[0], window).Center, new Vector(0, -3), RawInputModifiers.None);
            Render();
            Assert.True(scroll.Offset.Y > 0);
            Assert.NotSame(slices[1], FindNamed<Border>(window, "MemorySliceCard")?.DataContext);
            Assert.Same(local, FindNamed<Border>(window, "MemoryLocalView"));
            Control visible = list.Children.First(entry => BoundsInWindow(entry, window).Top > BoundsInWindow(scroll, window).Top);
            window.MouseMove(BoundsInWindow(visible, window).Center, RawInputModifiers.None);
            Render();
            Assert.Same(visible.DataContext, FindNamed<Border>(window, "MemorySliceCard")!.DataContext);
        }
        finally { window.Close(); }
    }
}
