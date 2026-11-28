// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Layered panel presentation consumes existing typed coverage facts without a page.</summary>
public sealed class OutputLayoutPanelPresentationTests
{
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
