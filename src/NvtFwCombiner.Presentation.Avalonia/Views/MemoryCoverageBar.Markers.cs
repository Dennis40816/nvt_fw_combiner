using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class MemoryCoverageBar
{
    private const double MinimumMarkerSize = 24;
    private readonly Canvas _markers = new() { Name = "MemoryTinyMarkers", Height = MinimumMarkerSize, Margin = new Thickness(0, 4, 0, 0), IsVisible = false };
    private double _markerWidth = double.NaN;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateTinyMarkers(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private void UpdateRailHeight()
    {
        Height = ShowLegend ? double.NaN : 34 + (_positions.IsVisible ? 28 : 0) + (_markers.IsVisible ? 28 : 0);
    }

    private void UpdateTinyMarkers(double width)
    {
        if (!double.IsFinite(width) || width < MinimumMarkerSize || _markerWidth == width) { return; }
        _markerWidth = width;
        _markers.Children.Clear();
        double total = _displaySegments.Sum(static slice => slice.BarWidth);
        double cursor = 0;
        var clusters = new List<(double Left, double LastLeft, List<MemoryCoverageSegmentViewModel> Slices)>();
        foreach (MemoryCoverageSegmentViewModel slice in _displaySegments)
        {
            double size = total > 0 ? width * slice.BarWidth / total : 0;
            double left = Math.Clamp(cursor + (size / 2) - (MinimumMarkerSize / 2), 0, width - MinimumMarkerSize);
            cursor += size;
            if (!slice.IsPrimaryContent || size <= 0 || size >= MinimumMarkerSize) { continue; }
            // Overlapping targets share a list, never a fabricated firmware range.
            if (clusters.Count > 0 && left < clusters[^1].LastLeft + MinimumMarkerSize + 2)
            {
                (double firstLeft, _, List<MemoryCoverageSegmentViewModel> members) = clusters[^1];
                members.Add(slice);
                clusters[^1] = (firstLeft, left, members);
            }
            else { clusters.Add((left, left, [slice])); }
        }
        foreach ((double left, double lastLeft, List<MemoryCoverageSegmentViewModel> slices) in clusters)
        {
            var item = new MemoryCoverageBarItem(slices.AsReadOnly());
            var target = new Border
            {
                Name = "MemoryTinyMarker",
                DataContext = item.IsGroup ? item : item.Slices[0],
                Width = MinimumMarkerSize,
                Height = MinimumMarkerSize,
                Focusable = true,
                FocusAdorner = null,
                Classes = { "memoryTinyMarker" },
                Child = new TextBlock
                {
                    Text = item.IsGroup ? FormattableString.Invariant($"{item.Slices.Count}") : "◆",
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Canvas.SetLeft(target, (left + lastLeft) / 2);
            if (item.IsGroup)
            {
                WireGroup(target, () => OpenLocal(item, target, collisionList: true));
                AutomationProperties.SetName(target, string.Join("; ", item.Slices.Select(static slice => slice.AccessibleDetail)));
                AutomationProperties.SetHelpText(target, Text.MemoryLocalViewHint);
            }
            else
            {
                WireSlice(target, item.Slices[0], local: false);
                AutomationProperties.SetName(target, item.Slices[0].AccessibleDetail);
            }
            _markers.Children.Add(target);
        }
        _markers.IsVisible = _markers.Children.Count > 0;
        UpdateRailHeight();
    }

    private void WireGroup(Control target, Action open)
    {
        WirePointerTarget(target, open);
        target.GotFocus += (_, e) => { if (CanExploreFromFocus(e)) { open(); } };
        target.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up)
            {
                open();
                _ = _sliceTargets.FirstOrDefault()?.Focus(NavigationMethod.Directional);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) { CloseAll(); e.Handled = true; }
        };
    }

    private Border CollisionEntry(MemoryCoverageSegmentViewModel slice)
    {
        var labels = new StackPanel { Spacing = 2 };
        labels.Children.Add(new TextBlock { Text = slice.DisplayTitle, Classes = { "bodyEmphasisText" }, TextWrapping = TextWrapping.Wrap });
        labels.Children.Add(new TextBlock { Text = $"{slice.AddressSpaceId} · {slice.AddressRangeLabel}", Classes = { "monoText", "captionText" }, TextWrapping = TextWrapping.Wrap });
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        row.Children.Add(new ContentControl
        {
            Content = slice,
            ContentTemplate = (IDataTemplate)this.FindResource("MemoryCoverageCompactMarkerTemplate")!,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        Grid.SetColumn(labels, 1);
        row.Children.Add(labels);
        var target = new Border
        {
            DataContext = slice,
            MinHeight = MinimumMarkerSize,
            Padding = new Thickness(6),
            FocusAdorner = null,
            Classes = { "memoryCollisionEntry" },
            Child = row,
        };
        AutomationProperties.SetName(target, slice.AccessibleDetail);
        return target;
    }
}
