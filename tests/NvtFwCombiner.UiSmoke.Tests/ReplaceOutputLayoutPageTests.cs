// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>The actual Replace page adopts complete typed displays across all context and interaction changes.</summary>
public sealed class ReplaceOutputLayoutPageTests
{
    /// <summary>Waiting notices retain the canonical reason and acquire no geometry during language changes.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.GeneralReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace)]
    public async Task WaitingAndLanguageSwitchUseOneCompletePublication(string mode)
    {
        using var workspace = TempWorkspace.Create("replace-layout-waiting");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        host.Shell.WorkflowSession.SelectedIc = "NT51926";
        host.Shell.Replace.SelectedReplaceMode = mode;
        host.Render();
        OutputLayoutPanelModel english = host.Model;
        Assert.Same(host.Shell.Replace.OutputLayout.Current.Panel, english);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, english.Notice!.Kind);
        Assert.Equal(Assert.Single(host.Shell.Replace.OutputLayout.Current.Rows).Detail, english.Notice.Reason);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        Assert.Null(english.Bank);
        Assert.Empty(english.LaneLinks);

        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();

        Assert.NotSame(english, host.Model);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
        Assert.NotEqual(english.Notice.Reason, host.Model.Notice.Reason);
        Assert.Equal("輸出配置", host.Panel.FindControl<TextBlock>("LayoutTitle")!.Text);
        Assert.False(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
    }

    /// <summary>Loaded details and physical facts survive relocalization while a mode round trip replaces every display input.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.GeneralReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace)]
    public async Task LoadedModeAndLanguageRoundTripNeverReusesAnotherModesDisplay(string mode)
    {
        using var workspace = TempWorkspace.Create("replace-layout-round-trip");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await ReplaceOutputLayoutScenarios.LoadAsync(host, workspace, mode);
        OutputLayoutPanelModel loaded = host.Model;
        Assert.Null(loaded.Notice);
        Assert.Same(host.Shell.Replace.OutputLayout.Current.Panel, loaded);
        Assert.NotEmpty(loaded.Rows);
        MemoryCoverageSegmentViewModel selected = loaded.RowSegments.First(static segment => segment.IsSelectedForWrite && segment.IsPrimaryContent);
        host.AssertDetails(selected);
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.NotSame(loaded, host.Model);
        Assert.Equal(loaded.RowSegments.Select(OutputLayoutRowIdentity.For), host.Model.RowSegments.Select(OutputLayoutRowIdentity.For));
        Assert.NotEqual(loaded.SummaryText, host.Model.SummaryText);
        host.Shell.Replace.SelectedReplaceMode = mode == ExperienceIds.GeneralReplace ? ExperienceIds.CtrlRamReplace : ExperienceIds.GeneralReplace;
        await host.SettleAsync();
        Assert.NotSame(loaded, host.Model);
        Assert.Null(host.Model.Bank);
        Assert.Empty(host.Panel.GetVisualDescendants().OfType<CtrlRamZoomBar>().Where(static zoom => zoom.IsEffectivelyVisible));
        host.Shell.Replace.SelectedReplaceMode = mode;
        await host.SettleAsync();
        await ReplaceOutputLayoutScenarios.LoadAsync(host, workspace, mode);
        Assert.Null(host.Model.Notice);
        Assert.Equal(loaded.StartAddress, host.Model.StartAddress);
        Assert.Equal(loaded.EndAddress, host.Model.EndAddress);
        Assert.Equal(loaded.RowSegments.Select(OutputLayoutRowIdentity.For), host.Model.RowSegments.Select(OutputLayoutRowIdentity.For));
    }

    /// <summary>Rejected input clears accepted geometry and exposes the complete canonical error on both production modes.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.GeneralReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace)]
    public async Task BlockedInputDropsThePreviousRowsBankAndZoom(string mode)
    {
        using var workspace = TempWorkspace.Create("replace-layout-blocked");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await ReplaceOutputLayoutScenarios.LoadAsync(host, workspace, mode);
        OutputLayoutPanelModel previous = host.Model;
        await host.Shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase,
            workspace.Write("invalid-reference.bin", [0]), TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.NotSame(previous, host.Model);
        Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
        Assert.Equal(Assert.Single(host.Shell.Replace.OutputLayout.Current.Rows).Detail, host.Model.Notice.Reason);
        Assert.Equal(host.Shell.Replace.ReplaceBaseSlot.IsSemanticStateError ? 1 : 0, host.Model.ErrorCount);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        Assert.Empty(host.Panel.FindControl<StackPanel>("LayoutGroups")!.Children);
        Assert.Null(host.Model.Bank);
        Assert.Empty(host.Model.LaneLinks);
        Assert.Contains(host.Panel.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == host.Model.Notice.Reason);
        Assert.False(host.Shell.Replace.CanBuildReplace);
    }

    /// <summary>Bank viewing changes only the display viewport, retaining the accepted draft, inspection and A-only write selection.</summary>
    [AvaloniaFact]
    public async Task BankSwitchPublishesMatchingRowsLanesAndLabelsWithoutChangingWriteScope()
    {
        using var workspace = TempWorkspace.Create("replace-layout-bank");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await ReplaceOutputLayoutScenarios.LoadAbAsync(host);
        OutputLayoutPanelModel bankA = host.Model;
        CtrlRamAuthoringDraftState? draft = host.Shell.Replace.CurrentCtrlRamDraft;
        AuthoringRevision revision = host.Shell.Replace.ReplaceInputAuthoringRevision;
        object? inspection = host.Shell.Replace.ReplaceBaseSlot.CurrentInspectionProjection;
        Assert.False(bankA.Bank!.IsViewingBankB);
        Assert.Equal("0x00000", bankA.StartAddress);
        RadioButton bankB = BankButton(host, "B");
        Assert.True(bankB.Focus());
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        host.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        host.Render();
        Assert.NotSame(bankA, host.Model);
        Assert.True(host.Model.Bank!.IsViewingBankB);
        Assert.Equal("0x40000", host.Model.StartAddress);
        Assert.Equal("0x7FFFF", host.Model.EndAddress);
        Assert.Equal(host.Shell.Replace.Text.FormatCtrlRamBankView(true, false), host.Model.Bank.BankBLabel);
        Assert.All(host.Model.RowSegments, static segment => Assert.True(segment.RangeStart >= 0x40000));
        Assert.All(host.Model.LaneLinks, static link => Assert.True(link.Lane.Start >= 0x40000));
        Assert.DoesNotContain(host.Model.RowSegments, static segment => segment.IsSelectedForWrite);
        Assert.Same(draft, host.Shell.Replace.CurrentCtrlRamDraft);
        Assert.Equal(revision, host.Shell.Replace.ReplaceInputAuthoringRevision);
        Assert.Same(inspection, host.Shell.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        Assert.True(host.Shell.Replace.IsCtrlRamBankASelected);
        Assert.True(host.Shell.Replace.CanBuildReplace);
        BankButton(host, "A").Command!.Execute(false);
        host.Render();
        Assert.Equal(bankA.StartAddress, host.Model.StartAddress);
        Assert.Equal(bankA.RowSegments.Select(OutputLayoutRowIdentity.For), host.Model.RowSegments.Select(OutputLayoutRowIdentity.For));
    }

    /// <summary>Inline zoom selects exact original members and clears old interaction targets after a language publication.</summary>
    [AvaloniaFact]
    public async Task ExpandedZoomUsesTheTypedLaneAndOriginalDetailedFacts()
    {
        using var workspace = TempWorkspace.Create("replace-layout-zoom");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadCtrlRamAsync();
        OutputLayoutPreparedLaneLink link = host.Model.LaneLinks.First();
        MemoryCoverageSegmentViewModel member = link.Lane.Ranges.First(range => OutputLayoutRowIdentity.For(range) == link.Row);
        host.Open(member);
        CtrlRamZoomBar zoom = Assert.Single(OutputLayoutPanelTestHost.Body(host.RowFor(member)).GetVisualDescendants().OfType<CtrlRamZoomBar>());
        Assert.Same(link.Zoom, zoom.Data);
        Assert.Equal(link.Lane.Start, zoom.Data!.Start);
        Assert.Equal(link.Lane.EndExclusive, zoom.Data.EndExclusive);
        Button target = Assert.Single(zoom.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "CtrlRamZoom.Slice.0");
        target.Command!.Execute(0);
        host.Render();
        Assert.Equal(0, zoom.SelectedIndex);
        Assert.Contains(zoom.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == link.Lane.Ranges[0].SourceLabel);
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.DoesNotContain(host.Panel.GetVisualDescendants(), control => ReferenceEquals(control, zoom));
        Assert.All(host.Panel.GetVisualDescendants().OfType<CtrlRamZoomBar>(), static current => Assert.Equal(-1, current.SelectedIndex));
        Assert.Equal(link.Lane.Ranges.Select(OutputLayoutRowIdentity.For), host.Model.LaneLinks[0].Lane.Ranges.Select(OutputLayoutRowIdentity.For));
    }

    /// <summary>Changing the IC discards the former file's bank, zoom, labels and accepted physical ranges.</summary>
    [AvaloniaFact]
    public async Task ClearingTheIcDropsEveryAcceptedDisplayPart()
    {
        using var workspace = TempWorkspace.Create("replace-layout-no-ic");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await ReplaceOutputLayoutScenarios.LoadAbAsync(host);
        OutputLayoutPanelModel previous = host.Model;
        host.Shell.WorkflowSession.SelectedIc = string.Empty;
        host.Render();
        Assert.NotSame(previous, host.Model);
        Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
        Assert.Null(host.Model.Bank);
        Assert.Empty(host.Model.LaneLinks);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.ShowIssue");
    }

    /// <summary>Publication counts use the same typed input states as the loaded page, without multiplying output slices.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.GeneralReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace)]
    public async Task LoadedCountsUseTheExistingInputFacts(string mode)
    {
        using var workspace = TempWorkspace.Create("replace-layout-counts");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await ReplaceOutputLayoutScenarios.LoadAsync(host, workspace, mode);
        FirmwareSlotViewModel[] slots = mode == ExperienceIds.GeneralReplace
            ? [host.Shell.Replace.ReplaceBaseSlot] : [.. host.Shell.Replace.ReplaceSlots];
        Assert.Equal(slots.Count(static slot => slot.IsInputInspectionWarning), host.Model.WarningCount);
        Assert.Equal(slots.Count(static slot => slot.IsSemanticStateError), host.Model.ErrorCount);
        Assert.False(host.Model.CanRequestIssue);
    }

    /// <summary>General overlap uses canonical conflict coordinates and one admission count without parsing issue text.</summary>
    [AvaloniaFact]
    public async Task GeneralConflictKeepsCanonicalCoordinatesAndDetails()
    {
        using var workspace = TempWorkspace.Create("replace-layout-conflict");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadGeneralAsync(workspace);
        host.Shell.Replace.AddGeneralReplaceMappingCommand.Execute(null);
        GeneralReplaceMappingViewModel second = host.Shell.Replace.GeneralReplaceMappings[1];
        second.TargetStartAddress = "0x3E022";
        second.Length = "0x4";
        await host.Shell.WorkflowSession.SetSlotFileAsync(second.MappingId,
            workspace.Write("overlap.bin", [0x20, 0x21, 0x22, 0x23]), TestContext.Current.CancellationToken);
        await host.SettleAsync();
        MemoryCoverageSegmentViewModel conflict = Assert.Single(host.Model.RowSegments,
            static segment => segment.FillRole == MemoryCoverageFillRole.Conflict);
        Assert.Equal(0x3E022, conflict.RangeStart);
        Assert.Equal(0x3E024, conflict.RangeEndExclusive);
        Assert.Equal(MemoryDiagnosticSeverity.Error, conflict.DiagnosticSeverity);
        Assert.Equal(1, host.Model.ErrorCount);
        Assert.False(host.Shell.Replace.CanBuildReplace);
        host.Open(conflict);
        Assert.Contains(OutputLayoutPanelTestHost.Body(host.RowFor(conflict)).GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == conflict.PreservationSummary);
        Assert.Contains(host.Panel.FindControl<Canvas>("LayoutBar")!.Children,
            block => ReferenceEquals(block.DataContext, conflict));
    }

    /// <summary>A blocked non-geometric General mapping keeps the page's complete Build explanation.</summary>
    [AvaloniaFact]
    public async Task GeneralBlockedMappingUsesThePagesFullTypedReason()
    {
        using var workspace = TempWorkspace.Create("replace-layout-general-blocked");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadGeneralAsync(workspace);
        Assert.Single(host.Shell.Replace.GeneralReplaceMappings).TargetStartAddress = "0x50000";
        await host.SettleAsync();
        Assert.False(host.Shell.Replace.CanBuildReplace);
        Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
        Assert.Equal(host.Shell.ReplaceBuildBlockerCard.AutomationText, host.Model.Notice.Reason);
        Assert.Empty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.Equal(host.Shell.ReplaceBuildBlockerCard.AutomationText, host.Model.Notice!.Reason);
    }

    /// <summary>Finds one bank choice on the currently published panel.</summary>
    private static RadioButton BankButton(ReplaceOutputLayoutTestHost host, string bank)
    {
        return Assert.Single(host.Panel.GetVisualDescendants().OfType<RadioButton>(),
            button => AutomationProperties.GetAutomationId(button) == "OutputLayout.Bank." + bank);
    }
}
