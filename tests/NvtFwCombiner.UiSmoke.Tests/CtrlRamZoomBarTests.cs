// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Protects the standalone CtrlRAM lane geometry, interaction and absent-data contract.</summary>
public sealed partial class CtrlRamZoomBarTests
{
    /// <summary>Address lengths, rather than legacy bar weights or label widths, determine physical geometry.</summary>
    [AvaloniaFact]
    public void SliceBoundsAreProportionalToThePublishedAddresses()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        Button nf = host.Find<Button>("CtrlRamZoom.Slice.0");
        Button normal = host.Find<Button>("CtrlRamZoom.Slice.1");
        Button vn = host.Find<Button>("CtrlRamZoom.Slice.2");
        Assert.InRange(Math.Abs(nf.Bounds.Width - (host.Rail.Bounds.Width * 0.25)), 0, 1);
        Assert.InRange(Math.Abs(normal.Bounds.Width - (host.Rail.Bounds.Width * 0.5)), 0, 1);
        Assert.InRange(Math.Abs(vn.Bounds.Width - (host.Rail.Bounds.Width * 0.25)), 0, 1);
        Assert.InRange(Math.Abs(nf.Bounds.Right - normal.Bounds.Left), 0, 1);
        Assert.InRange(Math.Abs(normal.Bounds.Right - vn.Bounds.Left), 0, 1);
        Assert.Equal(host.Rail.Bounds.Width, vn.Bounds.Right, precision: 3);
    }

    /// <summary>Physical fragments keep their original fill and kept pattern inside a single logical slice.</summary>
    [AvaloniaFact]
    public void MixedPhysicalPartsRetainTheirExactGeometryAndKeptPattern()
    {
        CtrlRamZoomData source = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane())!;
        CtrlRamZoomSlice slice = source.Slices[0] with
        {
            Fills = [new(0x1000, 0x1080, "NfcMemoryCtrlRamNfFillBrush", true),
                new(0x1080, 0x1100, "NfcMemoryCtrlRamNormalFillBrush", false)],
        };
        using var host = new OutputLayoutPartsTestHost(new(source.Start, source.EndExclusive, source.PositionLabel,
            source.AddressSpace, source.RangeLabel, source.SizeLabel, [slice, source.Slices[1], source.Slices[2]]));
        Canvas strip = Assert.Single(host.Find<Button>("CtrlRamZoom.Slice.0").GetVisualDescendants().OfType<Canvas>());
        Border[] fills = [.. strip.Children.Cast<Border>()];
        Assert.Equal(2, fills.Length);
        Assert.Equal(host.Rail.Bounds.Width * 0.125, fills[0].Bounds.Width, precision: 3);
        Assert.Equal(fills[0].Bounds.Right, fills[1].Bounds.Left, precision: 3);
        _ = Assert.IsType<DrawingBrush>(Assert.IsType<Border>(fills[0].Child).Background);
        Assert.NotEqual(fills[0].Background, fills[1].Background);
    }

    /// <summary>Tiny neighbours keep subpixel physical widths and receive distinct, non-overlapping targets.</summary>
    [AvaloniaFact]
    public void TinyNeighboursRemainIndividuallyReachableWithoutDistortingTheRail()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(tiny: true)));
        Button first = host.Find<Button>("CtrlRamZoom.Tiny.0");
        Button second = host.Find<Button>("CtrlRamZoom.Tiny.1");
        Assert.True(first.IsEffectivelyVisible);
        Assert.True(second.IsEffectivelyVisible);
        Assert.False(host.Bounds(first).Intersects(host.Bounds(second)));
        Assert.InRange(host.Find<Button>("CtrlRamZoom.Slice.0").Bounds.Width, 0.01, 1);
        Assert.NotEqual(AutomationProperties.GetName(first), AutomationProperties.GetName(second));
        Assert.True(first.Focus());
        host.Press(Key.Right, PhysicalKey.ArrowRight);
        Assert.True(second.IsFocused);
        host.Press(Key.Space, PhysicalKey.Space);
        Assert.Equal(1, host.Zoom.SelectedIndex);
        Assert.Equal("0x01001–0x01001", host.Zoom.Data!.Slices[1].RangeLabel);
    }

    /// <summary>The Master position keeps its name, range and underline and selects a block for the host.</summary>
    [AvaloniaFact]
    public void MasterPositionRetainsItsNameRangeUnderlineAndSelectsTheFirstBlock()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        Button position = host.Find<Button>("CtrlRamZoom.Position");
        Border underline = Assert.Single(host.Zoom.GetVisualDescendants().OfType<Border>(), b => b.Name == "CtrlRamPositionUnderline");
        Assert.Contains("Master", AutomationProperties.GetName(position), StringComparison.Ordinal);
        Assert.Contains("0x01000-0x013FF", AutomationProperties.GetName(position), StringComparison.Ordinal);
        Assert.Equal(host.Bounds(host.Rail).Left, host.Bounds(underline).Left, precision: 3);
        Assert.Equal(host.Rail.Bounds.Width, underline.Bounds.Width, precision: 3);
        Assert.True(position.Focus());
        host.Press(Key.Enter, PhysicalKey.Enter);
        Assert.Equal(0, host.Zoom.SelectedIndex);
        Assert.Equal("NF CtrlRAM", host.Zoom.Data!.Slices[0].SourceLabel);
        Assert.Equal("0x100 (0.25 KiB)", host.Zoom.Data.Slices[0].SizeLabel);
        Assert.DoesNotContain(host.Zoom.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>(), p => p.IsOpen);
    }

    /// <summary>Pointer emphasis clears on exit without changing the selected range or opening a popup.</summary>
    [AvaloniaFact]
    public void HoverHighlightsOnlyItsSliceAndLeavesSelectionIntact()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        host.Zoom.SelectedIndex = 0;
        Button normal = host.Find<Button>("CtrlRamZoom.Slice.1");
        Rect bounds = host.Bounds(normal);
        host.Window.MouseMove(new Point(bounds.Left + 3, bounds.Bottom - 3), RawInputModifiers.None);
        host.Layout();
        Assert.Equal(1, host.Zoom.HoveredIndex);
        Assert.Contains("highlighted", normal.Classes);
        Assert.Equal(0, host.Zoom.SelectedIndex);
        host.Window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        host.Layout();
        Assert.Equal(-1, host.Zoom.HoveredIndex);
        Assert.DoesNotContain("highlighted", normal.Classes);
        Assert.Equal(0, host.Zoom.SelectedIndex);
    }

    /// <summary>Real keyboard navigation reaches every range and Escape returns to the position heading.</summary>
    [AvaloniaFact]
    public void KeyboardMovesAcrossSlicesSelectsAndClearsSelection()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.0").Focus());
        host.Press(Key.End, PhysicalKey.End);
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.2").IsFocused);
        host.Press(Key.Enter, PhysicalKey.Enter);
        Assert.Equal(2, host.Zoom.SelectedIndex);
        host.Press(Key.Home, PhysicalKey.Home);
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.0").IsFocused);
        host.Press(Key.Down, PhysicalKey.ArrowDown);
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.1").IsFocused);
        host.Press(Key.Space, PhysicalKey.Space);
        Assert.Equal(1, host.Zoom.SelectedIndex);
        host.Press(Key.Escape, PhysicalKey.Escape);
        Assert.Equal(-1, host.Zoom.SelectedIndex);
        Assert.True(host.Find<Button>("CtrlRamZoom.Position").IsFocused);
    }

    /// <summary>A missing lane collapses the zoom and lets an ordinary host list take its place.</summary>
    [AvaloniaFact]
    public void MissingZoomDataLeavesTheNormalListAvailable()
    {
        using var host = new OutputLayoutPartsTestHost(null);
        Assert.False(Assert.IsType<StackPanel>(host.Zoom.Content).IsVisible);
        Assert.True(host.Fallback.IsEffectivelyVisible);
        Assert.Equal(0, host.Zoom.DesiredSize.Height);
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane());
        host.Layout();
        Assert.True(host.Zoom.IsEffectivelyVisible);
        Assert.False(host.Fallback.IsVisible);
        host.Zoom.Data = null;
        host.Layout();
        Assert.True(host.Fallback.IsEffectivelyVisible);
        Assert.Empty(host.Rail.Children);
    }

    /// <summary>Replacing a snapshot drops old targets and hover while preserving the host selection.</summary>
    [AvaloniaFact]
    public void ReplacingDataClearsTheOldInteractionTargets()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(tiny: true)));
        host.Zoom.SelectedIndex = 1;
        host.Zoom.HoveredIndex = 0;
        Button old = host.Find<Button>("CtrlRamZoom.Tiny.1");
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane());
        host.Layout();
        Assert.Equal(1, host.Zoom.SelectedIndex);
        Assert.Equal(-1, host.Zoom.HoveredIndex);
        Assert.Null(old.GetVisualParent());
        Assert.False(host.Find<Button>("CtrlRamZoom.Tiny.1").IsEffectivelyVisible);
    }

    /// <summary>Changing the language refreshes component labels while preserving input selection and focus.</summary>
    [AvaloniaFact]
    public void LanguageChangeRelocalizesVisibleAndAccessibleComponentText()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        host.Zoom.SelectedIndex = 1;
        Button selected = host.Find<Button>("CtrlRamZoom.Slice.1");
        Assert.True(selected.Focus());
        host.Zoom.Labels = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        host.Layout();
        Assert.Contains(ShellTextResources.For(ShellLanguage.ChineseTraditional).MemoryCtrlRamDetailLabel,
            AutomationProperties.GetName(host.Find<Button>("CtrlRamZoom.Position")), StringComparison.Ordinal);
        Assert.Equal(1, host.Zoom.SelectedIndex);
        Assert.True(selected.IsFocused);
    }

    /// <summary>Tab order follows physical ranges even when their visible targets move out of the rail.</summary>
    [AvaloniaFact]
    public void TabReachesTinyNeighboursInPhysicalOrder()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(tiny: true)));
        Assert.True(host.Find<Button>("CtrlRamZoom.Position").Focus());
        host.Press(Key.Tab, PhysicalKey.Tab);
        Assert.True(host.Find<Button>("CtrlRamZoom.Tiny.0").IsFocused);
        host.Press(Key.Tab, PhysicalKey.Tab);
        Assert.True(host.Find<Button>("CtrlRamZoom.Tiny.1").IsFocused);
        host.Press(Key.Tab, PhysicalKey.Tab);
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.2").IsFocused);
    }

    /// <summary>A valid viewport with no published slices also collapses instead of inventing zoom content.</summary>
    [AvaloniaFact]
    public void EmptySnapshotHasTheSameFallbackAsAnAbsentLane()
    {
        using var host = new OutputLayoutPartsTestHost(new(0, 1024, "Master", "flash", "0x00000–0x003FF", "1 KiB", []));
        Assert.False(Assert.IsType<StackPanel>(host.Zoom.Content).IsVisible);
        Assert.True(host.Fallback.IsEffectivelyVisible);
        Assert.Empty(host.Rail.Children);
        Assert.Null(CtrlRamZoomDisplay.FromLane(null));
    }

    /// <summary>Caller mutation cannot change a published slice or its physical fill parts.</summary>
    [Fact]
    public void DisplaySnapshotOwnsBothItsSlicesAndFillCollections()
    {
        CtrlRamZoomData source = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane())!;
        var fills = source.Slices[0].Fills.ToList();
        var slices = new List<CtrlRamZoomSlice> { source.Slices[0] with { Fills = fills } };
        var snapshot = new CtrlRamZoomData(source.Start, source.EndExclusive, source.PositionLabel,
            source.AddressSpace, source.RangeLabel, source.SizeLabel, slices);
        fills.Clear();
        slices.Clear();
        Assert.Equal("NF", Assert.Single(snapshot.Slices).Label);
        Assert.Equal(0x1000, Assert.Single(snapshot.Slices[0].Fills).Start);
    }

    /// <summary>Resizing updates the same address geometry and preserves selection without a lift or animation.</summary>
    [AvaloniaFact]
    public void ResizeAndEmphasisKeepTheSelectedSliceProportionalAndStationary()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        host.Zoom.SelectedIndex = 1;
        host.Window.Width = 320;
        host.Layout();
        Button selected = host.Find<Button>("CtrlRamZoom.Slice.1");
        Rect resting = host.Bounds(selected);
        host.Zoom.HoveredIndex = 1;
        host.Layout();
        Assert.Equal(resting, host.Bounds(selected));
        Assert.Equal(1, host.Zoom.SelectedIndex);
        Assert.Equal(host.Rail.Bounds.Width * 0.5, selected.Bounds.Width, precision: 3);
        Assert.True(selected.RenderTransform?.Value.IsIdentity ?? true, "A selected slice must not be lifted or scaled.");
        Assert.NotNull(selected.Transitions);
        Assert.Empty(selected.Transitions);
    }

    /// <summary>The host republishes localized lane facts instead of the control translating firmware labels.</summary>
    [AvaloniaFact]
    public void RepublishedLaneRefreshesTheLocalizedPositionAndSourceFacts()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        MemoryFocusLaneViewModel lane = OutputLayoutPartsTestHost.Lane(chinese: true);
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(lane);
        host.Zoom.Labels = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        host.Zoom.SelectedIndex = 1;
        host.Layout();
        Assert.Contains("主 IC", AutomationProperties.GetName(host.Find<Button>("CtrlRamZoom.Position")), StringComparison.Ordinal);
        Assert.Equal(lane.Ranges[1].AccessibleDetail, AutomationProperties.GetName(host.Find<Button>("CtrlRamZoom.Slice.1")));
        Assert.Equal("合成輸入的顯示細節。", host.Zoom.Data!.Slices[1].Detail);
    }
}
