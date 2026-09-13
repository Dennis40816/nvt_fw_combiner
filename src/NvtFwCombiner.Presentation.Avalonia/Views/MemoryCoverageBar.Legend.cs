using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class MemoryCoverageBar
{
    private readonly MemoryFooterPanel _footer = new();
    private readonly WrapPanel _legend = new() { Name = "MemoryLegend", Margin = new Thickness(0, 6, 0, 0) };

    // Text is not a byte range: preserve its natural width and clamp it to the rail.
    // The stroke and hover endpoint retain their original proportional geometry.
    private sealed class MemoryEndpointPanel : Panel
    {
        private readonly MemoryCoverageBar _owner;
        private readonly double _startFraction;
        private readonly double _widthFraction;

        internal MemoryEndpointPanel(MemoryCoverageBar owner, double startFraction, double widthFraction, Border marker, TextBlock label)
        {
            _owner = owner;
            _startFraction = startFraction;
            _widthFraction = widthFraction;
            Children.Add(marker);
            Children.Add(label);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Children[0].Measure(availableSize);
            Children[1].Measure(Size.Infinity);
            return new Size(0, 22);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double start = _owner.Bounds.Width * _startFraction;
            double width = Math.Min(Children[1].DesiredSize.Width, _owner.Bounds.Width);
            double left = GetLabelLeft();
            Children[0].Arrange(new Rect(0, 0, finalSize.Width, 2));
            Children[1].Arrange(new Rect(left - start, 4, width, Math.Max(0, finalSize.Height - 4)));
            return finalSize;
        }

        private double GetLabelLeft()
        {
            MemoryEndpointPanel[] peers = [.. _owner._positions.GetVisualDescendants().OfType<MemoryEndpointPanel>()
                .OrderBy(static peer => peer._startFraction)];
            double[] widths = [.. peers.Select(peer => Math.Min(peer.Children[1].DesiredSize.Width, _owner.Bounds.Width))];
            var lefts = new double[peers.Length];
            double cursor = 0;
            for (int index = 0; index < peers.Length; index++)
            {
                double center = _owner.Bounds.Width * (peers[index]._startFraction + (peers[index]._widthFraction / 2));
                lefts[index] = Math.Max(cursor, center - (widths[index] / 2));
                cursor = lefts[index] + widths[index] + 6;
            }
            // Pack back from the right edge when a short end range cannot center its label.
            cursor = _owner.Bounds.Width;
            for (int index = peers.Length - 1; index >= 0; index--)
            {
                lefts[index] = Math.Min(lefts[index], cursor - widths[index]);
                cursor = lefts[index] - 6;
            }
            return lefts[Array.IndexOf(peers, this)];
        }
    }

    private void BuildLegend()
    {
        _legend.Children.Clear();
        _legend.IsVisible = ShowLegend;
        if (!ShowLegend) { return; }
        var markerTemplate = (IDataTemplate)this.FindResource("MemoryCoverageCompactMarkerTemplate")!;
        // Retain each physical range and its typed identity, including disconnected ranges
        // with the same source. Technical traces stay in the terminal card, not this legend.
        foreach (MemoryCoverageSegmentViewModel slice in (ItemsSource?.Cast<MemoryCoverageSegmentViewModel>() ?? [])
            .Where(static slice => slice.IsPrimaryContent)
            .OrderBy(static slice => slice.ContentRole == MemoryContentRole.Unmapped)
            .ThenBy(static slice => slice.RangeStart))
        {
            var target = new Border
            {
                Name = "MemoryLegendTarget",
                DataContext = slice,
                FocusAdorner = null,
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 3),
                Margin = new Thickness(0, 0, 8, 2),
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new ContentControl { Content = slice, ContentTemplate = markerTemplate, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = slice.DisplayTitle, Classes = { "bodyText" }, MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis });
            target.Child = row;
            AutomationProperties.SetName(target, slice.AccessibleDetail);
            MemoryCoverageInteractionBehavior.SetIsEnabled(target, true);
            WireSlice(target, slice, local: false);
            _legend.Children.Add(target);
        }
        _footer.InvalidateMeasure();
    }

    // The endpoint row always measures at the full address scale. Only the legend wraps
    // when the unused tail cannot fit it; endpoint positions are never compressed.
    private sealed class MemoryFooterPanel : Panel
    {
        internal double OccupiedFraction { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (Control child in Children) { child.Measure(availableSize); }
            Size positions = Children[0].DesiredSize;
            Size legend = Children[1].DesiredSize;
            bool shared = Fits(availableSize.Width, legend.Width);
            return new Size(Math.Max(positions.Width, legend.Width), shared ? Math.Max(positions.Height, legend.Height) : positions.Height + legend.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Size positions = Children[0].DesiredSize;
            Size legend = Children[1].DesiredSize;
            bool shared = Fits(finalSize.Width, legend.Width);
            Children[0].Arrange(new Rect(0, 0, finalSize.Width, positions.Height));
            double left = Math.Max(0, finalSize.Width - legend.Width);
            Children[1].Arrange(new Rect(left, shared ? 0 : positions.Height, Math.Min(finalSize.Width, legend.Width), legend.Height));
            return finalSize;
        }

        private bool Fits(double width, double legendWidth)
        {
            return legendWidth == 0 || OccupiedFraction == 0 || legendWidth + 16 <= width * (1 - OccupiedFraction);
        }
    }
}
