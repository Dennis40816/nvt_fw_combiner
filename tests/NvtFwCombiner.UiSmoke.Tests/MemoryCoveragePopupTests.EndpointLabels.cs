using Avalonia;
using NvtFwCombiner.Application.MemoryLayout;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using Avalonia.Headless.XUnit;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Endpoint captions retain glyph space even when their physical ranges are very short.</summary>
    [AvaloniaTheory]
    [InlineData(240, false)]
    [InlineData(388, false)]
    [InlineData(620, true)]
    public void EndpointCaptionsHaveNonzeroNaturalWidth(int width, bool dark)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryCoverageSegmentViewModel[] slices =
        [
            new("master", "Normal", "detail", MemoryCoverageFillRole.CtrlRamNormal,
                40, regionGroup: ReplaceRegionGroup.Master, rangeStart: 280, rangeEndExclusive: 320, contentRole: MemoryContentRole.CtrlRam),
            new("slave", "Normal", "detail", MemoryCoverageFillRole.CtrlRamNormal,
                5, regionGroup: ReplaceRegionGroup.SlaveRight, rangeStart: 350, rangeEndExclusive: 355, contentRole: MemoryContentRole.CtrlRam),
        ];
        IReadOnlyList<MemoryFocusLaneViewModel> lanes = MemoryFocusLaneViewModel.Create([new MemoryCoverageLogicalItemViewModel("normal", slices, text)], text);
        var bar = new MemoryCoverageBar
        {
            ItemsSource = MemoryCoverageBarProjectionTests.Example(),
            Labels = text,
            ShowLegend = true,
            FocusPositions = MemoryFocusLaneViewModel.CreatePositions(lanes, 500),
        };
        var window = new Window
        {
            Width = width + 32,
            Height = 620,
            RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light,
            Content = new Border { Padding = new Thickness(16), Child = bar },
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        try
        {
            Capture(window, $"endpoint-captions-{width}-{dark}");
            TextBlock[] labels = [.. bar.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("memoryPositionLabel"))];
            Assert.Equal(2, labels.Length);
            foreach (TextBlock label in labels)
            {
                Assert.True(label.Bounds.Width > 20, $"{label.Text}: Bounds={label.Bounds}, Desired={label.DesiredSize}");
                Assert.True(label.Bounds.Height >= 11);
                Rect bounds = BoundsInWindow(label, window);
                Rect rail = BoundsInWindow(bar, window);
                Assert.True(bounds.Left >= rail.Left - 1 && bounds.Right <= rail.Right + 1);
            }
            Assert.False(BoundsInWindow(labels[0], window).Intersects(BoundsInWindow(labels[1], window)));
        }
        finally { window.Close(); }
    }
}
