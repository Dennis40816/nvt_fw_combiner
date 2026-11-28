// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Pointer, keyboard and publication replacement operate on the actual standalone control.</summary>
public sealed class OutputLayoutPanelInteractionTests
{
    /// <summary>Hover links the matching bar, pin and row without opening details or a popup.</summary>
    [AvaloniaFact]
    public void HoverHighlightsWithoutOpeningAndClickExpandsInPlace()
    {
        using var host = new OutputLayoutPanelTestHost();
        Border block = Assert.IsType<Border>(host.Bar.Children[0]);
        host.Window.MouseMove(host.Bounds(block).Center, RawInputModifiers.None);
        host.Layout();
        Assert.Contains("active", block.Classes);
        Assert.Contains("active", Assert.IsType<Border>(host.Pins.Children[0]).Classes);
        Assert.Contains(host.Row("1").GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("outputLayoutRow") && border.Classes.Contains("active"));
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.All(host.Panel.GetVisualDescendants().OfType<Control>(), static control => Assert.Null(ToolTip.GetTip(control)));
        host.Click(host.Row("1"));
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Technical("1")).IsVisible);
        host.Click(host.Technical("1"));
        Assert.True(OutputLayoutPanelTestHost.Body(host.Technical("1")).IsVisible);
        Assert.True(host.Bounds(host.Technical("1")).Top >= host.Bounds(host.Row("1")).Bottom);
    }

    /// <summary>Enter and Space toggle the same row command and retain keyboard focus.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter, PhysicalKey.Enter, null)]
    [InlineData(Key.Space, PhysicalKey.Space, " ")]
    public void KeyboardActivationExpandsAndCollapsesTheFocusedRow(Key key, PhysicalKey physicalKey, string? symbol)
    {
        using var host = new OutputLayoutPanelTestHost();
        Assert.True(host.Row("1").Focus(NavigationMethod.Tab));
        host.Key(key, physicalKey, symbol);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.True(host.Row("1").IsFocused);
        host.Key(key, physicalKey, symbol);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
    }

    /// <summary>Enter and Space on a pin or a bar block open its row once and leave it open.</summary>
    [AvaloniaTheory]
    [InlineData(true, Key.Enter, PhysicalKey.Enter, null)]
    [InlineData(true, Key.Space, PhysicalKey.Space, " ")]
    [InlineData(false, Key.Enter, PhysicalKey.Enter, null)]
    [InlineData(false, Key.Space, PhysicalKey.Space, " ")]
    public void MapTargetKeyboardActivationOpensItsRow(bool pin, Key key, PhysicalKey physicalKey, string? symbol)
    {
        using var host = new OutputLayoutPanelTestHost();
        Control target = pin ? host.Pins.Children[0] : host.Bar.Children[0];
        Assert.True(target.Focus(NavigationMethod.Tab));
        host.Key(key, physicalKey, symbol);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.True(host.Row("1").IsFocused);
    }

    /// <summary>Clicking a pin or a bar block opens its row.</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapTargetClickOpensItsRow(bool pin)
    {
        using var host = new OutputLayoutPanelTestHost();
        host.Click(pin ? host.Pins.Children[0] : host.Bar.Children[0]);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.True(host.Row("1").IsFocused);
    }

    /// <summary>Collapsing a row from a control inside it returns focus to the row.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Escape, PhysicalKey.Escape)]
    [InlineData(Key.Left, PhysicalKey.ArrowLeft)]
    public void CollapsingFromInsideARowFocusesTheRow(Key key, PhysicalKey physicalKey)
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 100, "DP", severity: MemoryDiagnosticSeverity.Warning, slot: "dp")], warningCount: 1, canRequestIssue: true));
        host.Click(host.Row("1"));
        Assert.True(host.Find<Button>("OutputLayout.ShowIssue").Focus(NavigationMethod.Tab));
        host.Key(key, physicalKey);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.True(host.Row("1").IsFocused);
    }

    /// <summary>Right/Left disclose, arrows move through visible headers, and Home/End reach list boundaries.</summary>
    [AvaloniaFact]
    public void ArrowAndBoundaryKeysNavigateVisibleDisclosures()
    {
        using var host = new OutputLayoutPanelTestHost();
        Assert.True(host.Row("1").Focus(NavigationMethod.Tab));
        host.Key(Key.Right, PhysicalKey.ArrowRight);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        host.Key(Key.Down, PhysicalKey.ArrowDown);
        Assert.True(host.Technical("1").IsFocused);
        host.Key(Key.Up, PhysicalKey.ArrowUp);
        Assert.True(host.Row("1").IsFocused);
        host.Key(Key.Left, PhysicalKey.ArrowLeft);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        host.Key(Key.End, PhysicalKey.End);
        Assert.True(host.Group(OutputLayoutGroupKind.NotWritten).IsFocused);
        host.Key(Key.Home, PhysicalKey.Home);
        Assert.True(host.Group(OutputLayoutGroupKind.Write).IsFocused);
    }

    /// <summary>Escape is consumed only when it collapses the current row or technical disclosure.</summary>
    [AvaloniaFact]
    public void EscapeBubblesWhenItDidNotCollapseAnything()
    {
        using var host = new OutputLayoutPanelTestHost();
        host.Click(host.Row("1"));
        Assert.True(host.Row("1").Focus());
        var first = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        host.Row("1").RaiseEvent(first);
        Assert.True(first.Handled);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        var second = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        host.Row("1").RaiseEvent(second);
        Assert.False(second.Handled);
    }

    /// <summary>Tab traverses pins, blocks and list headers in visual order with automation names.</summary>
    [AvaloniaFact]
    public void TabReachesPinsBlocksAndRows()
    {
        using var host = new OutputLayoutPanelTestHost();
        Assert.True(host.Pins.Children[0].Focus(NavigationMethod.Tab));
        host.Key(Key.Tab, PhysicalKey.Tab);
        Assert.True(host.Pins.Children[1].IsFocused);
        host.Key(Key.Tab, PhysicalKey.Tab);
        Assert.True(host.Pins.Children[2].IsFocused);
        host.Key(Key.Tab, PhysicalKey.Tab);
        Assert.True(host.Bar.Children[0].IsFocused);
        Assert.NotEmpty(AutomationProperties.GetName(host.Bar.Children[0]) ?? string.Empty);
        Assert.NotEmpty(AutomationProperties.GetName(host.Pins.Children[0]) ?? string.Empty);
        Assert.Contains("DP BIN", AutomationProperties.GetName(host.Row("1")), StringComparison.Ordinal);
    }

    /// <summary>Identity survives localized titles and changed observations, retaining both disclosures and actual focus.</summary>
    [AvaloniaFact]
    public void ReplacementRetainsExpansionAndFocusForExistingIdentity()
    {
        using var host = new OutputLayoutPanelTestHost();
        host.Click(host.Row("1"));
        host.Click(host.Technical("1"));
        Assert.True(host.Technical("1").Focus(NavigationMethod.Tab));
        host.Panel.Model = new([
            OutputLayoutPanelTestHost.Slice(0, 0x6000, "Renamed input", disposition: MemoryWorkflowDisposition.Kept,
                observed: MemoryObservedChange.Changed, slot: "dp"),
            OutputLayoutPanelTestHost.Slice(0x7000, 0x40000, "TP BIN", slot: "tp")]);
        host.Layout();
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Technical("1")).IsVisible);
        Assert.True(host.Technical("1").IsFocused);
    }

    /// <summary>Removed identities discard expansion and focus, including when a later publication adds the range again.</summary>
    [AvaloniaFact]
    public void RemovedIdentityDropsItsExpansionAndFocus()
    {
        using var host = new OutputLayoutPanelTestHost();
        host.Click(host.Row("1"));
        Button removed = host.Row("1");
        Assert.True(removed.Focus());
        host.Panel.Model = new([OutputLayoutPanelTestHost.Slice(0x7000, 0x40000, "TP BIN", slot: "tp")]);
        host.Layout();
        Assert.False(removed.IsFocused);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        host.Panel.Model = OutputLayoutPanelTestHost.Example();
        host.Layout();
        Assert.False(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
    }

    /// <summary>The Issues action forwards the supplied segment identity without classifying its message.</summary>
    [AvaloniaFact]
    public void WarningActionRaisesHostNavigationWithTheOriginalSegment()
    {
        MemoryCoverageSegmentViewModel segment = OutputLayoutPanelTestHost.Slice(0, 100, "DP", severity: MemoryDiagnosticSeverity.Warning, slot: "dp");
        using var host = new OutputLayoutPanelTestHost(new([segment], warningCount: 1, canRequestIssue: true));
        OutputLayoutIssueRequestedEventArgs? received = null;
        host.Panel.IssueRequested += (_, args) => received = args;
        host.Click(host.Row("1"));
        host.Click(host.Find<Button>("OutputLayout.ShowIssue"));
        Assert.NotNull(received);
        Assert.Same(segment, received.Segment);
        Assert.Equal("dp", received.SourceSlotId);
    }

    /// <summary>A removed warning action returns focus to its surviving row rather than discarding the row's focus.</summary>
    [AvaloniaFact]
    public void ClearingAWarningKeepsFocusOnItsSurvivingRow()
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 100, "DP", severity: MemoryDiagnosticSeverity.Warning, slot: "dp")], warningCount: 1, canRequestIssue: true));
        host.Click(host.Row("1"));
        Assert.True(host.Find<Button>("OutputLayout.ShowIssue").Focus(NavigationMethod.Tab));
        host.Panel.Model = new([OutputLayoutPanelTestHost.Slice(0, 100, "DP", slot: "dp")]);
        host.Layout();
        Assert.True(host.Row("1").IsFocused);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
    }

    /// <summary>Changing reduced motion leaves current expansion intact and propagates to generated controls.</summary>
    [AvaloniaFact]
    public void ReducedMotionDoesNotResetDisclosures()
    {
        using var host = new OutputLayoutPanelTestHost();
        host.Click(host.Row("1"));
        host.Panel.ReducedMotion = false;
        host.Panel.ReducedMotion = true;
        host.Layout();
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.Contains("reducedMotion", host.Row("1").Classes);
        Assert.Null(host.Bar.Children[0].Transitions);
    }

    /// <summary>Keyboard focus uses the shared visible-focus theme, and every disclosure uses right/down geometry.</summary>
    [AvaloniaFact]
    public void SharedFocusAndChevronGeometryRemainVisible()
    {
        using var host = new OutputLayoutPanelTestHost();
        Assert.True(host.Row("1").Focus(NavigationMethod.Tab));
        host.Layout();
        ContentPresenter presenter = Assert.Single(host.Row("1").GetVisualDescendants().OfType<ContentPresenter>(),
            static control => control.Name == "PART_ContentPresenter");
        Assert.Equal(new Thickness(2), presenter.BorderThickness);
        Avalonia.Controls.Shapes.Path chevron = Assert.Single(host.Row("1").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.True(chevron.Data!.Bounds.Height > chevron.Data.Bounds.Width);
        host.Key(Key.Right, PhysicalKey.ArrowRight);
        Assert.True(chevron.Data.Bounds.Width > chevron.Data.Bounds.Height);
    }
}
