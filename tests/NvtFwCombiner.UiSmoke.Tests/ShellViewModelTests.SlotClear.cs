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
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            dp.SlotId,
            TestContext.Current.CancellationToken);

        Assert.False(dp.HasFile);
        Assert.Empty(dp.FirmwareFacts);
        Assert.False(dp.HasInputInspectionStatus);
        Assert.Null(dp.CurrentInspectionProjection);
        Assert.True(tp.HasFile);
        Assert.False(tp.IsInputInspectionPending);
        Assert.True(File.Exists(dpPath));
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

    /// <summary>DP Replace Clear preserves the peer and fails closed when either required identity is absent.</summary>
    [Fact]
    public async Task DpReplaceSlotClearPreservesPeerAndRecomputesDependentReadiness()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-replace-slot-clear");
        string basePath = workspace.Write("reference.bin", CreatePattern(0x40000, 0x71));
        string replacementPath = workspace.Write(
            "initial-code.bin",
            CreatePattern(0x40000, 0x41));
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceDp,
            replacementPath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.ReplaceDp);

        Assert.True(viewModel.Replace.CanBuildReplace);
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            replacement.SlotId,
            TestContext.Current.CancellationToken);

        Assert.True(baseSlot.HasFile);
        Assert.False(replacement.HasFile);
        Assert.Empty(replacement.FirmwareFacts);
        Assert.False(replacement.HasInputInspectionStatus);
        Assert.Null(replacement.CurrentInspectionProjection);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Equal("Context changed", viewModel.RunSession.LastRunResult.Title);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
        Assert.True(File.Exists(basePath));
        Assert.True(File.Exists(replacementPath));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace);
        await viewModel.WorkflowSession.ClearSlotFileAsync(
            baseSlot.SlotId,
            TestContext.Current.CancellationToken);

        Assert.False(baseSlot.HasFile);
        Assert.True(replacement.HasFile);
        FirmwareInspectionSnapshot pendingProjection = Assert.IsType<FirmwareInspectionSnapshot>(
            replacement.CurrentInspectionProjection);
        AuthoringInputSlotStatus pendingStatus = Assert.IsType<AuthoringInputSlotStatus>(
            pendingProjection.InputSlotStatus);
        Assert.Null(pendingStatus.CompilationFingerprint);
        Assert.Equal(ResolvedChildReadiness.Blocked, pendingStatus.Readiness);
        Assert.Equal(ResolvedChildReadiness.PendingInput, replacement.SelectionReadinessState);
        Assert.Contains(
            "Reference",
            replacement.SelectionReadinessDetail,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.True(File.Exists(basePath));
        Assert.True(File.Exists(replacementPath));
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
