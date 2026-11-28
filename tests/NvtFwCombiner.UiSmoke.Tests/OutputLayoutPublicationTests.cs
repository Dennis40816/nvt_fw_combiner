// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Legacy coverage and the unused output map share one complete revision-bound publication.</summary>
public sealed class OutputLayoutPublicationTests
{
    /// <summary>A new revision replaces map, coverage, addresses and details in one observable change.</summary>
    [Fact]
    public async Task AcceptedRevisionReplacesTheCompleteDisplayOnce()
    {
        using var workspace = TempWorkspace.Create("output-layout-revision");
        string path = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        PresentationCompositionServices services = PresentationTestHost.CreateServices("1.3.0").Composition;
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        ActiveSessionSnapshot first = await PrepareGeneralAsync(services, session, path, "0x20", "0x4");
        var child = new OutputLayoutPresentationViewModel();
        PublishGeneral(child, services, first, ShellLanguage.English);
        OutputLayoutPublication previous = child.Current;
        ActiveSessionSnapshot second = await PrepareGeneralAsync(services, session, path, "0x40", "0x8");
        List<OutputLayoutPublication> observed = [];
        child.PropertyChanged += (_, args) =>
        {
            Assert.Equal(nameof(OutputLayoutPresentationViewModel.Current), args.PropertyName);
            observed.Add(child.Current);
        };

        PublishGeneral(child, services, second, ShellLanguage.English);

        OutputLayoutPublication current = Assert.Single(observed);
        Assert.Same(current, child.Current);
        Assert.NotSame(previous, current);
        Assert.Equal(second.AuthoringRevision, current.Revision);
        Assert.NotEqual(previous.Revision, current.Revision);
        OutputMapModel map = Assert.IsType<OutputMapModel>(current.Map);
        OutputMapBlock copy = Assert.Single(map.Blocks, static block => block.Kind == OutputMapBlockKind.Copy);
        Assert.Equal(0x40, map.OutputLength);
        Assert.Equal(new ByteRange(8, 4), copy.TargetRange);
        Assert.Equal(new ByteRange(0, 4), copy.SourceRange);
        Assert.Equal("source.bin", Assert.Single(map.Sources).FileName);
        MemoryCoverageSegmentViewModel coverage = Assert.Single(current.CoverageSegments,
            segment => segment.RangeStart == copy.TargetRange.Start &&
                segment.RangeEndExclusive == copy.TargetRange.EndExclusive);
        Assert.NotEmpty(coverage.Detail);
        MemoryCoverageSegmentViewModel detail = Assert.Single(current.CoverageRows,
            row => row.RangeStart == 8 && row.RangeEndExclusive == 12);
        Assert.Same(coverage.Interaction, detail.Interaction);
        Assert.Contains(current.Rows, row => row.RangeLabel == coverage.RangeLabel && row.Detail == coverage.Detail);
        Assert.Equal("0x00000", current.StartAddress);
        Assert.Equal("0x0003F", current.EndAddress);
        Assert.Equal("0x0001F", previous.EndAddress);
        Assert.Equal(0x20, Assert.IsType<OutputMapModel>(previous.Map).OutputLength);
        Assert.Contains(previous.CoverageSegments, static segment => segment.RangeStart == 4 && segment.RangeEndExclusive == 8);
    }

    /// <summary>Pending input clears the accepted map while keeping the original waiting text and details.</summary>
    [Fact]
    public async Task PendingPublicationReplacesAcceptedGeometryWithWaitingDetails()
    {
        using var workspace = TempWorkspace.Create("output-layout-pending");
        PresentationCompositionServices services = PresentationTestHost.CreateServices("1.3.0").Composition;
        ActiveSessionSnapshot accepted = await PrepareGeneralAsync(services,
            new AuthoringSessionState(ExperienceIds.GeneralMerge),
            workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]), "0x20", "0x4");
        var child = new OutputLayoutPresentationViewModel();
        PublishGeneral(child, services, accepted, ShellLanguage.English);
        Assert.NotNull(child.Current.Map);

        PublishGeneral(child, services, null, ShellLanguage.English);

        OutputLayoutPublication pending = child.Current;
        Assert.Null(pending.Map);
        Assert.Null(pending.Revision);
        Assert.Equal("Waiting for source mapping", pending.RangeLabel);
        MemoryCoverageSegmentViewModel coverage = Assert.Single(pending.CoverageSegments);
        Assert.Null(coverage.RangeStart);
        Assert.Equal(pending.RangeLabel, coverage.SourceLabel);
        Assert.Contains("source BIN", coverage.Detail, StringComparison.Ordinal);
        Assert.Equal(coverage.Detail, Assert.Single(pending.Rows).Detail);
        Assert.NotEmpty(pending.CoverageRows);
    }

    /// <summary>Typed blocked status keeps its error, action and localized detail without a map.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlockedPublicationRetainsTheLegacyError(bool chinese)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var slot = new FirmwareSlotViewModel("dp", "DP BIN", "DP input", FirmwareSlotKind.Dp,
            addressSpaceId: "dp-input")
        { FilePath = "dp.bin" };
        var status = new AuthoringInputSlotStatus(
            new CapabilityRouteIdentity("NT-SYNTHETIC", ExperienceIds.StandardMerge, "none", "default"),
            new ResolutionToken("layout-blocked"), new AuthoringRevision(1), new string('a', 64), null,
            new InputSelectionMemberReadiness("dp-input", true, ResolvedChildReadiness.Blocked, false,
                "Unresolved prerequisite", null), "dp-input", null, null, null, "dp.bin");
        slot.ApplyExperienceText(text);
        FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, text);
        var child = new OutputLayoutPresentationViewModel();

        child.Publish(PresentationTestHost.CreateServices("1.3.0").Composition, null, text,
            [slot], MemoryPendingPrerequisite.DpBin);

        Assert.Null(child.Current.Map);
        MemoryCoverageSegmentViewModel coverage = Assert.Single(child.Current.CoverageSegments);
        Assert.Equal(MemoryDiagnosticSeverity.Error, coverage.DiagnosticSeverity);
        Assert.Equal(MemoryCoverageFillRole.Conflict, coverage.FillRole);
        Assert.Equal(text.GetDpInputSelectionReadinessDetail(status.SelectionReadiness), coverage.Detail);
        Assert.Equal(MemoryPlanActionKind.Blocked, Assert.Single(child.Current.Rows).Action);
        Assert.Null(coverage.RangeStart);
    }

    /// <summary>Language and mode switches publish fresh legacy text and cannot retain another mode's map.</summary>
    [AvaloniaFact]
    public async Task LanguageAndModeSwitchesKeepOneDisplayOwner()
    {
        using var workspace = TempWorkspace.Create("output-layout-language-mode");
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowMergeCommand.Execute(null);
        shell.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        shell.Merge.GeneralMergeOutputLength = "0x20";
        GeneralMergeMappingViewModel mapping = Assert.Single(shell.Merge.GeneralMergeMappings);
        mapping.Length = "0x4";
        await shell.WorkflowSession.SetSlotFileAsync(mapping.MappingId,
            workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]), TestContext.Current.CancellationToken);
        await shell.Merge.Inspection.ActiveTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        OutputLayoutPresentationViewModel child = shell.Merge.OutputLayout;
        OutputLayoutPublication english = child.Current;
        OutputMapModel englishMap = Assert.IsType<OutputMapModel>(english.Map);

        shell.SelectedLanguage = "Traditional Chinese";

        OutputLayoutPublication chinese = child.Current;
        Assert.NotSame(english, chinese);
        Assert.Equal(english.Revision, chinese.Revision);
        Assert.Equal(englishMap.OutputLength, Assert.IsType<OutputMapModel>(chinese.Map).OutputLength);
        Assert.Contains("保留值", shell.Merge.OutputLayout.Current.Panel!.SummaryText, StringComparison.Ordinal);
        Assert.NotEqual(english.CoverageSegments[0].Detail, chinese.CoverageSegments[0].Detail);

        shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;

        Assert.Same(child, shell.Merge.OutputLayout);
        Assert.Null(child.Current.Map);
        Assert.NotSame(chinese, child.Current);

        shell.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        await shell.Merge.Inspection.ActiveTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.NotNull(child.Current.Map);
        Assert.Equal("0x0001F", child.Current.EndAddress);
        _ = Assert.Single(shell.Merge.GeneralMergeMappings);
        Assert.Same(mapping, shell.Merge.GeneralMergeMappings[0]);
    }

    /// <summary>Unchanged context notification does not create or replace a panel publication.</summary>
    [AvaloniaFact]
    public void ContextNotificationKeepsTheCurrentPanelPublication()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowMergeCommand.Execute(null);
        OutputLayoutPublication current = shell.Merge.OutputLayout.Current;
        shell.Merge.PublishAcceptedModeContext();
        Assert.Same(current, shell.Merge.OutputLayout.Current);
        Assert.Same(current.Panel, shell.Merge.OutputLayout.Current.Panel);
    }

    /// <summary>A map refresh replaces the complete panel input exactly once.</summary>
    [AvaloniaFact]
    public void MapRefreshPublishesOnePanelModel()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowMergeCommand.Execute(null);
        List<OutputLayoutPublication> observed = [];
        shell.Merge.OutputLayout.PropertyChanged += (_, args) =>
        {
            Assert.Equal(nameof(OutputLayoutPresentationViewModel.Current), args.PropertyName);
            observed.Add(shell.Merge.OutputLayout.Current);
        };
        shell.Merge.RefreshMergeMemoryMapState();
        OutputLayoutPublication publication = Assert.Single(observed);
        Assert.Same(publication, shell.Merge.OutputLayout.Current);
        Assert.NotNull(publication.Panel);
        Assert.Equal(publication.CoverageSegments, publication.Panel.Segments);
        Assert.Equal(publication.CoverageRows, publication.Panel.CoverageRows);
        Assert.Equal(publication.RangeLabel, publication.Panel.RangeLabel);
    }

    /// <summary>An accepted scratch-space layout keeps legacy ownership and initialization when the adapter returns null.</summary>
    [Fact]
    public void AdapterNullKeepsTheCompleteLegacyDisplay()
    {
        var host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        byte[] tp = new byte[0x40000];
        tp[0x36001] = 0xFF;
        tp[0x36017] = 1;
        byte[] signature = [0, 0x4E, 0x56, 0x54];
        signature.CopyTo(tp, 0x36FFC);
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new(CompositionAddressSpaceIds.TpAInput, "a.bin", tp), new(CompositionAddressSpaceIds.TpBInput, "b.bin", tp)],
            AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        PresentationCompositionServices services = PresentationTestHost.CreateServices("1.3.0", host, static authoring => authoring).Composition;
        var child = new OutputLayoutPresentationViewModel();
        var text = ShellTextResources.For(ShellLanguage.English);

        child.Publish(services, prepared.Snapshot, text, [], MemoryPendingPrerequisite.DpBin);

        Assert.Null(child.Current.Map);
        Assert.Equal(prepared.Snapshot!.AuthoringRevision, child.Current.Revision);
        Assert.Contains(child.Current.Rows, static row => row.AfterSource == "TPA");
        Assert.Contains(child.Current.Rows, static row => row.AfterSource == "TPB");
        Assert.Contains(child.Current.CoverageSegments, segment => segment.SourceFieldLabel == text.MemoryInitializationLabel);
        Assert.NotEmpty(child.Current.CoverageRows);
        Assert.All(child.Current.CoverageSegments, static segment => Assert.NotEmpty(segment.Detail));
        Assert.Equal("0x00000", child.Current.StartAddress);
        Assert.NotEqual(text.NotAvailableLabel, child.Current.EndAddress);
    }

    private static void PublishGeneral(OutputLayoutPresentationViewModel child,
        PresentationCompositionServices services, ActiveSessionSnapshot? accepted, ShellLanguage language)
    {
        child.Publish(services, accepted, ShellTextResources.For(language), [],
            MemoryPendingPrerequisite.GeneralMergeSourceMapping);
    }

    private static async Task<ActiveSessionSnapshot> PrepareGeneralAsync(PresentationCompositionServices services,
        AuthoringSessionState session, string path, string outputLength, string targetStart)
    {
        AuthoringMappingState mapping = GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
            "map-1", path, "0x0", targetStart, "0x4");
        Assert.True(GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft([mapping],
            out GeneralMappingDraftState? mappings, out _));
        Assert.True(GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(outputLength, null,
            out GeneralMergeInitializer? initializer));
        GeneralAuthoringSessionPreparation prepared = await services.GeneralAuthoring.PrepareMergeSessionAsync(
            session, "NT51926", GeneralMergeAuthoringUseCase.CreateDraft(initializer, mappings),
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues));
        return Assert.IsType<ActiveSessionSnapshot>(prepared.AcceptedSession);
    }
}
