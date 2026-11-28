// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Merge's actual production page consumes one coherent panel publication in every mode.</summary>
public sealed class MergeOutputLayoutPageTests
{
    /// <summary>Waiting content and its complete reason relocalize without acquiring address geometry.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.GeneralMerge)]
    [InlineData(ExperienceIds.AbMerge)]
    public async Task WaitingAndLanguageSwitchUseTheCurrentPublication(string mode)
    {
        using var workspace = TempWorkspace.Create("merge-layout-waiting");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        host.Shell.Merge.SelectedMergeMode = mode;
        host.Render();
        OutputLayoutPanel panel = host.Panel;
        OutputLayoutPanelModel english = host.Model;
        Assert.Same(host.Shell.Merge.OutputLayout.Current.Panel, english);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, english.Notice!.Kind);
        Assert.Equal(Assert.Single(host.Shell.Merge.OutputLayout.Current.Rows).Detail, english.Notice.Reason);
        Assert.False(panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
        Assert.Empty(panel.FindControl<Canvas>("LayoutBar")!.Children);

        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();

        Assert.Same(panel, host.Panel);
        Assert.NotSame(english, host.Model);
        Assert.NotEqual(english.Notice.Title, host.Model.Notice!.Title);
        Assert.NotEqual(english.Notice.Reason, host.Model.Notice.Reason);
        Assert.Equal("輸出配置", panel.FindControl<TextBlock>("LayoutTitle")!.Text);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice.Kind);
    }

    /// <summary>Loaded layouts keep physical facts, details and input selections through relocalization and a mode round trip.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.GeneralMerge)]
    [InlineData(ExperienceIds.AbMerge)]
    public async Task LoadedModeAndLanguageSwitchNeverReuseAnotherModesContent(string mode)
    {
        using var workspace = TempWorkspace.Create("merge-layout-loaded");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await LoadAsync(host, workspace, mode);
        OutputLayoutPanelModel loaded = host.Model;
        Assert.Null(loaded.Notice);
        Assert.NotEmpty(loaded.Rows);
        Assert.Same(host.Shell.Merge.OutputLayout.Current.Panel, loaded);
        Assert.False(loaded.CanRequestIssue);
        host.AssertDetails(loaded.Rows.First(static row => row.Group == OutputLayoutGroupKind.Write).Members[0]);
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.NotSame(loaded, host.Model);
        Assert.Equal(loaded.Segments.Select(static segment => (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId)),
            host.Model.Segments.Select(static segment => (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId)));
        Assert.NotEqual(loaded.SummaryText, host.Model.SummaryText);
        host.Shell.Merge.SelectedMergeMode = mode == ExperienceIds.GeneralMerge ? ExperienceIds.AbMerge : ExperienceIds.GeneralMerge;
        await host.SettleAsync();
        Assert.NotSame(loaded, host.Model);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        host.Shell.Merge.SelectedMergeMode = mode;
        await host.SettleAsync();
        Assert.Equal(loaded.StartAddress, host.Model.StartAddress);
        Assert.Equal(loaded.EndAddress, host.Model.EndAddress);
        Assert.Null(host.Model.Notice);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.ShowIssue");
    }

    /// <summary>Input blockers retain the exact typed pending error and full reason on the production page.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.AbMerge)]
    public async Task BlockedInputKeepsTheFullReasonAndClearsLoadedGeometry(string mode)
    {
        using var workspace = TempWorkspace.Create("merge-layout-blocked");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await LoadAsync(host, workspace, mode);
        string slot = mode == ExperienceIds.AbMerge ? CompositionAddressSpaceIds.DpAbInput : CompositionSlotIds.MergeDp;
        await host.Shell.WorkflowSession.SetSlotFileAsync(slot, workspace.Write("invalid.bin", [0]), TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
        Assert.Equal(Assert.Single(host.Shell.Merge.OutputLayout.Current.Rows).Detail, host.Model.Notice.Reason);
        Assert.Equal(1, host.Model.ErrorCount);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        Assert.Contains(host.Panel.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == host.Model.Notice.Reason);
        Assert.False(host.Shell.Merge.CanBuildMerge);
    }

    /// <summary>General overlap is drawn from the canonical admission conflict, with its exact addresses and details.</summary>
    [AvaloniaFact]
    public async Task GeneralConflictUsesCanonicalCoordinatesAndCount()
    {
        using var workspace = TempWorkspace.Create("merge-layout-conflict");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadGeneralAsync(workspace);
        host.Shell.Merge.AddGeneralMergeMappingCommand.Execute(null);
        GeneralMergeMappingViewModel second = host.Shell.Merge.GeneralMergeMappings[1];
        second.TargetStartAddress = "0x7002";
        second.Length = "0x4";
        await host.Shell.WorkflowSession.SetSlotFileAsync(second.MappingId,
            workspace.Write("second.bin", [0x20, 0x21, 0x22, 0x23]), TestContext.Current.CancellationToken);
        await host.SettleAsync();
        MemoryCoverageSegmentViewModel conflict = Assert.Single(host.Model.Segments,
            static segment => segment.FillRole == MemoryCoverageFillRole.Conflict);
        Assert.Equal(0x7002, conflict.RangeStart);
        Assert.Equal(0x7004, conflict.RangeEndExclusive);
        Assert.Equal(1, host.Model.ErrorCount);
        Assert.False(host.Shell.Merge.CanBuildMerge);
        host.Open(conflict);
        Assert.Contains(OutputLayoutPanelTestHost.Body(host.RowFor(conflict)).GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == conflict.PreservationSummary);
        Assert.Contains(host.Panel.FindControl<Canvas>("LayoutBar")!.Children,
            block => ReferenceEquals(block.DataContext, conflict));
    }

    /// <summary>A General blocker without drawable overlap reuses the page's complete Build explanation.</summary>
    [AvaloniaFact]
    public async Task GeneralBlockedMappingKeepsThePagesFullReason()
    {
        using var workspace = TempWorkspace.Create("merge-layout-general-blocked");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadGeneralAsync(workspace);
        Assert.Single(host.Shell.Merge.GeneralMergeMappings).TargetStartAddress = "0x50000";
        await host.SettleAsync();
        Assert.False(host.Shell.Merge.CanBuildMerge);
        Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
        Assert.Equal(host.Shell.MergeBuildBlockerCard.AutomationText, host.Model.Notice.Reason);
        Assert.True(host.Model.ErrorCount > 0);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.Equal(host.Shell.MergeBuildBlockerCard.AutomationText, host.Model.Notice!.Reason);
    }

    /// <summary>One input warning is counted once across its multiple output slices, and no unhandled Issues action is shown.</summary>
    [AvaloniaFact]
    public async Task WarningCountUsesTheExistingInputDiagnostic()
    {
        using var workspace = TempWorkspace.Create("merge-layout-warning");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadAbAsync("NT51950", "nt51950-ab-osd-d03t02-20260924");
        int warnings = host.Shell.Merge.MergeSlots.Count(static slot => slot.IsInputInspectionWarning);
        Assert.True(warnings > 0);
        Assert.Equal(warnings, host.Model.WarningCount);
        Assert.Equal(0, host.Model.ErrorCount);
        Assert.Equal($"{warnings} {(warnings == 1 ? "warning" : "warnings")}", host.Panel.FindControl<TextBlock>("LayoutIssues")!.Text);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.ShowIssue");
    }

    /// <summary>Clearing the IC cannot leave another context's accepted geometry on the page.</summary>
    [AvaloniaFact]
    public async Task NoIcDropsTheAcceptedLayout()
    {
        using var workspace = TempWorkspace.Create("merge-layout-no-ic");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadStandardAsync();
        OutputLayoutPanelModel previous = host.Model;
        host.Shell.WorkflowSession.SelectedIc = string.Empty;
        host.Render();
        Assert.NotSame(previous, host.Model);
        Assert.Null(host.Shell.Merge.OutputLayout.Current.Map);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
    }

    /// <summary>Loads each mode through its production input authoring path.</summary>
    internal static Task LoadAsync(MergeOutputLayoutTestHost host, TempWorkspace workspace, string mode)
    {
        return mode switch
        {
            ExperienceIds.GeneralMerge => host.LoadGeneralAsync(workspace),
            ExperienceIds.AbMerge => host.LoadAbAsync(),
            _ => host.LoadStandardAsync(),
        };
    }
}
