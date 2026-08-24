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
}
