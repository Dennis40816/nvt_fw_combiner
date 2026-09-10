using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Actual shared rail arrangement preserves its exact byte weights at narrow and wide sizes.</summary>
public sealed class MemoryCoverageBarGeometryTests
{
    /// <summary>Grouping does not introduce minimum-width inflation or lose address-proportional placement.</summary>
    [AvaloniaTheory]
    [InlineData(240, false)]
    [InlineData(388, false)]
    [InlineData(620, false)]
    [InlineData(240, true)]
    [InlineData(388, true)]
    [InlineData(620, true)]
    public void GroupedMainRailRetainsExactProportionalBounds(int width, bool plain)
    {
        var bar = new MemoryCoverageBar
        {
            ItemsSource = MemoryCoverageBarProjectionTests.Example(),
            Labels = ShellTextResources.For(ShellLanguage.English),
            IsPlain = plain,
        };
        var window = new Window { Width = width + 32, Height = 400, Content = new Border { Padding = new Thickness(16), Child = bar } };
        var uri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        window.Resources.MergedDictionaries.Add(new ResourceInclude(uri) { Source = uri });
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            ProportionalStackPanel panel = Assert.Single(bar.GetVisualDescendants().OfType<ProportionalStackPanel>());
            Assert.Equal(3, panel.Children.Count);
            Assert.Equal(width, panel.Bounds.Width);
            Assert.Equal(34, bar.Bounds.Height);
            Border[] mainSlices = [.. panel.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("memoryExplorerSlice"))];
            Assert.Equal(2, mainSlices.Length);
            Assert.All(mainSlices, slice => Assert.Equal(default, slice.BorderThickness));
            Assert.InRange(Math.Abs(panel.Children[1].Bounds.X - (width * 0.4921875)), 0, 1);
            Assert.InRange(Math.Abs(panel.Children[1].Bounds.Width - (width / 32d)), 0, 1);
            Assert.InRange(Math.Abs(panel.Children[^1].Bounds.Right - width), 0, 1);
            Assert.Equal(0, panel.Children[0].Bounds.Left);
            // Avalonia snaps each child independently to pixels. Check covered extent and
            // boundary error, not the sum of overlapping rasterized child widths.
            for (int index = 1; index < panel.Children.Count; index++)
            {
                Assert.InRange(Math.Abs(panel.Children[index].Bounds.Left - panel.Children[index - 1].Bounds.Right), 0, 1);
            }
            Assert.Equal(0x80000, panel.Children.Sum(ProportionalStackPanel.GetWeight));
            Assert.Equal(0x4000, ProportionalStackPanel.GetWeight(panel.Children[1]));
            MemoryCoverageBarItem group = Assert.IsType<MemoryCoverageBarItem>(panel.Children[1].DataContext);
            Assert.Equal(8, group.Slices.Count);
        }
        finally { window.Close(); }
    }
}
