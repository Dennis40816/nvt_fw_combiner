using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Every rail slice of a loaded image lights its own legend row while hovered.</summary>
public sealed class MemoryLegendRailHighlightTests
{
    /// <summary>Every raw AB block lights its coalesced row and numbered pin without opening a popup.</summary>
    [AvaloniaTheory]
    [InlineData("NT51929", "nt51929-ab-t05-d06")]
    [InlineData("NT51950", "nt51950-ab-boe-d82t80")]
    public async Task HoveringEveryAbRailSliceLightsItsNumberedRow(string ic, string caseId)
    {
        using var workspace = TempWorkspace.Create("legend-rail-highlight");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadAbAsync(ic, caseId);
        Canvas bar = host.Panel.FindControl<Canvas>("LayoutBar")!;
        Assert.NotEmpty(bar.Children);
        foreach (Control slice in bar.Children)
        {
            MemoryCoverageSegmentViewModel segment = Assert.IsType<MemoryCoverageSegmentViewModel>(slice.DataContext);
            Button row = host.RowFor(segment);
            Border highlight = Assert.Single(row.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("outputLayoutRow"));
            host.Window.MouseMove(host.Bounds(slice).Center, RawInputModifiers.None);
            host.Render();
            Assert.Contains("active", highlight.Classes);
            _ = Assert.Single(host.Panel.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("outputLayoutRow") && border.Classes.Contains("active"));
            _ = Assert.Single(host.Panel.FindControl<Canvas>("LayoutPins")!.Children,
                static pin => pin.Classes.Contains("active"));
            Assert.False(OutputLayoutPanelTestHost.Body(row).IsVisible);
            host.Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            host.Render();
            Assert.DoesNotContain("active", highlight.Classes);
        }
        Assert.DoesNotContain(host.Window.GetVisualDescendants(), static control => control.Name is "MemoryLocalView" or "MemorySliceCard");
    }

    /// <summary>A CtrlRAM row highlights its numbered entry while preserving its physical lane facts.</summary>
    [AvaloniaFact]
    public async Task HoveringACtrlRamLaneLightsItsContainingSectionRow()
    {
        using var workspace = TempWorkspace.Create("legend-lane-highlight");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1440, height: 1040);
        await host.LoadCtrlRamAsync();
        MemoryCoverageSegmentViewModel selected = host.Model.RowSegments.First(
            static segment => segment.CtrlRamRegionRole == CtrlRamRegionRole.Nf && segment.IsSelectedForWrite);
        Assert.Contains(host.Model.LaneLinks, link => link.Row == OutputLayoutRowIdentity.For(selected));
        Button row = host.RowFor(selected);
        Border highlight = Assert.Single(row.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("outputLayoutRow"));
        host.Window.MouseMove(host.Bounds(row).Center, RawInputModifiers.None);
        host.Render();
        Assert.Contains("active", highlight.Classes);
        Assert.False(OutputLayoutPanelTestHost.Body(row).IsVisible);
        host.Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
        host.Render();
        Assert.DoesNotContain("active", highlight.Classes);
        host.AssertDetails(selected);
        Assert.DoesNotContain(host.Window.GetVisualDescendants(),
            static control => control.Name is "MemoryLocalView" or "MemorySliceCard");
    }

}
