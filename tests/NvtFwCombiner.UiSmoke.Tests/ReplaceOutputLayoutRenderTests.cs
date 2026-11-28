// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Captures the actual production Replace surface across the approved review states.</summary>
public sealed class ReplaceOutputLayoutRenderTests
{
    /// <summary>Both modes, banks, expanded zoom, dense pins and terminal notices use both themes, languages and widths.</summary>
    public static TheoryData<string, int, bool, bool> Cases
    {
        get
        {
            var cases = new TheoryData<string, int, bool, bool>();
            foreach (string state in new[] { "general", "ctrlram", "ab-a", "ab-b", "expanded", "merged-pins",
                "general-waiting", "ctrlram-waiting", "general-blocked", "ctrlram-blocked" })
            {
                foreach (int width in new[] { 1280, 960 })
                {
                    cases.Add(state, width, false, false);
                    cases.Add(state, width, false, true);
                    cases.Add(state, width, true, false);
                    cases.Add(state, width, true, true);
                }
            }
            return cases;
        }
    }

    /// <summary>Writes 80 production screenshots and asserts hierarchy, responsive anchors and horizontal containment.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public async Task RenderProductionReplaceReviewMatrix(string state, int width, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("replace-layout-render");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(
            workspace, width, height: 2400, dark: dark, chinese: chinese);
        await PrepareAsync(host, workspace, state);
        host.Render();
        OutputLayoutPanel panel = host.Panel;
        Grid layout = Assert.IsType<Grid>(panel.Parent);
        Assert.Equal(width == 960 ? 1 : 2, layout.ColumnDefinitions.Count);
        Assert.Equal(width == 960 ? 3 : 2, Grid.GetRow(panel));
        Assert.Equal(width == 960 ? 0 : 1, Grid.GetColumn(panel));
        Assert.InRange(Math.Abs(panel.Bounds.Width - (width == 960 ? layout.Bounds.Width : 430)), 0, 0.5);
        Assert.InRange(host.Bounds(panel).Right, 1, width);
        Assert.Equal(chinese ? "輸出配置" : "Output layout", panel.FindControl<TextBlock>("LayoutTitle")!.Text);
        Control next = host.Model.Notice is null ? panel.FindControl<StackPanel>("LayoutMap")! : panel.FindControl<StackPanel>("LayoutNotice")!;
        Assert.True(host.Bounds(panel.FindControl<TextBlock>("LayoutTitle")!).Bottom <= host.Bounds(next).Top);
        Assert.All(panel.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.IsEffectivelyVisible), text =>
        {
            Assert.True(host.Bounds(text).Left >= host.Bounds(panel).Left);
            Assert.True(host.Bounds(text).Right <= host.Bounds(panel).Right + 0.5);
        });
        host.Capture($"replace-output-layout-{state}-{width}-{(dark ? "dark" : "light")}-{(chinese ? "zh-TW" : "en")}");
    }

    /// <summary>Authors each frame through existing production commands and accepted fixture inputs.</summary>
    private static async Task PrepareAsync(ReplaceOutputLayoutTestHost host, TempWorkspace workspace, string state)
    {
        if (state is "ab-a" or "ab-b")
        {
            await ReplaceOutputLayoutScenarios.LoadAbAsync(host);
            host.Shell.Replace.IsViewingCtrlRamBankB = state == "ab-b";
            host.Render();
            Assert.NotNull(host.Model.Bank);
            return;
        }
        string mode = state.StartsWith("general", StringComparison.Ordinal) || state == "merged-pins"
            ? ExperienceIds.GeneralReplace : ExperienceIds.CtrlRamReplace;
        if (state.EndsWith("waiting", StringComparison.Ordinal))
        {
            host.Shell.WorkflowSession.SelectedIc = "NT51926";
            host.Shell.Replace.SelectedReplaceMode = mode;
            host.Render();
            Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
            return;
        }
        if (state == "merged-pins")
        {
            await ReplaceOutputLayoutScenarios.LoadDenseGeneralAsync(host, workspace);
            OpenMergedPins(host);
            return;
        }
        await ReplaceOutputLayoutScenarios.LoadAsync(host, workspace, mode);
        if (state.EndsWith("blocked", StringComparison.Ordinal))
        {
            await host.Shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase,
                workspace.Write("blocked-reference.bin", [0]), TestContext.Current.CancellationToken);
            await host.SettleAsync();
            Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
        }
        else if (state == "expanded")
        {
            OutputLayoutPreparedLaneLink link = host.Model.LaneLinks.First();
            MemoryCoverageSegmentViewModel member = link.Lane.Ranges.First(range => OutputLayoutRowIdentity.For(range) == link.Row);
            host.Open(member);
            Assert.Contains(host.Panel.GetVisualDescendants().OfType<CtrlRamZoomBar>(), static zoom => zoom.IsEffectivelyVisible);
        }
    }

    /// <summary>Activates the real geometry-grouped pin through keyboard input before capture.</summary>
    private static void OpenMergedPins(ReplaceOutputLayoutTestHost host)
    {
        Canvas pins = host.Panel.FindControl<Canvas>("LayoutPins")!;
        Control merged = pins.Children.First(pin =>
            AutomationProperties.GetAutomationId(pin)?.StartsWith("OutputLayout.Pin.", StringComparison.Ordinal) == true &&
            pin.DataContext is OutputLayoutPanelRow[] rows && (rows.Length > 1 || rows[0].Members.Count > 1));
        Assert.True(merged.Focus());
        host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        host.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        host.Render();
        Assert.Contains(host.Panel.GetVisualDescendants().OfType<Button>(), static button =>
            button.IsEffectivelyVisible && (AutomationProperties.GetAutomationId(button)?.StartsWith("OutputLayout.PinMember.", StringComparison.Ordinal) == true ||
                button.DataContext is OutputLayoutPanelRow { Members.Count: > 1 }));
    }
}
