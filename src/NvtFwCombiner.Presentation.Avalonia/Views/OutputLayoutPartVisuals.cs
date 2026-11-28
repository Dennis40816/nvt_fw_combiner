// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Small visual construction helpers shared only by the standalone layout parts.</summary>
internal static class OutputLayoutPartVisuals
{
    internal static TextBlock Text(string text, string style = "layoutPartCaption")
    {
        return new TextBlock { Text = text, Classes = { style } };
    }

    internal static void Resource(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        _ = target.Bind(property, new DynamicResourceExtension(key));
    }

    internal static StackPanel Stack(string spacing = "NfcSpace4")
    {
        var panel = new StackPanel();
        Resource(panel, StackPanel.SpacingProperty, spacing);
        return panel;
    }

    internal static void Identify(Control control, string id, string name)
    {
        AutomationProperties.SetAutomationId(control, id);
        AutomationProperties.SetName(control, name);
    }
}
