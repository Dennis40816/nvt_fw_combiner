using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class MemoryCoverageBar
{
    /// <summary>Optional localized heading; supplied by the existing workflow presentation.</summary>
    public static readonly StyledProperty<string?> HeadingProperty = AvaloniaProperty.Register<MemoryCoverageBar, string?>(nameof(Heading));
    /// <summary>Already formatted capacity displayed beside the heading.</summary>
    public static readonly StyledProperty<string?> CapacityLabelProperty = AvaloniaProperty.Register<MemoryCoverageBar, string?>(nameof(CapacityLabel));
    /// <summary>Already formatted outer start address; this control does not derive ranges.</summary>
    public static readonly StyledProperty<string?> StartAddressProperty = AvaloniaProperty.Register<MemoryCoverageBar, string?>(nameof(StartAddress));
    /// <summary>Already formatted outer end address.</summary>
    public static readonly StyledProperty<string?> EndAddressProperty = AvaloniaProperty.Register<MemoryCoverageBar, string?>(nameof(EndAddress));

    /// <inheritdoc cref="HeadingProperty" />
    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    /// <inheritdoc cref="CapacityLabelProperty" />
    public string? CapacityLabel { get => GetValue(CapacityLabelProperty); set => SetValue(CapacityLabelProperty, value); }
    /// <inheritdoc cref="StartAddressProperty" />
    public string? StartAddress { get => GetValue(StartAddressProperty); set => SetValue(StartAddressProperty, value); }
    /// <inheritdoc cref="EndAddressProperty" />
    public string? EndAddress { get => GetValue(EndAddressProperty); set => SetValue(EndAddressProperty, value); }

    private readonly Border _footer = new();
    private readonly StackPanel _header = new() { Name = "MemoryOverviewHeader", Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _legend = new() { Name = "MemoryLegend", Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _heading = new() { Name = "MemoryOverviewHeading", Classes = { "bodyEmphasisText" }, FontSize = 14, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _capacity = new() { Classes = { "captionText" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _addresses = new() { Name = "MemoryOverviewAddresses", ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _startAddress = new() { Classes = { "monoText", "captionText" }, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _endAddress = new() { Classes = { "monoText", "captionText" } };
    private double _arrangedRailWidth;

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Child arrangement runs before this control's Bounds is updated.
        _arrangedRailWidth = finalSize.Width;
        UpdateTinyMarkers(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    private void InitializeOverview()
    {
        _header.Children.Add(new WrapPanel { Name = "MemoryOverviewTitle", Children = { _heading, _capacity } });
        _heading.Margin = new Thickness(0, 0, 8, 0);
        _addresses.Children.Add(_startAddress);
        Grid.SetColumn(_endAddress, 1);
        _addresses.Children.Add(_endAddress);
    }

    // Text is not a byte range: preserve its natural width and clamp it to the rail.
    // The hover endpoint keeps its proportional geometry; only the drawn stroke of a short
    // range is widened to the tiny-marker size, centred on its range inside its own slot.
    private sealed class MemoryEndpointPanel : Panel
    {
        private const double MinimumStrokeWidth = MinimumMarkerSize;
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
            double start = _owner._arrangedRailWidth * _startFraction;
            double width = Math.Min(Children[1].DesiredSize.Width, _owner._arrangedRailWidth);
            double left = GetLabelLeft();
            (double strokeLeft, double strokeWidth) = GetStroke();
            Children[0].Arrange(new Rect(strokeLeft - start, 0, strokeWidth, 2));
            Children[1].Arrange(new Rect(left - start, 4, width, Math.Max(0, finalSize.Height - 4)));
            return finalSize;
        }

        private (double Left, double Width) GetStroke()
        {
            double rail = _owner._arrangedRailWidth;
            double start = rail * _startFraction;
            double width = rail * _widthFraction;
            if (width >= MinimumStrokeWidth) { return (start, width); }
            MemoryEndpointPanel[] peers = [.. _owner._positions.GetVisualDescendants().OfType<MemoryEndpointPanel>()
                .OrderBy(static peer => peer._startFraction)];
            int index = Array.IndexOf(peers, this);
            // Each stroke stays inside the midpoint gap to its neighbours, so widened strokes never overlap.
            double slotLeft = index > 0 ? ((rail * (peers[index - 1]._startFraction + peers[index - 1]._widthFraction)) + start) / 2 : 0;
            double slotRight = index >= 0 && index < peers.Length - 1
                ? (start + width + (rail * peers[index + 1]._startFraction)) / 2
                : rail;
            double drawn = Math.Min(MinimumStrokeWidth, slotRight - slotLeft - 2);
            if (drawn <= width) { return (start, width); }
            return (Math.Clamp(start + (width / 2) - (drawn / 2), slotLeft + 1, slotRight - 1 - drawn), drawn);
        }

        private double GetLabelLeft()
        {
            MemoryEndpointPanel[] peers = [.. _owner._positions.GetVisualDescendants().OfType<MemoryEndpointPanel>()
                .OrderBy(static peer => peer._startFraction)];
            double[] widths = [.. peers.Select(peer => Math.Min(peer.Children[1].DesiredSize.Width, _owner._arrangedRailWidth))];
            double[] lefts = new double[peers.Length];
            double cursor = 0;
            for (int index = 0; index < peers.Length; index++)
            {
                double center = _owner._arrangedRailWidth * (peers[index]._startFraction + (peers[index]._widthFraction / 2));
                lefts[index] = Math.Max(cursor, center - (widths[index] / 2));
                cursor = lefts[index] + widths[index] + 6;
            }
            // Pack back from the right edge when a short end range cannot center its label.
            cursor = _owner._arrangedRailWidth;
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
        _heading.Text = Heading;
        _heading.IsVisible = !string.IsNullOrEmpty(Heading);
        _capacity.Text = CapacityLabel;
        _capacity.IsVisible = !string.IsNullOrEmpty(CapacityLabel);
        _header.IsVisible = ShowLegend;
        _startAddress.Text = StartAddress;
        _endAddress.Text = EndAddress;
        _addresses.IsVisible = !string.IsNullOrEmpty(StartAddress) || !string.IsNullOrEmpty(EndAddress);
        _legend.IsVisible = ShowLegend;
        if (!ShowLegend) { return; }
        var markerTemplate = (IDataTemplate)this.FindResource("MemoryCoverageCompactMarkerTemplate")!;
        // The legend and rail share the same content runs in physical address order.
        foreach (MemoryCoverageSegmentViewModel slice in _displaySegments.Where(static slice => slice.IsPrimaryContent))
        {
            var target = new Border
            {
                Name = "MemoryLegendTarget",
                DataContext = slice,
                IsHitTestVisible = false,
                Classes = { "memoryPassiveLegend" },
                Padding = new Thickness(0, 3),
                Margin = new Thickness(0, 0, 0, 2),
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            // No shared size group: Avalonia 12.1 applies groups set on code-built columns, which 12.0.5 ignored.
            // Each address column fits its own range, as on 12.0.5.
            Control marker = new ContentControl { Content = slice, ContentTemplate = markerTemplate, VerticalAlignment = VerticalAlignment.Center };
            if (slice.DisplayParts.Any(part => part.FillRole != slice.FillRole || part.UsesKeptPattern != slice.UsesKeptPattern))
            {
                var parts = new ProportionalStackPanel { IsHitTestVisible = false };
                foreach (MemoryCoverageSegmentViewModel part in slice.DisplayParts)
                {
                    Control fill = markerTemplate.Build(part)!;
                    fill.DataContext = part;
                    fill.Width = double.NaN;
                    ProportionalStackPanel.SetWeight(fill, part.BarWidth);
                    parts.Children.Add(fill);
                }
                marker = new Border
                {
                    Name = "MemoryMixedLegendMarker",
                    Width = 10,
                    Height = 10,
                    CornerRadius = new CornerRadius(2),
                    ClipToBounds = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = parts,
                };
            }
            row.Children.Add(marker);
            var title = new TextBlock { Text = slice.DisplayTitle, Classes = { "bodyText" }, Margin = new Thickness(6, 0, 12, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(title, 1);
            row.Children.Add(title);
            var address = new TextBlock { Text = slice.AddressRangeLabel, Classes = { "monoText", "bodyText" }, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(address, 2);
            row.Children.Add(address);
            target.Child = row;
            AutomationProperties.SetName(target, slice.AccessibleDetail);
            _ = target.Bind(MemoryCoverageInteractionBehavior.RailActiveProperty,
                new Binding("Interaction.IsRailActive") { Source = slice });
            _legend.Children.Add(target);
        }
        _header.InvalidateMeasure();
    }

    // Text, not its column, is what a decorative stem must not cross.
    private IEnumerable<Control> OverviewObstacles()
    {
        foreach (TextBlock text in this.GetVisualDescendants().OfType<TextBlock>().Where(static block => block.IsEffectivelyVisible)) { yield return text; }
        foreach (Control marker in LegendMarkers()) { yield return marker; }
    }

    private IEnumerable<Control> LegendMarkers()
    {
        return _legend.Children.OfType<Border>().Select(static row => row.Child).OfType<Grid>()
            .Select(static row => row.Children[0]).Where(static marker => marker.IsEffectivelyVisible);
    }

    private static Rect[] ObstacleRects(IEnumerable<Control> controls, Visual reference, double offsetX, double offsetY)
    {
        return [.. controls.Select(control =>
        {
            Point point = control.TranslatePoint(default, reference) ?? default;
            return new Rect(point.X + offsetX, point.Y + offsetY, control.Bounds.Width, control.Bounds.Height);
        })];
    }

}
