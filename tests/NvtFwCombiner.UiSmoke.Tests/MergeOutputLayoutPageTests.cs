// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Merge's actual production page consumes one coherent panel publication in every mode.</summary>
public sealed class MergeOutputLayoutPageTests
{
    /// <summary>Empty inputs retain the capability's plan or the General pending reason through relocalization.</summary>
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
        AssertEmptyInputLayout(host, mode);

        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();

        Assert.Same(panel, host.Panel);
        Assert.NotSame(english, host.Model);
        AssertEmptyInputLayout(host, mode);
        if (mode == ExperienceIds.GeneralMerge)
        {
            Assert.NotEqual(english.Notice!.Title, host.Model.Notice!.Title);
            Assert.NotEqual(english.Notice.Reason, host.Model.Notice.Reason);
        }
        else
        {
            Assert.Equal(english.StartAddress, host.Model.StartAddress);
            Assert.Equal(english.EndAddress, host.Model.EndAddress);
            Assert.NotEqual(english.SummaryText, host.Model.SummaryText);
        }
        Assert.Equal("輸出配置", panel.FindControl<TextBlock>("LayoutTitle")!.Text);
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
        string otherMode = mode == ExperienceIds.GeneralMerge ? ExperienceIds.AbMerge : ExperienceIds.GeneralMerge;
        host.Shell.Merge.SelectedMergeMode = otherMode;
        await host.SettleAsync();
        Assert.NotSame(loaded, host.Model);
        AssertEmptyInputLayout(host, otherMode);
        host.Shell.Merge.SelectedMergeMode = mode;
        await host.SettleAsync();
        Assert.Equal(loaded.StartAddress, host.Model.StartAddress);
        Assert.Equal(loaded.EndAddress, host.Model.EndAddress);
        Assert.Null(host.Model.Notice);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.ShowIssue");
    }

    /// <summary>Invalid inputs block Build while the exact capability's planned layout remains available.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.AbMerge)]
    public async Task BlockedInputKeepsThePlannedLayoutAndExistingDiagnostic(string mode)
    {
        using var workspace = TempWorkspace.Create("merge-layout-blocked");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await LoadAsync(host, workspace, mode);
        (long? Start, long? End, string? Slot)[] planned = [.. host.Model.Segments.Select(
            static segment => (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId))];
        string slot = mode == ExperienceIds.AbMerge ? CompositionAddressSpaceIds.DpAbInput : CompositionSlotIds.MergeDp;
        await host.Shell.WorkflowSession.SetSlotFileAsync(slot, workspace.Write("invalid.bin", [0]), TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.Null(host.Model.Notice);
        Assert.Equal(planned, host.Model.Segments.Select(
            static segment => (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId)));
        Assert.Equal(1, host.Model.ErrorCount);
        Assert.True(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
        Assert.NotEmpty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        FirmwareSlotViewModel blocked = Assert.Single(host.Shell.Merge.MergeSlots,
            static input => input.IsInputInspectionBlocking);
        Assert.Equal(slot, blocked.SlotId);
        Assert.Equal(blocked.SemanticStateAutomationText, Assert.Single(host.Model.InputErrors));
        _ = Assert.Single(host.Panel.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == blocked.SemanticStateAutomationText);
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

    /// <summary>A General blocker keeps the Application sentence and code without repeating status or title.</summary>
    [AvaloniaFact]
    public async Task GeneralBlockedMappingKeepsThePagesFullReason()
    {
        using var workspace = TempWorkspace.Create("merge-layout-general-blocked");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadGeneralAsync(workspace);
        GeneralMergeMappingViewModel mapping = Assert.Single(host.Shell.Merge.GeneralMergeMappings);
        mapping.TargetStartAddress = "0x50000";
        await host.SettleAsync();
        Assert.False(host.Shell.Merge.CanBuildMerge);
        Assert.Empty(host.Shell.Merge.BuildAvailability.Blockers);
        Assert.True(host.Shell.HasMergeBuildBlocker);
        Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
        GeneralAuthoringAdmissionIssue issue = Assert.Single(host.Shell.Merge.GeneralMergeAdmission!.Issues);
        Assert.Equal(GeneralAuthoringIssueCodes.TargetOutOfBounds, issue.Code);
        Assert.Equal($"General mapping '{mapping.MappingId}' target [0x50000, 0x50004) is outside address space 'output-image'.", issue.Message);
        Assert.Equal(issue.Message, host.Shell.MergeBuildBlockerCard.Summary);
        Assert.Equal($"{issue.Message} {issue.Code}", host.Model.Notice.Reason);
        Assert.Equal(host.Shell.MergeBuildBlockerCard.Subject, host.Model.Notice.Title);
        Assert.DoesNotContain(host.Shell.MergeBuildBlockerCard.Caption, host.Model.Notice.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Model.Notice.Title, host.Model.Notice.Reason, StringComparison.Ordinal);
        Assert.Equal(1, host.Model.ErrorCount);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        _ = Assert.Single(host.Panel.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == host.Model.Notice.Reason);
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.Equal(issue.Message, host.Shell.MergeBuildBlockerCard.Summary);
        Assert.Equal($"{issue.Message} {issue.Code}", host.Model.Notice!.Reason);
        Assert.DoesNotContain(host.Shell.MergeBuildBlockerCard.Caption, host.Model.Notice.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Model.Notice.Title, host.Model.Notice.Reason, StringComparison.Ordinal);
        mapping.TargetStartAddress = "0x7000";
        await host.SettleAsync();
        Assert.Null(host.Model.Notice);
        Assert.Equal(0, host.Model.ErrorCount);
        Assert.NotEmpty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
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

    /// <summary>A transient empty selector value retains the accepted IC and its complete panel publication.</summary>
    [AvaloniaFact]
    public async Task EmptyIcSelectionKeepsTheAcceptedLayout()
    {
        using var workspace = TempWorkspace.Create("merge-layout-no-ic");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadStandardAsync();
        OutputLayoutPanelModel previous = host.Model;
        host.Shell.WorkflowSession.SelectedIc = string.Empty;
        host.Render();
        Assert.Equal("NT51929", host.Shell.WorkflowSession.SelectedIc);
        Assert.Same(previous, host.Model);
        Assert.NotNull(host.Shell.Merge.OutputLayout.Current.Map);
        Assert.Null(host.Model.Notice);
        Assert.NotEmpty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
    }

    /// <summary>Only General lacks an exact capability before its first source mapping is accepted.</summary>
    private static void AssertEmptyInputLayout(MergeOutputLayoutTestHost host, string mode)
    {
        if (mode == ExperienceIds.GeneralMerge)
        {
            Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
            Assert.Equal(Assert.Single(host.Shell.Merge.OutputLayout.Current.Rows).Detail, host.Model.Notice.Reason);
            Assert.Null(host.Shell.Merge.OutputLayout.Current.Map);
            Assert.False(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
            Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
            return;
        }

        Assert.Null(host.Model.Notice);
        // The legacy operation map omits AB scratch-space steps; the panel uses canonical coverage instead.
        if (mode == ExperienceIds.AbMerge)
        {
            Assert.Null(host.Shell.Merge.OutputLayout.Current.Map);
        }
        else
        {
            Assert.NotNull(host.Shell.Merge.OutputLayout.Current.Map);
        }
        Assert.NotEmpty(host.Model.Rows);
        Assert.Equal(host.Shell.Merge.OutputLayout.Current.CoverageSegments, host.Model.Segments);
        Assert.Equal("0x00000", host.Model.StartAddress);
        Assert.Equal(mode == ExperienceIds.AbMerge ? "0x7FFFF" : "0x3FFFF", host.Model.EndAddress);
        Assert.True(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
        Assert.NotEmpty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        Assert.False(host.Shell.Merge.CanBuildMerge);
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
