using System.Text.Json;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Clear removes one Standard Merge identity and rebuilds retained typed input state.</summary>
    [Fact]
    public async Task StandardMergeSlotClearRetainsPeerAndNeverDeletesSource()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            tpPath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);

        Assert.True(viewModel.Merge.CanBuildMerge);
        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded);
        CompositionRunContext completed = viewModel.Merge.CaptureRunContext(ExperienceIds.StandardMerge);
        Assert.True(completed.IsPublicationCurrent);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            dp.SlotId,
            TestContext.Current.CancellationToken);

        Assert.False(dp.HasFile);
        Assert.Empty(dp.FirmwareFacts);
        Assert.False(dp.HasInputInspectionStatus);
        Assert.Null(dp.CurrentInspectionProjection);
        Assert.True(tp.HasFile);
        Assert.Equal(tpPath, tp.FilePath);
        Assert.False(tp.IsInputInspectionPending);
        Assert.True(File.Exists(dpPath));
        Assert.True(File.Exists(tpPath));
        Assert.False(completed.IsPublicationCurrent);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Equal("Context changed", viewModel.RunSession.LastRunResult.Title);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Clearing a compiler prerequisite cannot leave a retained dependent's old accepted projection.</summary>
    [Fact]
    public async Task StandardMergePrerequisiteClearInvalidatesRetainedDependentProjection()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51950");
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            tpPath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);

        Assert.NotNull(tp.CurrentInspectionProjection);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            dp.SlotId,
            TestContext.Current.CancellationToken);

        Assert.False(dp.HasFile);
        Assert.True(tp.HasFile);
        Assert.Equal(ResolvedChildReadiness.PendingInput, tp.SelectionReadinessState);
        Assert.Contains("DP", tp.SelectionReadinessDetail, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.Merge.CanBuildMerge);
        Assert.True(File.Exists(tpPath));
    }

    /// <summary>AB Clear targets only one independent slot, but either linked TP action clears the pair.</summary>
    [Fact]
    public async Task AbSlotClearHonorsIndependentAndLinkedOwnership()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-slot-clear");
        string tpAPath = workspace.Write(
            "tp-a.bin",
            CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102));
        string tpBPath = workspace.Write(
            "tp-b.bin",
            CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C));
        MainWindowViewModel viewModel = PrepareAbSameTpViewModel();
        FirmwareSlotViewModel tpA = AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput);
        FirmwareSlotViewModel tpB = AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            tpA.SlotId,
            tpAPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            tpB.SlotId,
            tpBPath,
            TestContext.Current.CancellationToken);

        await viewModel.WorkflowSession.ClearSlotFileAsync(
            tpA.SlotId,
            TestContext.Current.CancellationToken);

        Assert.False(tpA.HasFile);
        Assert.Equal(tpBPath, tpB.FilePath);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.Equal(tpBPath, tpA.FilePath);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            tpB.SlotId,
            TestContext.Current.CancellationToken);

        Assert.All([tpA, tpB], static linkedSlot =>
        {
            Assert.False(linkedSlot.HasFile);
            Assert.Empty(linkedSlot.FirmwareFacts);
            Assert.False(linkedSlot.HasInputInspectionStatus);
            Assert.Null(linkedSlot.CurrentInspectionProjection);
        });
        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.True(tpA.CanSelectFile);
        Assert.False(tpB.CanSelectFile);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            tpA.SlotId,
            tpAPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(tpAPath, tpA.FilePath);
        Assert.Equal(tpAPath, tpB.FilePath);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            tpA.SlotId,
            TestContext.Current.CancellationToken);

        Assert.All([tpA, tpB], static linkedSlot =>
        {
            Assert.False(linkedSlot.HasFile);
            Assert.Empty(linkedSlot.FirmwareFacts);
            Assert.False(linkedSlot.HasInputInspectionStatus);
            Assert.Null(linkedSlot.CurrentInspectionProjection);
        });
        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.True(tpA.CanSelectFile);
        Assert.False(tpB.CanSelectFile);
        Assert.True(File.Exists(tpAPath));
        Assert.True(File.Exists(tpBPath));
    }

    /// <summary>General Replace Base Clear invalidates acceptance while preserving the mapping selection and both source files.</summary>
    [Fact]
    public async Task GeneralReplaceBaseClearPreservesMappingAndInvalidatesAcceptance()
    {
        using var workspace = TempWorkspace.Create("general-replace-slot-clear");
        string basePath = workspace.Write("reference.bin", CreatePattern(0x40000, 0x26));
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase, basePath, TestContext.Current.CancellationToken);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        string mappingId = mapping.MappingId;
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            mappingId, replacementPath, TestContext.Current.CancellationToken);
        await viewModel.Replace.Inspection.ActiveTask;
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        _ = Assert.NotNull(mapping.AcceptedFileStamp);
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded);
        CompositionRunContext completed = viewModel.Replace.CaptureRunContext(ExperienceIds.GeneralReplace);
        Assert.NotNull(completed.AcceptedSession);
        Assert.True(completed.IsPublicationCurrent);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            CompositionSlotIds.ReplaceBase, TestContext.Current.CancellationToken);

        Assert.False(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.Null(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        Assert.Null(viewModel.Replace.ReplaceBaseSlot.InputInspectionSeverity);
        Assert.Same(mapping, Assert.Single(viewModel.Replace.GeneralReplaceMappings));
        Assert.Equal(mappingId, mapping.MappingId);
        Assert.Equal(replacementPath, mapping.FilePath);
        Assert.Equal("0x3E020", mapping.TargetStartAddress);
        Assert.Equal("0x2", mapping.Length);
        Assert.False(completed.IsPublicationCurrent);
        ActiveSessionSnapshot? cleared = viewModel.Replace
            .CaptureRunContext(ExperienceIds.GeneralReplace).AcceptedSession;
        Assert.NotNull(cleared);
        string? referenceSpaceId = cleared.ExactCapability?.CompiledComposition
            .Plan.OutputInitialization.ReferenceSpaceId;
        Assert.NotNull(referenceSpaceId);
        AuthoringSlotState reference = Assert.Single(cleared.Slots, slot =>
            StringComparer.Ordinal.Equals(slot.DefinitionId, referenceSpaceId));
        Assert.Equal(AuthoringSlotLifecycle.Empty, reference.Lifecycle);
        Assert.Null(reference.SelectedPath);
        Assert.Null(reference.FileStamp);
        Assert.True(reference.AcceptedBytes.GetValueOrDefault().IsEmpty);
        Assert.False(cleared.HasCurrentInputInspection);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Equal("Context changed", viewModel.RunSession.LastRunResult.Title);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
        Assert.True(File.Exists(basePath));
        Assert.True(File.Exists(replacementPath));
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            CompositionSlotIds.ReplaceBase, TestContext.Current.CancellationToken);
        Assert.Same(cleared, viewModel.Replace
            .CaptureRunContext(ExperienceIds.GeneralReplace).AcceptedSession);
    }

    /// <summary>CtrlRAM region Clear retains the accepted Base and recomputes replacement readiness.</summary>
    [Fact]
    public async Task CtrlRamRegionClearRetainsBaseAndNeverDeletesSource()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-slot-clear");
        JsonElement goldenCase = golden.CaseByIc("51926");
        string basePath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(
            static slot => slot.ReplaceInputRole == ReplaceInputRole.CtrlRam);
        string replacementSlotId = replacement.SlotId;
        string replacementPath = workspace.Write("vn-ctrlram.bin", new byte[0x100]);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacementSlotId,
            replacementPath,
            TestContext.Current.CancellationToken);

        replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacementSlotId));
        Assert.True(replacement.HasFile);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            replacementSlotId,
            TestContext.Current.CancellationToken);

        replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacementSlotId));
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.False(replacement.HasFile);
        Assert.Null(replacement.CurrentInspectionProjection);
        Assert.True(File.Exists(replacementPath));
        Assert.False(viewModel.Replace.CanBuildReplace);

        replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacementSlotId));
        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacementSlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacementSlotId) && slot.HasFile);

        await viewModel.WorkflowSession.ClearSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.All(viewModel.Replace.ReplaceSlots, static slot => Assert.False(slot.HasFile));
        Assert.True(File.Exists(basePath));
        Assert.True(File.Exists(replacementPath));
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>The shared selected-slot card exposes the approved localized trash action and wires it once.</summary>
    [AvaloniaFact]
    public async Task SelectedSlotCardClearActionUsesApprovedSharedControl()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(static candidate =>
            candidate.SlotId == CompositionSlotIds.MergeDp);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = viewModel.Text.BrowseLabel,
            ClearSelectionLabel = viewModel.Text.ClearFirmwareSelectionLabel,
            ClearSelectionCommand = viewModel.WorkflowSession.ClearSlotFileCommand,
            DataContext = slot,
        };
        var window = new Window { DataContext = viewModel, Content = card };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button clear = Assert.IsType<Button>(card.FindControl<Control>("ClearButton"));

            Assert.True(clear.IsVisible);
            Assert.True(clear.IsEnabled);
            Assert.Contains("slotClearAction", clear.Classes);
            Assert.Contains("danger", clear.Classes);
            Assert.Equal(
                $"{viewModel.Text.ClearFirmwareSelectionLabel} — {slot.Title}",
                clear.GetValue(Avalonia.Automation.AutomationProperties.NameProperty));
            Assert.Equal(
                clear.GetValue(Avalonia.Automation.AutomationProperties.NameProperty),
                ToolTip.GetTip(clear));
            Assert.NotNull(clear.Command);
            ICommand clearCommand = clear.Command;
            Assert.Same(viewModel.WorkflowSession.ClearSlotFileCommand, clearCommand);
            Assert.Equal(slot.SlotId, clear.CommandParameter);

            clearCommand.Execute(clear.CommandParameter);
            Task clearExecution = Assert.IsType<Task>(
                viewModel.WorkflowSession.ClearSlotFileCommand.ExecutionTask,
                exactMatch: false);
            await clearExecution.WaitAsync(
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.False(slot.HasFile);
            Assert.True(clear.IsVisible);
            Assert.False(clear.IsEnabled);
            Assert.True(File.Exists(dpPath));
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(window.Close);
            await Dispatcher.UIThread.InvokeAsync(static () => { });
        }
    }
}
