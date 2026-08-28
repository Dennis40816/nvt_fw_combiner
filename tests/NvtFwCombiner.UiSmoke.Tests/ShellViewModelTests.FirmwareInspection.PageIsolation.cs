using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Merge IC/count changes cannot clear or rebuild the hidden Replace page state.</summary>
    [Fact]
    public void MergeDeviceContextChangeCannotMutateHiddenReplaceState()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";
        MemoryMapRowViewModel retainedMemoryRow = viewModel.Replace.ReplaceMemoryRows.First();
        string retainedOutputName = viewModel.Replace.ReplaceOutputFileName;

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Replace.ReplaceBaseSlot.FilePath = @"C:\hidden-replace.bin";
        viewModel.Replace.ReplaceBaseSlot.SetFirmwareFacts([new("Sentinel", "Replace")]);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;

        Assert.Equal(@"C:\hidden-replace.bin", viewModel.Replace.ReplaceBaseSlot.FilePath);
        Assert.Equal("Replace", Assert.Single(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts).Value);
        Assert.Contains(retainedMemoryRow, viewModel.Replace.ReplaceMemoryRows);
        Assert.Equal(retainedOutputName, viewModel.Replace.ReplaceOutputFileName);

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("2", viewModel.WorkflowSession.SelectedNumber);
        Assert.Contains(retainedMemoryRow, viewModel.Replace.ReplaceMemoryRows);
    }

    /// <summary>Replace IC/count changes cannot clear or rebuild the hidden Merge page state.</summary>
    [Fact]
    public void ReplaceDeviceContextChangeCannotMutateHiddenMergeState()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        MemoryMapRowViewModel retainedMemoryRow = viewModel.Merge.MergeMemoryRows.First();
        string retainedOutputName = viewModel.Merge.MergeOutputFileName;

        OpenReplace(viewModel, ExperienceIds.DpReplace);
        viewModel.Merge.MergeDpSlot.FilePath = @"C:\hidden-merge.bin";
        viewModel.Merge.MergeDpSlot.SetFirmwareFacts([new("Sentinel", "Merge")]);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";

        Assert.Equal(@"C:\hidden-merge.bin", viewModel.Merge.MergeDpSlot.FilePath);
        Assert.Equal("Merge", Assert.Single(viewModel.Merge.MergeDpSlot.FirmwareFacts).Value);
        Assert.Contains(retainedMemoryRow, viewModel.Merge.MergeMemoryRows);
        Assert.Equal(retainedOutputName, viewModel.Merge.MergeOutputFileName);

        viewModel.ShowMergeCommand.Execute(null);

        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);
        Assert.Contains(retainedMemoryRow, viewModel.Merge.MergeMemoryRows);
    }

    /// <summary>A hidden Replace mode change cannot cancel or invalidate the active Merge inspection.</summary>
    [Fact]
    public async Task HiddenReplaceModeChangeCannotInvalidateActiveMergeInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-hidden-replace-inspection");
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        int batches = 0;
        var readerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReader = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices services = PresentationTestHost.CreateServices("test");
        var viewModel = new MainWindowViewModel(
            "test",
            "test",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: (icId, inputs) =>
                {
                    batches++;
                    _ = readerEntered.TrySetResult();
                    releaseReader.Task.GetAwaiter().GetResult();
                    return BuiltInFirmwareInspection.InspectFirmwareBatch(
                        (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                        icId,
                        inputs);
                }));
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        try
        {
            await readerEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            WorkflowInspectionLifecycle standard = viewModel.Merge.InspectionLifecycles[
                ExperienceIds.StandardMerge];
            Assert.True(standard.IsRunning);

            viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;

            Assert.True(standard.IsRunning);
            Assert.Equal(1, batches);
        }
        finally
        {
            _ = releaseReader.TrySetResult();
        }

        await selection;
        Assert.Equal(
            WorkflowInspectionAttemptState.Succeeded,
            viewModel.Merge.InspectionLifecycles[ExperienceIds.StandardMerge].State);
    }

    /// <summary>A hidden Merge mode change cannot invalidate the active Replace inspection result.</summary>
    [Fact]
    public async Task HiddenMergeModeChangeCannotInvalidateActiveReplaceInspection()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        int batches = 0;
        var readerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReader = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices services = PresentationTestHost.CreateServices("test");
        var viewModel = new MainWindowViewModel(
            "test",
            "test",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: (icId, inputs) =>
                {
                    batches++;
                    _ = readerEntered.TrySetResult();
                    releaseReader.Task.GetAwaiter().GetResult();
                    return BuiltInFirmwareInspection.InspectFirmwareBatch(
                        (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                        icId,
                        inputs);
                }));
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.DpReplace);

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        try
        {
            await readerEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            WorkflowInspectionLifecycle dpReplace = viewModel.Replace.InspectionLifecycles[
                ExperienceIds.DpReplace];
            Assert.True(dpReplace.IsRunning);

            viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;

            Assert.True(dpReplace.IsRunning);
            Assert.Equal(1, batches);
        }
        finally
        {
            _ = releaseReader.TrySetResult();
        }

        await selection;
        Assert.Equal(
            WorkflowInspectionAttemptState.Succeeded,
            viewModel.Replace.InspectionLifecycles[ExperienceIds.DpReplace].State);
    }

    /// <summary>Selected-file admission rejects slots owned by the inactive page in both directions.</summary>
    [Fact]
    public async Task InactivePageSlotSelectionCannotEnterTheActiveInspectionRoute()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-inactive-page-inspection");
        string replacePath = workspace.Write("replace-base.bin", [0x01]);
        string mergePath = workspace.Write("merge-dp.bin", [0x02]);
        int batches = 0;
        MainWindowViewModel mergeViewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches++;
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection("0101"))),
            ];
        });
        mergeViewModel.ShowMergeCommand.Execute(null);

        await mergeViewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            replacePath,
            TestContext.Current.CancellationToken);

        Assert.Null(mergeViewModel.Replace.ReplaceBaseSlot.FilePath);
        Assert.Equal(0, batches);

        MainWindowViewModel replaceViewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches++;
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection("0202"))),
            ];
        });
        OpenReplace(replaceViewModel, ExperienceIds.DpReplace);

        await replaceViewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            mergePath,
            TestContext.Current.CancellationToken);

        Assert.Null(replaceViewModel.Merge.MergeDpSlot.FilePath);
        Assert.Equal(0, batches);
    }

    /// <summary>Non-workflow pages and wrong modes cannot admit hidden slot or mapping callbacks.</summary>
    [Fact]
    public async Task NonWorkflowPageAndWrongModeSelectionsAreRejected()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-wrong-context-inspection");
        string path = workspace.Write("input.bin", [0x01]);
        int batches = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches++;
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection("0101"))),
            ];
        });
        viewModel.ShowHomeCommand.Execute(null);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            path,
            TestContext.Current.CancellationToken);

        Assert.Null(viewModel.Merge.MergeDpSlot.FilePath);
        Assert.Equal(0, batches);

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            mapping.MappingId,
            path,
            TestContext.Current.CancellationToken);

        Assert.Null(mapping.FilePath);
        Assert.Equal(0, batches);
    }

    /// <summary>An AB callback for a slot removed from the current compiled topology is rejected.</summary>
    [Fact]
    public async Task FormerAbTopologySlotCannotReenterInspectionAdmission()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-former-ab-slot");
        string path = workspace.Write("former-tp-b.bin", [0x01]);
        int batches = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches++;
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection("0101"))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        FirmwareSlotViewModel former = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionAddressSpaceIds.TpBInput);
        Assert.True(viewModel.Merge.MergeSlots.Remove(former));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            former.SlotId,
            path,
            TestContext.Current.CancellationToken);

        Assert.Null(former.FilePath);
        Assert.Equal(0, batches);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>A Standard Merge callback for a slot outside current compiled membership is rejected.</summary>
    [Fact]
    public async Task FormerStandardMergeSlotCannotReenterInspectionAdmission()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-former-standard-slot");
        string path = workspace.Write("former-ldc.bin", [0x01]);
        int batches = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches++;
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection("0101"))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        FirmwareSlotViewModel former = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeLdc);
        Assert.True(viewModel.Merge.MergeSlots.Remove(former));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            former.SlotId,
            path,
            TestContext.Current.CancellationToken);

        Assert.Null(former.FilePath);
        Assert.Equal(0, batches);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>Confirmed Merge-to-Replace navigation prevents the obsolete Merge callback from publishing.</summary>
    [Fact]
    public async Task MergeInspectionCannotPublishAfterConfirmedReplaceNavigation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-merge-navigation-inspection");
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            readerEntered.Set();
            releaseReader.Wait(TestContext.Current.CancellationToken);
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        try
        {
            Assert.True(readerEntered.Wait(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken));
            viewModel.ShowReplaceCommand.Execute(null);
            Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);

            viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);

            Assert.True(viewModel.IsReplaceVisible);
            Assert.Null(viewModel.Merge.MergeDpSlot.FilePath);
        }
        finally
        {
            releaseReader.Set();
        }

        await selection;
        Assert.Equal(
            WorkflowInspectionAttemptState.Cancelled,
            viewModel.Merge.InspectionLifecycles[ExperienceIds.StandardMerge].State);
        Assert.Empty(viewModel.Merge.MergeDpSlot.FirmwareFacts);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>Confirmed Replace-to-Merge navigation prevents the obsolete Replace callback from publishing.</summary>
    [Fact]
    public async Task ReplaceInspectionCannotPublishAfterConfirmedMergeNavigation()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            readerEntered.Set();
            releaseReader.Wait(TestContext.Current.CancellationToken);
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.DpReplace);

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        try
        {
            Assert.True(readerEntered.Wait(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken));
            viewModel.ShowMergeCommand.Execute(null);
            Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);

            viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);

            Assert.True(viewModel.IsMergeVisible);
            Assert.Null(viewModel.Replace.ReplaceBaseSlot.FilePath);
        }
        finally
        {
            releaseReader.Set();
        }

        await selection;
        Assert.Equal(
            WorkflowInspectionAttemptState.Cancelled,
            viewModel.Replace.InspectionLifecycles[ExperienceIds.DpReplace].State);
        Assert.Empty(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>A live AB-filtered selector preserves independent Merge and Replace state across a canonical linked-TP round trip.</summary>
    /// <remarks>The owner's NT51950 2-IC AB scenario is represented by the profile-owned generic <c>cascade</c> topology token.</remarks>
    [AvaloniaFact]
    public async Task Nt51950CanonicalLinkedAbTpCanRoundTripThroughIndependentReplaceContext()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ab-merge",
            "nt51950-ab-boe-d82t80");
        JsonElement tpAArtifact = goldenCase.GetProperty("artifacts").EnumerateArray().Single(
            static artifact =>
                artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.TpAInput);
        string canonicalTpAPath = CanonicalGoldenTestData.ArtifactPath(tpAArtifact);
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);

        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        string replaceOutputBefore = viewModel.Replace.ReplaceOutputFileName;
        string replaceReadinessBefore = viewModel.Replace.ReplaceReadinessStatus;
        WorkflowInspectionLifecycle replaceInspection = viewModel.Replace.Inspection;
        WorkflowInspectionAttemptState replaceInspectionBefore = replaceInspection.State;
        (string SlotId, string? FilePath)[] replaceSlotsBefore =
        [
            .. viewModel.Replace.ReplaceSlots
                .Prepend(viewModel.Replace.ReplaceBaseSlot)
                .Select(static slot => (slot.SlotId, slot.FilePath)),
        ];
        void AssertReplaceStateUnchanged()
        {
            Assert.Equal(replaceSlotsBefore, ReplaceSlotPaths(viewModel));
            Assert.Equal(replaceOutputBefore, viewModel.Replace.ReplaceOutputFileName);
            Assert.Equal(replaceReadinessBefore, viewModel.Replace.ReplaceReadinessStatus);
            Assert.Equal(replaceInspectionBefore, replaceInspection.State);
        }

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        Assert.Equal(
            [IcNumberSelectionTokens.SingleChip, IcNumberSelectionTokens.Cascade],
            viewModel.WorkflowSession.NumberSelectionChoices.Select(static choice => choice.Token));
        Assert.Equal(
            "2 IC",
            Assert.Single(
                viewModel.WorkflowSession.NumberSelectionChoices,
                static choice => choice.Token == IcNumberSelectionTokens.Cascade).DisplayLabel);
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            canonicalTpAPath,
            TestContext.Current.CancellationToken);
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        WorkflowInspectionLifecycle mergeInspection = viewModel.Merge.Inspection;
        WorkflowInspectionAttemptState mergeInspectionBeforeNavigation = mergeInspection.State;
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.Equal(canonicalTpAPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Equal(canonicalTpAPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);
        Assert.False(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).CanSelectFile);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, mergeInspectionBeforeNavigation);
        Assert.Equal("NT51950", selector.SelectedItem);
        Assert.Equal("NT51950", viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        Assert.Equal(
            IcNumberSelectionTokens.Cascade,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Merge));
        Assert.Equal("NT51926", viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace));
        Assert.Equal(
            IcNumberSelectionTokens.SingleChip,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Replace));
        AssertReplaceStateUnchanged();
        Assert.NotSame(mergeInspection, replaceInspection);

        viewModel.ShowReplaceCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal("NT51926", selector.SelectedItem);
        Assert.Contains("NT51926", selector.Items.Cast<string>());
        AssertReplaceStateUnchanged();
        Assert.Equal(mergeInspectionBeforeNavigation, mergeInspection.State);
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceSlots.Prepend(viewModel.Replace.ReplaceBaseSlot),
            static slot => slot.HasFile);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);

        viewModel.ShowMergeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsMergeVisible);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(
            IcNumberSelectionTokens.Cascade,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Merge));
        Assert.Equal("NT51950", selector.SelectedItem);
        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.False(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).CanSelectFile);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
        Assert.Null(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Null(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);
        Assert.Equal(mergeInspectionBeforeNavigation, mergeInspection.State);
        Assert.NotEqual(replaceReadinessBefore, viewModel.Merge.MergeReadinessStatus);
        Assert.NotEqual(replaceOutputBefore, viewModel.Merge.MergeOutputFileName);
        AssertReplaceStateUnchanged();
    }

    private static (string SlotId, string? FilePath)[] ReplaceSlotPaths(MainWindowViewModel viewModel)
    {
        return
        [
            .. viewModel.Replace.ReplaceSlots
                .Prepend(viewModel.Replace.ReplaceBaseSlot)
                .Select(static slot => (slot.SlotId, slot.FilePath)),
        ];
    }

    private static string StandardMergeDraftSignature(MainWindowViewModel viewModel)
    {
        return string.Join(
            '|',
            viewModel.Merge.SelectedMergeMode,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge),
            viewModel.Merge.MergeDpSlot.FilePath ?? string.Empty,
            viewModel.Merge.StandardMergeOutputFileName);
    }

    private static string AbMergeDraftSignature(MainWindowViewModel viewModel)
    {
        return string.Join(
            '|',
            viewModel.Merge.SelectedMergeMode,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge),
            viewModel.Merge.UseSameTpForAbMerge,
            viewModel.Merge.AbMergeOutputFileName);
    }

    private static string GeneralMergeDraftSignature(MainWindowViewModel viewModel)
    {
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        return string.Join(
            '|',
            viewModel.Merge.SelectedMergeMode,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge),
            viewModel.Merge.GeneralMergeOutputLength,
            viewModel.Merge.GeneralMergeOutputFillByte,
            mapping.MappingId,
            mapping.SourceStartAddress,
            mapping.TargetStartAddress,
            mapping.Length);
    }

    private static string CtrlRamReplaceDraftSignature(MainWindowViewModel viewModel)
    {
        return string.Join(
            '|',
            viewModel.Replace.SelectedReplaceMode,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace),
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Replace),
            viewModel.Replace.ReplaceOutputFileName);
    }

    private static string GeneralReplaceDraftSignature(MainWindowViewModel viewModel)
    {
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        return string.Join(
            '|',
            viewModel.Replace.SelectedReplaceMode,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace),
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Replace),
            mapping.MappingId,
            mapping.TargetStartAddress,
            mapping.Length);
    }

    private static string ActiveWorkflowDraftSignature(MainWindowViewModel viewModel)
    {
        return viewModel.IsMergeVisible
            ? viewModel.Merge.SelectedMergeMode switch
            {
                ExperienceIds.AbMerge => AbMergeDraftSignature(viewModel),
                ExperienceIds.GeneralMerge => GeneralMergeDraftSignature(viewModel),
                _ => StandardMergeDraftSignature(viewModel),
            }
            : viewModel.Replace.SelectedReplaceMode == ExperienceIds.GeneralReplace
                ? GeneralReplaceDraftSignature(viewModel)
                : CtrlRamReplaceDraftSignature(viewModel);
    }
}
