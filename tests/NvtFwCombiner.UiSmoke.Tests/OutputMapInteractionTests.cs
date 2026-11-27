// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real pointer and keyboard input drives the same bindable preview and pin state.</summary>
public sealed partial class OutputMapInteractionTests
{
    /// <summary>Hover is temporary; click survives exit and Escape clears the pin.</summary>
    [AvaloniaFact]
    public void HoverPreviewsClickPinsAndEscapeClears()
    {
        using var host = new OutputMapTestHost();
        Point point = host.Bounds(host.Block("tp")).Center;
        host.Window.MouseMove(point, RawInputModifiers.None);
        host.Layout();
        Assert.Equal("tp", host.Map.HoveredKey);
        Assert.Null(host.Map.SelectedKey);
        Assert.True(host.Find<Canvas>("OutputMap.SourceStrip").IsEffectivelyVisible);
        host.Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
        Assert.Null(host.Map.HoveredKey);
        Assert.Contains(host.Map.GetVisualDescendants().OfType<TextBlock>(), t => t.Text ==
            "Hover or click a block, a region or a step row to zoom in.");
        host.Window.MouseMove(point, RawInputModifiers.None);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        host.Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
        host.Layout();
        Assert.Equal("tp", host.Map.SelectedKey);
        Assert.True(host.Find<Canvas>("OutputMap.ZoomBar").IsEffectivelyVisible);
        host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(host.Map.SelectedKey);
        Assert.Null(host.Map.HoveredKey);
    }

    /// <summary>Every main block, region and jump button is reachable with real Tab traversal.</summary>
    [AvaloniaFact]
    public void TabReachesEveryTargetAndArrowsPreviewTheNextTarget()
    {
        using var host = new OutputMapTestHost();
        Button first = host.Region("tp-region");
        Assert.True(first.Focus());
        var visited = new HashSet<string>(StringComparer.Ordinal);
        WalkTargets(host, visited, 12);
        Assert.Equal(12, visited.Count);
        Assert.Contains("OutputMap.Block.patch", visited);
        Assert.Contains("OutputMap.Region.customer", visited);
        Assert.Contains("OutputMap.ZoomTo.dp-region", visited);
        Assert.True(first.Focus());
        host.Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Assert.True(host.Region("reserved").IsFocused);
        Assert.Equal("reserved", host.Map.HoveredKey);
    }

    /// <summary>Native Button keyboard activation pins the same keys as pointer input.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter, PhysicalKey.Enter, null)]
    [InlineData(Key.Space, PhysicalKey.Space, " ")]
    public void KeyboardActivationPinsAndEscapeClears(Key key, PhysicalKey physicalKey, string? symbol)
    {
        using var host = new OutputMapTestHost();
        Assert.True(host.Block("fill").Focus(NavigationMethod.Tab));
        host.Layout();
        Assert.Equal(new Thickness(2), host.Block("fill").BorderThickness);
        host.Window.KeyPress(key, RawInputModifiers.None, physicalKey, symbol);
        host.Window.KeyRelease(key, RawInputModifiers.None, physicalKey, symbol);
        Assert.Equal("fill", host.Map.SelectedKey);
        host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(host.Map.SelectedKey);
    }

    /// <summary>Zoom targets do not preview on focus, and keep keyboard focus when pinning redraws their strip.</summary>
    [AvaloniaFact]
    public void ZoomKeyboardPreviewRetainsFocusAndActivates()
    {
        using var host = new OutputMapTestHost();
        host.Map.SelectedKey = "tp";
        host.Layout();
        Assert.True(host.Find<Button>("OutputMap.ZoomBlock.fill").Focus(NavigationMethod.Tab));
        host.Layout();
        Assert.True(host.Find<Button>("OutputMap.ZoomBlock.fill").IsFocused);
        Assert.Null(host.Map.HoveredKey);
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        host.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.Equal("fill", host.Map.SelectedKey);
        Assert.True(host.Find<Button>("OutputMap.ZoomBlock.fill").IsFocused);
    }

    /// <summary>Touch pins on contact instead of requiring a separate hover preview.</summary>
    [AvaloniaFact]
    public void TouchContactPinsImmediately()
    {
        using var host = new OutputMapTestHost();
        Button button = host.Block("dp");
        using var pointer = new Pointer(42, PointerType.Touch, true);
        var press = new PointerPressedEventArgs(button, pointer, host.Window, host.Bounds(button).Center, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1);
        button.RaiseEvent(press);
        Assert.Equal("dp", host.Map.SelectedKey);
    }

    /// <summary>The outside table can preview another target without discarding the pinned target.</summary>
    [AvaloniaFact]
    public void ExternalKeysPreviewThenReturnToThePinnedRange()
    {
        using var host = new OutputMapTestHost();
        host.Map.SelectedKey = "tp";
        host.Map.HoveredKey = "dp";
        host.Layout();
        Assert.Contains("DP table", AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
        host.Map.HoveredKey = null;
        host.Layout();
        Assert.Equal("tp", host.Map.SelectedKey);
        Assert.Contains("TP code", AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
    }

    /// <summary>The default two-way bindings exchange state with an external step table.</summary>
    [AvaloniaFact]
    public void BoundKeysExchangeExternalSelectionAndPointerState()
    {
        using var host = new OutputMapTestHost();
        var selection = new SelectionState();
        _ = host.Map.Bind(OutputMap.SelectedKeyProperty,
            new Binding(nameof(SelectionState.SelectedKey)) { Source = selection });
        _ = host.Map.Bind(OutputMap.HoveredKeyProperty,
            new Binding(nameof(SelectionState.HoveredKey)) { Source = selection });
        selection.SelectedKey = "dp";
        host.Layout();
        Assert.Equal("dp", host.Map.SelectedKey);
        Point point = host.Bounds(host.Block("tp")).Center;
        host.Window.MouseMove(point, RawInputModifiers.None);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        host.Layout();
        Assert.Equal("tp", selection.SelectedKey);
        Assert.Equal("tp", selection.HoveredKey);
    }

    /// <summary>Automation labels retain hidden region names, exact ranges, access and source reads.</summary>
    [AvaloniaFact]
    public void AutomationNamesDescribeTheBarRegionsBlocksButtonsAndZoom()
    {
        using var host = new OutputMapTestHost();
        Assert.Equal("Output map · output [0x00000, 0x40000)", AutomationProperties.GetName(host.Bar));
        Assert.Equal(AutomationProperties.GetName(host.Bar), ControlAutomationPeer.CreatePeerForElement(host.Bar).GetName());
        Button customer = host.Region("customer");
        Assert.Equal("Customer info · output [0x3D000, 0x3E000) · read-only", AutomationProperties.GetName(customer));
        Assert.Equal(AutomationProperties.GetName(customer), ToolTip.GetTip(customer));
        Assert.Equal(AutomationProperties.GetName(customer), ControlAutomationPeer.CreatePeerForElement(customer).GetName());
        Assert.Contains("TP code · Copy · output [0x00000, 0x3C000)", AutomationProperties.GetName(host.Block("tp")), StringComparison.Ordinal);
        Assert.Contains("Customer info", AutomationProperties.GetName(host.Find<Button>("OutputMap.ZoomTo.customer")), StringComparison.Ordinal);
        host.Map.SelectedKey = "dp";
        host.Layout();
        Assert.Equal("dp_table.bin · read [0x00000, 0x02000)", AutomationProperties.GetName(host.Find<Canvas>("OutputMap.SourceStrip")));
        Assert.Contains("DP table", AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")), StringComparison.Ordinal);
        Assert.Equal(AutomationProperties.GetName(host.Find<Border>("OutputMap.ZoomStrip")),
            ControlAutomationPeer.CreatePeerForElement(host.Find<Border>("OutputMap.ZoomStrip")).GetName());
    }

    /// <summary>Language comes from the shell resource bundle and motion can change without losing the pin.</summary>
    [AvaloniaFact]
    public void ChineseLabelsAndReducedMotionRetainSelection()
    {
        using var host = new OutputMapTestHost(chinese: true);
        host.Map.SelectedKey = "customer";
        host.Map.ReducedMotion = false;
        host.Layout();
        Assert.All(host.Map.GetVisualDescendants().OfType<Button>(), b => Assert.NotEmpty(b.Transitions!));
        host.Map.ReducedMotion = true;
        host.Layout();
        Assert.Equal("輸出地圖", host.Map.FindControl<TextBlock>("MapTitle")!.Text);
        Assert.Contains("唯讀", AutomationProperties.GetName(host.Region("customer")), StringComparison.Ordinal);
        Assert.All(host.Map.GetVisualDescendants().OfType<Button>(), b => Assert.Empty(b.Transitions!));
        Assert.Equal("customer", host.Map.SelectedKey);
    }

    private static void WalkTargets(OutputMapTestHost host, HashSet<string> visited, int count)
    {
        for (int index = 0; index < count; index++)
        {
            Button focused = Assert.Single(host.Map.GetVisualDescendants().OfType<Button>(), b => b.IsFocused);
            Assert.True(focused.IsTabStop);
            Assert.True(visited.Add(AutomationProperties.GetAutomationId(focused)!));
            host.Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        }
    }

    // UI thread only, like the external step-table state this fixture represents.
    private sealed partial class SelectionState : ObservableObject
    {
        [ObservableProperty]
        public partial string? SelectedKey { get; set; }

        [ObservableProperty]
        public partial string? HoveredKey { get; set; }
    }
}
