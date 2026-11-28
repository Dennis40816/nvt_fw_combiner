// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using Path = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Visual construction only; colors and patterns reuse the existing memory templates.</summary>
internal static class OutputLayoutVisuals
{
    internal static void Resource(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }

    internal static StackPanel Stack(string spacing = "NfcSpace8")
    {
        var panel = new StackPanel();
        Resource(panel, StackPanel.SpacingProperty, spacing);
        return panel;
    }

    internal static Grid Grid(string columns)
    {
        var grid = new Grid { ColumnDefinitions = new(columns) };
        Resource(grid, global::Avalonia.Controls.Grid.ColumnSpacingProperty, "NfcSpace8");
        return grid;
    }

    internal static TextBlock Text(string? value, string role = "bodyText")
    {
        return new() { Text = value, Classes = { role }, TextWrapping = TextWrapping.Wrap };
    }

    internal static Border Pin(string number)
    {
        TextBlock text = Text(number, "captionText");
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.NoWrap;
        return new() { Classes = { "outputLayoutPin" }, Child = text };
    }

    internal static Path Chevron()
    {
        return new()
        {
            Classes = { "outputLayoutChevron" },
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Data = Geometry.Parse("M 6,3 L 11,8 L 6,13"),
        };
    }

    internal static Control Fill(OutputLayoutPanel owner, MemoryCoverageSegmentViewModel segment)
    {
        if (OutputLayoutPanelRow.GroupFor(segment) == OutputLayoutGroupKind.NotWritten)
        {
            return new Border
            {
                Classes = { "memoryCoverageFill" },
                DataContext = segment,
                IsHitTestVisible = false,
            };
        }

        var template = (IDataTemplate)owner.FindResource("MemoryCoverageSegmentBarTemplate")!;
        Control fill = template.Build(segment)!;
        fill.DataContext = segment;
        fill.IsHitTestVisible = false;
        MemoryCoverageInteractionBehavior.SetIsEnabled(fill, false);
        _ = fill.Classes.Remove("memoryCoverageBarSegment");
        ToolTip.SetTip(fill, null);
        return fill;
    }

    internal static Control Swatch(OutputLayoutPanel owner, MemoryCoverageSegmentViewModel segment)
    {
        var parts = new ProportionalStackPanel { IsHitTestVisible = false };
        foreach (MemoryCoverageSegmentViewModel part in segment.DisplayParts.Count > 0 ? segment.DisplayParts : [segment])
        {
            Control fill = Fill(owner, part);
            fill.Height = double.NaN;
            ProportionalStackPanel.SetWeight(fill, part.BarWidth);
            parts.Children.Add(fill);
        }
        return new Border
        {
            Width = 10,
            Height = 10,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Child = parts,
        };
    }

    internal static Control Indent(Control child)
    {
        Grid grid = Grid("Auto,*");
        var spacer = new Border();
        Resource(spacer, Layoutable.WidthProperty, "NfcSpace24");
        grid.Children.Add(spacer);
        global::Avalonia.Controls.Grid.SetColumn(child, 1);
        grid.Children.Add(child);
        return grid;
    }

    internal static Control Inset(Control child, string spacing)
    {
        var grid = new Grid { ColumnDefinitions = new("Auto,*,Auto"), RowDefinitions = new("Auto,Auto,Auto") };
        var first = new Border();
        var last = new Border();
        Resource(first, Layoutable.WidthProperty, spacing);
        Resource(first, Layoutable.HeightProperty, spacing);
        Resource(last, Layoutable.WidthProperty, spacing);
        Resource(last, Layoutable.HeightProperty, spacing);
        global::Avalonia.Controls.Grid.SetColumn(last, 2);
        global::Avalonia.Controls.Grid.SetRow(last, 2);
        global::Avalonia.Controls.Grid.SetColumn(child, 1);
        global::Avalonia.Controls.Grid.SetRow(child, 1);
        grid.Children.Add(first);
        grid.Children.Add(child);
        grid.Children.Add(last);
        return grid;
    }

    internal static Grid Fact(string label, string? value)
    {
        Grid grid = Grid("94,*");
        grid.Children.Add(Text(label, "captionText"));
        TextBlock text = Text(value, "technicalValue");
        global::Avalonia.Controls.Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    internal static void Name(Control target, string name, string id)
    {
        AutomationProperties.SetName(target, name);
        AutomationProperties.SetAutomationId(target, id);
    }
}
