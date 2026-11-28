// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Layered panel presentation consumes existing typed coverage facts without a page.</summary>
public sealed class OutputLayoutPanelPresentationTests
{
    /// <summary>The published bank selector renders both host labels and invokes only the supplied command.</summary>
    [AvaloniaFact]
    public void BankSnapshotUsesHostLabelsAndCommand()
    {
        bool? selected = null;
        OutputLayoutPanelModel model = new([OutputLayoutPanelTestHost.Slice(0, 100, "Overview")],
            bank: new(false, "A · selected", "B · kept", new RelayCommand<bool>(value => selected = value)));
        using var host = new OutputLayoutPanelTestHost(model);
        OutputLayoutBankSwitch bank = Assert.Single(host.Panel.GetVisualDescendants().OfType<OutputLayoutBankSwitch>());
        Assert.Equal("A · selected", bank.BankALabel);
        Assert.Equal("B · kept", bank.BankBLabel);
        bank.IsViewingBankB = true;
        Assert.Equal(true, selected);
    }

    /// <summary>Overview geometry and physical CtrlRAM rows may come from separate typed publication lists.</summary>
    [AvaloniaFact]
    public void OverviewRailRetainsPhysicalRowsAndTheirOriginalLaneFacts()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryCoverageSegmentViewModel overview = OutputLayoutPanelTestHost.Slice(0, 1000, "Section overview",
            MemoryWorkflowDisposition.Resolved);
        MemoryCoverageSegmentViewModel physical = OutputLayoutPanelTestHost.Slice(100, 300, "Normal CtrlRAM",
            MemoryWorkflowDisposition.WillReplace, slot: "normal", contentRole: MemoryContentRole.CtrlRam);
        MemoryCoverageSegmentViewModel second = OutputLayoutPanelTestHost.Slice(300, 500, "VN CtrlRAM",
            MemoryWorkflowDisposition.WillReplace, slot: "vn", contentRole: MemoryContentRole.CtrlRam);
        MemoryCoverageLogicalItemViewModel firstItem = new("normal", [physical], text);
        MemoryCoverageLogicalItemViewModel secondItem = new("vn", [second], text);
        MemoryFocusLaneViewModel lane = Assert.Single(MemoryFocusLaneViewModel.Create([firstItem, secondItem], text));
        OutputLayoutPanelModel model = new([overview], rowSegments: [physical, second],
            laneLinks: [new(OutputLayoutRowIdentity.For(physical), lane), new(OutputLayoutRowIdentity.For(second), lane)]);
        using var host = new OutputLayoutPanelTestHost(model);
        Assert.Equal(3, host.Bar.Children.Count);
        Assert.Equal("Normal CtrlRAM", ((MemoryCoverageSegmentViewModel)host.Bar.Children[1].DataContext!).DisplayTitle);
        Assert.True(host.Bounds(host.Bar.Children[1]).Left >= host.Bounds(host.Bar.Children[0]).Left);
        Assert.True(host.Bounds(host.Bar.Children[1]).Right <= host.Bounds(host.Bar.Children[0]).Right);
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal("Normal CtrlRAM", model.Rows[0].Members[0].DisplayTitle);
        host.Click(host.Row("1"));
        CtrlRamZoomBar zoom = Assert.Single(OutputLayoutPanelTestHost.Body(host.Row("1"))
            .GetVisualDescendants().OfType<CtrlRamZoomBar>());
        Assert.NotNull(zoom.Data);
        zoom.SelectedIndex = 1;
        host.Layout();
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("2")).IsVisible);
        Assert.Contains("VN CtrlRAM", AutomationProperties.GetName(host.Row("2")), StringComparison.Ordinal);
        Assert.True(host.Find<Button>("CtrlRamZoom.Position").Focus());
        host.Key(Key.Escape, PhysicalKey.Escape);
        Assert.True(host.Row("1").IsFocused);
    }

    /// <summary>Unclassified primary facts remain in a neutral list, and different spaces suppress a false shared scale.</summary>
    [AvaloniaFact]
    public void DifferentAddressSpacesKeepCompleteContextRowsWithoutSharedScale()
    {
        OutputLayoutPanelModel model = new([
            OutputLayoutPanelTestHost.Slice(0, 100, "First space", MemoryWorkflowDisposition.Resolved, space: "flash"),
            OutputLayoutPanelTestHost.Slice(0, 100, "Second space", MemoryWorkflowDisposition.Resolved, space: "ram"),
        ]);
        using var host = new OutputLayoutPanelTestHost(model);
        Assert.Equal(2, model.Rows.Count);
        Assert.False(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
        Assert.True(host.Panel.FindControl<TextBlock>("LayoutScaleUnavailable")!.IsVisible);
        Assert.Contains("First space", AutomationProperties.GetName(host.Row("1")), StringComparison.Ordinal);
        Assert.Contains("Second space", AutomationProperties.GetName(host.Row("2")), StringComparison.Ordinal);
    }

    /// <summary>Action groups own the common state; plain rows do not repeat group badges.</summary>
    [AvaloniaFact]
    public void GroupsOwnCommonStatesAndOnlyExceptionsGetBadges()
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 100, "Write"),
            OutputLayoutPanelTestHost.Slice(100, 200, "Kept", MemoryWorkflowDisposition.Kept),
            OutputLayoutPanelTestHost.Slice(200, 300, "Blank", MemoryWorkflowDisposition.Blank),
            OutputLayoutPanelTestHost.Slice(300, 400, "Warning", severity: MemoryDiagnosticSeverity.Warning),
            OutputLayoutPanelTestHost.Slice(400, 500, "Conflict", severity: MemoryDiagnosticSeverity.Error, role: MemoryCoverageFillRole.Conflict),
            OutputLayoutPanelTestHost.Slice(500, 600, "Observed", observed: MemoryObservedChange.Changed),
            OutputLayoutPanelTestHost.Slice(600, 700, "Observed", observed: MemoryObservedChange.Unchanged)]));
        Assert.Equal("Will write", AutomationProperties.GetName(host.Group(OutputLayoutGroupKind.Write)));
        Assert.Equal("Kept from base", AutomationProperties.GetName(host.Group(OutputLayoutGroupKind.Kept)));
        Assert.Equal("Not written", AutomationProperties.GetName(host.Group(OutputLayoutGroupKind.NotWritten)));
        Assert.True(OutputLayoutPanelTestHost.Body(host.Group(OutputLayoutGroupKind.Write)).IsVisible);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Group(OutputLayoutGroupKind.Kept)).IsVisible);
        Assert.False(OutputLayoutPanelTestHost.Body(host.Group(OutputLayoutGroupKind.NotWritten)).IsVisible);
        Assert.Contains(host.Group(OutputLayoutGroupKind.Kept).GetVisualDescendants().OfType<Border>(),
            static border => border.IsEffectivelyVisible && border.Background is DrawingBrush);
        Assert.Equal(["Warning", "Conflict", "Changed", "Unchanged"], host.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Where(static text => text.Classes.Contains("outputLayoutBadge") && text.IsEffectivelyVisible).Select(static text => text.Text));
    }

    /// <summary>Partial runs use the shared projection and retain one row with mixed parts.</summary>
    [AvaloniaFact]
    public void PartialArtifactRunShowsAnExceptionBadgeAndOnePin()
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 100, "BIN", MemoryWorkflowDisposition.WillReplace, artifact: "same"),
            OutputLayoutPanelTestHost.Slice(100, 200, "BIN", MemoryWorkflowDisposition.Kept, artifact: "same")],
            writeDisposition: MemoryWorkflowDisposition.WillReplace));
        Assert.Equal("Will replace", AutomationProperties.GetName(host.Group(OutputLayoutGroupKind.Write)));
        Assert.Equal("Partial", Assert.Single(host.Row("1").GetVisualDescendants().OfType<TextBlock>(),
            static text => text.Classes.Contains("outputLayoutBadge")).Text);
        _ = Assert.Single(host.Pins.Children);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.Row.2");
    }

    /// <summary>Address-order numbers stay identical between the bar and grouped list, including tiny runs.</summary>
    [AvaloniaFact]
    public void TinyRunHasOneMergedPinAndOneExpandableListRow()
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 440, "DP"),
            OutputLayoutPanelTestHost.Slice(440, 450, "First", MemoryWorkflowDisposition.Blank),
            OutputLayoutPanelTestHost.Slice(450, 460, "Second", MemoryWorkflowDisposition.Blank),
            OutputLayoutPanelTestHost.Slice(460, 470, "Third", MemoryWorkflowDisposition.Blank),
            OutputLayoutPanelTestHost.Slice(470, 1000, "TP")]));
        Assert.Equal(["OutputLayout.Pin.1", "OutputLayout.Pin.2–4", "OutputLayout.Pin.5"],
            host.Pins.Children.Select(AutomationProperties.GetAutomationId));
        host.Click(host.Group(OutputLayoutGroupKind.NotWritten));
        Assert.True(host.Row("2–4").IsEffectivelyVisible);
        Assert.False(host.Row("2").IsEffectivelyVisible);
        host.Click(host.Row("2–4"));
        Border localBar = Assert.Single(OutputLayoutPanelTestHost.Body(host.Row("2–4"))
            .GetVisualDescendants().OfType<Border>(), static border => border.Classes.Contains("outputLayoutLocalBar"));
        Assert.Equal(3, Assert.IsType<Canvas>(localBar.Child).Children.Count);
        Assert.True(host.Row("2").IsEffectivelyVisible);
        Assert.True(host.Row("3").IsEffectivelyVisible);
        Assert.True(host.Row("4").IsEffectivelyVisible);
        Assert.Contains("First", AutomationProperties.GetName(host.Row("2")), StringComparison.Ordinal);
    }

    /// <summary>Warnings/errors are supplied counts and never inferred from segment text.</summary>
    [AvaloniaTheory]
    [InlineData(0, 0, "", false)]
    [InlineData(1, 0, "1 warning", true)]
    [InlineData(2, 1, "1 error · 2 warnings", true)]
    public void OnlyPositiveHostIssueCountsShowOneLine(int warnings, int errors, string expected, bool visible)
    {
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 100, "Warning Conflict blocked unchanged")], warningCount: warnings, errorCount: errors));
        TextBlock line = host.Panel.FindControl<TextBlock>("LayoutIssues")!;
        Assert.Equal(expected, line.Text);
        Assert.Equal(visible, line.IsVisible);
        Assert.DoesNotContain(host.Row("1").GetVisualDescendants().OfType<TextBlock>(),
            static text => text.Classes.Contains("outputLayoutBadge") && text.IsVisible);
    }

    /// <summary>Waiting and blocked states preserve complete opaque reasons and suppress address geometry.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingStatesDisplayTheFullHostReasonInADashedFrame(bool blocked)
    {
        const string reason = "First reason line.\nSecond reason line with the entire input failure and next action.";
        using var host = new OutputLayoutPanelTestHost(new(OutputLayoutPanelTestHost.Example().Segments,
            notice: new(blocked ? OutputLayoutNoticeKind.Blocked : OutputLayoutNoticeKind.Waiting, "Host status", reason)));
        Assert.False(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
        Assert.Empty(host.Bar.Children);
        Assert.Contains(host.Panel.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == reason);
        Assert.Equal([3d, 3d], Assert.Single(host.Panel.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()).StrokeDashArray);
    }

    /// <summary>Replacing a localized publication updates both component chrome and the supplied row strings.</summary>
    [AvaloniaFact]
    public void LanguageSwitchUpdatesLabelsWithoutLosingExpandedRows()
    {
        using var host = new OutputLayoutPanelTestHost();
        host.Click(host.Row("1"));
        host.Panel.Labels = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        host.Panel.Model = OutputLayoutPanelTestHost.Example(ShellLanguage.ChineseTraditional);
        host.Layout();
        Assert.Equal("輸出配置", host.Panel.FindControl<TextBlock>("LayoutTitle")!.Text);
        Assert.Equal("將寫入", AutomationProperties.GetName(host.Group(OutputLayoutGroupKind.Write)));
        Assert.Equal("技術細節", AutomationProperties.GetName(host.Technical("1")));
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row("1")).IsVisible);
        Assert.Contains(host.Panel.GetVisualDescendants().OfType<TextBlock>(), static text => text.IsEffectivelyVisible && text.Text == "目標位址");
    }
}
