using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>
/// R56 regressions for R12-02 H2/F02: Cancel invalidates a pending CtrlRAM Confirm,
/// restores its draft, and guards later confirmations and bank switches. Output checks
/// compare FWConfig version metadata; they do not establish complete-image byte parity.
/// </summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed class R12H2CtrlRamCancelTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Cancel during readiness closes the confirmation permanently and executes neither delivery kind.
    /// </summary>
    /// <param name="bundle">Whether Confirm requests bundle delivery.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R12H2aCancelDuringReadinessDoesNotReopenOrExecute(bool bundle)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = TempWorkspace.Create("r12-h2a-readiness");
        Harness harness = CreateHarness();
        MainWindowViewModel viewModel = harness.ViewModel;
        PrepareStandardReady(viewModel, workspace);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        Assert.True(viewModel.OutputDelivery.IsOpen);
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        viewModel.OutputDelivery.SetBundleEnabled(bundle);
        viewModel.OutputDelivery.SetParentDirectory(workspace.Root);
        viewModel.OutputDelivery.SetBundleFolderName("cancelled-bundle");
        string outputPath = workspace.PathFor("cancelled.bin");

        Task readinessHeld = harness.Readiness.Arm();
        Task firstConfirm = bundle
            ? viewModel.OutputDelivery.ConfirmBundleAsync()
            : viewModel.OutputDelivery.ConfirmLooseAsync(outputPath, null, false, false);
        await readinessHeld.WaitAsync(Wait, cancellationToken);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        Assert.False(viewModel.OutputDelivery.IsOpen);
        harness.Readiness.Release();

        await firstConfirm.WaitAsync(Wait, cancellationToken);
        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.Empty(harness.Execution.Requests);
        Assert.False(File.Exists(outputPath));
        Assert.False(Directory.Exists(workspace.PathFor("cancelled-bundle")));
    }

    /// <summary>
    /// Cancel during successor naming refuses the old confirmation and restores the draft.
    /// </summary>
    [Fact]
    public async Task R12H2aCancelDuringNamingDoesNotReopen()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = TempWorkspace.Create("r12-h2a-naming");
        Harness harness = CreateHarness();
        MainWindowViewModel viewModel = harness.ViewModel;
        PrepareStandardReady(viewModel, workspace);
        CtrlRamAuthoringDraftState? previousDraft = viewModel.Replace.CurrentCtrlRamDraft;
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";

        Task namingHeld = harness.Naming.Arm();
        Task<bool> firstConfirm = viewModel.OutputDelivery.PrepareModeSpecificAsync();
        await namingHeld.WaitAsync(Wait, cancellationToken);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        harness.Naming.Release();

        Assert.False(await firstConfirm.WaitAsync(Wait, cancellationToken));
        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.Empty(harness.Execution.Requests);
        Assert.Equal(previousDraft, viewModel.Replace.CurrentCtrlRamDraft);
        Assert.Equal(previousDraft, Assert.IsType<ActiveSessionSnapshot>(harness.Readiness.LastSession).DraftState);
    }

    /// <summary>
    /// A modal reopened while cancelled naming is held keeps its lease and writes its Keep/Edit choice.
    /// </summary>
    /// <param name="edit">Whether the new confirmation edits the version.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R12H2NamingCancelReopenBeforeReleaseKeepsNewConfirmationCurrent(bool edit)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = TempWorkspace.Create($"r56-naming-reopen-{edit}");
        Harness harness = CreateHarness();
        MainWindowViewModel viewModel = harness.ViewModel;
        PrepareStandardReady(viewModel, workspace);
        string sourcePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        FirmwareConfigMetadataSnapshot source = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(TestProjection, "NT51926", sourcePath));
        CtrlRamAuthoringDraftState? previousDraft = viewModel.Replace.CurrentCtrlRamDraft;
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";

        Task namingHeld = harness.Naming.Arm();
        Task<bool> firstConfirm = viewModel.OutputDelivery.PrepareModeSpecificAsync();
        await namingHeld.WaitAsync(Wait, cancellationToken);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        Assert.False(firstConfirm.IsCompleted);
        Assert.Equal(previousDraft, viewModel.Replace.CurrentCtrlRamDraft);
        ActiveSessionSnapshot restored = Assert.IsType<ActiveSessionSnapshot>(harness.Readiness.LastSession);
        Assert.Equal(previousDraft, restored.DraftState);
        Assert.Equal(2, harness.Readiness.VersionTransitions);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        if (edit)
        {
            viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
            viewModel.Replace.CtrlRamFirmwareVersionText = "33";
            viewModel.Replace.CtrlRamFirmwareSubVersionText = "44";
        }
        else
        {
            viewModel.Replace.SelectCtrlRamFirmwareVersionPreserveCommand.Execute(null);
        }
        long newPreparation = viewModel.OutputDelivery.PreparationGeneration;
        harness.Naming.Release();

        Assert.False(await firstConfirm.WaitAsync(Wait, cancellationToken));
        Assert.Equal(2, harness.Readiness.VersionTransitions);
        Assert.Same(restored, harness.Readiness.LastSession);
        Assert.Equal(newPreparation, viewModel.OutputDelivery.PreparationGeneration);
        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.Replace.CanConfirmCtrlRamFirmwareVersion);
        Assert.Equal(edit, viewModel.Replace.IsCtrlRamFirmwareVersionEditSelected);
        Assert.Empty(harness.Execution.Requests);

        string outputPath = await ConfirmAsync(viewModel, workspace, bundle: false);
        AcceptedCompositionExecutionRequest executed = Assert.Single(harness.Execution.Requests);
        Assert.Equal(edit ? new CtrlRamFirmwareVersionDraftState(0x33, 0x44) : null,
            executed.AcceptedSession.DraftState);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        FirmwareConfigMetadataSnapshot output = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(TestProjection, "NT51926", outputPath));
        Assert.Equal(edit ? 0x33 : source.FirmwareVersion, output.FirmwareVersion);
        Assert.Equal(edit ? 0x44 : source.FirmwareSubVersion, output.FirmwareSubVersion);
        Assert.Equal(edit ? 0xCC : source.FirmwareVersionBar, output.FirmwareVersionBar);
        Assert.False(viewModel.OutputDelivery.IsOpen);
    }

    /// <summary>
    /// A bank switch before cancelled readiness resumes keeps the old B-version in Preview's
    /// accepted draft; the next Build's B-bank FWConfig metadata follows its new Keep/Edit choice.
    /// </summary>
    /// <param name="edit">Whether the next Build edits the B-version.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R12H2AbCancelSwitchBankBeforeReleaseDoesNotCarryCancelledVersion(bool edit)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = TempWorkspace.Create($"r56-ab-bank-switch-{edit}");
        Harness harness = CreateHarness();
        MainWindowViewModel viewModel = harness.ViewModel;
        ConfigureAbCtrlRamPage(viewModel);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, cancellationToken);
        await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.Both);
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, cancellationToken);
        AbCtrlRamDraftState previousDraft = Assert.IsType<AbCtrlRamDraftState>(viewModel.Replace.CurrentCtrlRamDraft);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        CtrlRamFirmwareVersionEditorViewModel bankB = Assert.Single(
            viewModel.Replace.AbCtrlRamVersionEditors, static editor => editor.BankId == "b-bank");
        bankB.EditCommand.Execute(null);
        bankB.VersionText = "34";
        bankB.SubVersionText = "12";

        Task readinessHeld = harness.Readiness.Arm();
        Task<bool> firstConfirm = viewModel.OutputDelivery.PrepareModeSpecificAsync();
        await readinessHeld.WaitAsync(Wait, cancellationToken);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        Assert.False(firstConfirm.IsCompleted);
        Assert.Equal(previousDraft, viewModel.Replace.CurrentCtrlRamDraft);
        Assert.Equal(previousDraft, Assert.IsType<ActiveSessionSnapshot>(harness.Readiness.LastSession).DraftState);
        Assert.Equal(2, harness.Readiness.VersionTransitions);
        await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.B);
        var expectedDraft = new AbCtrlRamDraftState(AbCtrlRamBankSelection.B, previousDraft.AVersion, previousDraft.BVersion);
        ActiveSessionSnapshot bankSession = Assert.IsType<ActiveSessionSnapshot>(harness.Readiness.LastSession);
        Assert.Equal(expectedDraft, bankSession.DraftState);
        harness.Readiness.Release();

        Assert.False(await firstConfirm.WaitAsync(Wait, cancellationToken));
        Assert.Equal(2, harness.Readiness.VersionTransitions);
        Assert.Same(bankSession, harness.Readiness.LastSession);
        Assert.Equal(expectedDraft, viewModel.Replace.CurrentCtrlRamDraft);
        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.Empty(harness.Execution.Requests);
        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        AcceptedCompositionExecutionRequest preview = Assert.Single(harness.Execution.Requests);
        Assert.Equal(expectedDraft, preview.AcceptedSession.DraftState);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.bin"));

        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        bankB = Assert.Single(viewModel.Replace.AbCtrlRamVersionEditors, static editor => editor.BankId == "b-bank");
        if (edit)
        {
            bankB.EditCommand.Execute(null);
            bankB.VersionText = "33";
            bankB.SubVersionText = "44";
        }
        else
        {
            bankB.PreserveCommand.Execute(null);
        }
        string outputPath = await ConfirmAsync(viewModel, workspace, bundle: false);
        Assert.Equal(2, harness.Execution.Requests.Count);
        AbCtrlRamDraftState builtDraft = Assert.IsType<AbCtrlRamDraftState>(harness.Execution.Requests[1].AcceptedSession.DraftState);
        Assert.Equal(expectedDraft.Banks, builtDraft.Banks);
        Assert.Equal(expectedDraft.AVersion, builtDraft.AVersion);
        Assert.Equal(edit ? new CtrlRamFirmwareVersionDraftState(0x33, 0x44) : previousDraft.BVersion, builtDraft.BVersion);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        byte[] reference = await File.ReadAllBytesAsync(AbCtrlRamReferencePath, cancellationToken);
        byte[] actual = await File.ReadAllBytesAsync(outputPath, cancellationToken);
        Assert.Equal(reference.Length, actual.Length);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference.AsSpan(0x40000, 0x40000), out FirmwareConfigMetadata before));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(actual.AsSpan(0x40000, 0x40000), out FirmwareConfigMetadata after));
        Assert.Equal(edit ? 0x33 : before.FirmwareVersion, after.FirmwareVersion);
        Assert.Equal(edit ? 0x44 : before.FirmwareSubVersion, after.FirmwareSubVersion);
        Assert.Equal(edit ? 0xCC : before.FirmwareVersionBar, after.FirmwareVersionBar);
        Assert.False(viewModel.OutputDelivery.IsOpen);
    }

    /// <summary>
    /// After Cancel during readiness, a fresh Standard confirmation writes only its new Keep/Edit choice.
    /// </summary>
    /// <param name="visibleChange">The version choice in the next Build Settings flow.</param>
    /// <param name="bundle">Whether the next Confirm requests bundle delivery.</param>
    [Theory]
    [InlineData("keep", false)]
    [InlineData("edit-fields", false)]
    [InlineData("keep", true)]
    [InlineData("edit-fields", true)]
    public async Task R12H2bStandardNextConfirmationHasNoCancelledVersionResidue(string visibleChange, bool bundle)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = TempWorkspace.Create($"r12-h2b-{visibleChange}-{bundle}");
        Harness harness = CreateHarness();
        MainWindowViewModel viewModel = harness.ViewModel;
        PrepareStandardReady(viewModel, workspace);
        string sourceBasePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        FirmwareConfigMetadataSnapshot source = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(TestProjection, "NT51926", sourceBasePath));
        CtrlRamAuthoringDraftState? previousDraft = viewModel.Replace.CurrentCtrlRamDraft;
        Assert.NotEqual(0x2A, source.FirmwareVersion);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        Task readinessHeld = harness.Readiness.Arm();
        Task<bool> firstConfirm = viewModel.OutputDelivery.PrepareModeSpecificAsync();
        await readinessHeld.WaitAsync(Wait, cancellationToken);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        harness.Readiness.Release();
        Assert.False(await firstConfirm.WaitAsync(Wait, cancellationToken));
        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.Empty(harness.Execution.Requests);
        Assert.Equal(previousDraft, viewModel.Replace.CurrentCtrlRamDraft);
        Assert.Equal(previousDraft, Assert.IsType<ActiveSessionSnapshot>(harness.Readiness.LastSession).DraftState);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());

        if (visibleChange == "keep")
        {
            viewModel.Replace.SelectCtrlRamFirmwareVersionPreserveCommand.Execute(null);
            Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionPreserveSelected);
        }
        else
        {
            viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
            viewModel.Replace.CtrlRamFirmwareVersionText = "33";
            viewModel.Replace.CtrlRamFirmwareSubVersionText = "44";
            Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionEditSelected);
        }

        string outputPath = await ConfirmAsync(viewModel, workspace, bundle);

        AcceptedCompositionExecutionRequest executed = Assert.Single(harness.Execution.Requests);
        if (visibleChange == "keep")
        {
            Assert.Null(executed.AcceptedSession.DraftState);
        }
        else
        {
            Assert.Equal(new CtrlRamFirmwareVersionDraftState(0x33, 0x44), executed.AcceptedSession.DraftState);
        }
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        FirmwareConfigMetadataSnapshot output = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(TestProjection, "NT51926", outputPath));
        Assert.Equal(visibleChange == "keep" ? source.FirmwareVersion : 0x33, output.FirmwareVersion);
        Assert.Equal(visibleChange == "keep" ? source.FirmwareSubVersion : 0x44, output.FirmwareSubVersion);
        Assert.Equal(visibleChange == "keep" ? source.FirmwareVersionBar : 0xCC, output.FirmwareVersionBar);
        TestContext.Current.TestOutputHelper?.WriteLine(FormattableString.Invariant(
            $"R56 Standard {visibleChange}, bundle={bundle}: source {source.FirmwareVersion:X2}/{source.FirmwareSubVersion:X2}; output {output.FirmwareVersion:X2}/{output.FirmwareSubVersion:X2}"));
        Assert.False(viewModel.OutputDelivery.IsOpen);
    }

    /// <summary>
    /// After Cancel during readiness, a fresh AB confirmation writes its B-bank Keep/Edit choice.
    /// The A-bank check compares FWConfig metadata, not all A-bank bytes.
    /// </summary>
    /// <param name="visibleChange">The B-bank version choice in the next Build Settings flow.</param>
    /// <param name="bundle">Whether the next Confirm requests bundle delivery.</param>
    [Theory]
    [InlineData("keep", false)]
    [InlineData("edit-fields", false)]
    [InlineData("keep", true)]
    [InlineData("edit-fields", true)]
    public async Task R12H2cAbNextConfirmationHasNoCancelledBankVersionResidue(string visibleChange, bool bundle)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = TempWorkspace.Create($"r12-h2c-ab-{visibleChange}-{bundle}");
        Harness harness = CreateHarness();
        MainWindowViewModel viewModel = harness.ViewModel;
        ConfigureAbCtrlRamPage(viewModel);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, cancellationToken);
        await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.Both);
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, cancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        Assert.True(viewModel.Replace.IsAbCtrlRamReference);
        AbCtrlRamDraftState previousDraft = Assert.IsType<AbCtrlRamDraftState>(viewModel.Replace.CurrentCtrlRamDraft);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        CtrlRamFirmwareVersionEditorViewModel bankB = Assert.Single(
            viewModel.Replace.AbCtrlRamVersionEditors, static editor => editor.BankId == "b-bank");
        Assert.True(bankB.CanEdit);
        bankB.EditCommand.Execute(null);
        bankB.VersionText = "34";
        bankB.SubVersionText = "12";

        Task readinessHeld = harness.Readiness.Arm();
        Task<bool> firstConfirm = viewModel.OutputDelivery.PrepareModeSpecificAsync();
        await readinessHeld.WaitAsync(Wait, cancellationToken);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        Assert.False(viewModel.OutputDelivery.IsOpen);
        harness.Readiness.Release();

        Assert.False(await firstConfirm.WaitAsync(Wait, cancellationToken));
        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.Empty(harness.Execution.Requests);
        Assert.Equal(previousDraft, viewModel.Replace.CurrentCtrlRamDraft);
        Assert.Equal(previousDraft, Assert.IsType<ActiveSessionSnapshot>(harness.Readiness.LastSession).DraftState);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        bankB = Assert.Single(viewModel.Replace.AbCtrlRamVersionEditors, static editor => editor.BankId == "b-bank");
        Assert.True(bankB.IsPreserveSelected);

        if (visibleChange == "keep")
        {
            bankB.PreserveCommand.Execute(null);
        }
        else
        {
            bankB.EditCommand.Execute(null);
            bankB.VersionText = "33";
            bankB.SubVersionText = "44";
        }
        string outputPath = await ConfirmAsync(viewModel, workspace, bundle);

        AcceptedCompositionExecutionRequest executed = Assert.Single(harness.Execution.Requests);
        AbCtrlRamDraftState executedDraft = Assert.IsType<AbCtrlRamDraftState>(executed.AcceptedSession.DraftState);
        Assert.Equal(AbCtrlRamBankSelection.Both, executedDraft.Banks);
        Assert.Equal(previousDraft.AVersion, executedDraft.AVersion);
        Assert.Equal(visibleChange == "keep" ? null : new CtrlRamFirmwareVersionDraftState(0x33, 0x44), executedDraft.BVersion);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        byte[] reference = await File.ReadAllBytesAsync(AbCtrlRamReferencePath, cancellationToken);
        byte[] actual = await File.ReadAllBytesAsync(outputPath, cancellationToken);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            reference.AsSpan(0x40000, 0x40000), out FirmwareConfigMetadata before));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            actual.AsSpan(0x40000, 0x40000), out FirmwareConfigMetadata after));
        Assert.Equal(reference.Length, actual.Length);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference.AsSpan(0, 0x40000), out FirmwareConfigMetadata beforeA));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(actual.AsSpan(0, 0x40000), out FirmwareConfigMetadata afterA));
        Assert.Equal(beforeA, afterA);
        Assert.Equal(visibleChange == "keep" ? before.FirmwareVersion : 0x33, after.FirmwareVersion);
        Assert.Equal(visibleChange == "keep" ? before.FirmwareSubVersion : 0x44, after.FirmwareSubVersion);
        Assert.Equal(visibleChange == "keep" ? before.FirmwareVersionBar : 0xCC, after.FirmwareVersionBar);
        TestContext.Current.TestOutputHelper?.WriteLine(FormattableString.Invariant(
            $"R56 AB B-bank {visibleChange}, bundle={bundle}: source {before.FirmwareVersion:X2}/{before.FirmwareSubVersion:X2}; output {after.FirmwareVersion:X2}/{after.FirmwareSubVersion:X2}"));
        Assert.False(viewModel.OutputDelivery.IsOpen);
    }

    private static async Task<string> ConfirmAsync(MainWindowViewModel viewModel, TempWorkspace workspace, bool bundle)
    {
        viewModel.OutputDelivery.SetBundleEnabled(bundle);
        if (bundle)
        {
            viewModel.OutputDelivery.SetParentDirectory(workspace.Root);
            viewModel.OutputDelivery.SetBundleFolderName("next-bundle");
            await viewModel.OutputDelivery.ConfirmBundleAsync();
            return Path.Combine(workspace.Root, "next-bundle", viewModel.OutputDelivery.OutputFileName);
        }

        string outputPath = workspace.PathFor("next.bin");
        Assert.True(await viewModel.OutputDelivery.PrepareModeSpecificAsync());
        await OutputDeliveryConfirmationModal.ConfirmPreparedLooseWithPickersAsync(
            viewModel.OutputDelivery,
            () => Task.FromResult<string?>(outputPath),
            () => Task.FromResult<string?>(null));
        return outputPath;
    }

    private static void PrepareStandardReady(MainWindowViewModel viewModel, TempWorkspace workspace)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile("replace-base", workspace.Write("base-from-golden.bin", baseBytes));
        FirmwareSlotViewModel replacementSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        CtrlRamRegionViewModel region = viewModel.Replace.CtrlRamRegions.Single(
            candidate => candidate.Name == replacementSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        viewModel.SetSlotFile(
            replacementSlot.SlotId,
            workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]));
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
    }

    private static Harness CreateHarness()
    {
        PresentationHostServices original = PresentationTestHost.CreateServices(
            ApplicationVersionProvider.InformationalVersion);
        PresentationCompositionServices current = original.Composition;
        ICtrlRamAuthoring authoring = DispatchProxy.Create<ICtrlRamAuthoring, R12H2ReadinessGate>();
        var readiness = (R12H2ReadinessGate)authoring;
        readiness.Inner = current.CtrlRamAuthoring;
        var naming = new R12H2NamingGate(current.OutputNaming);
        var execution = new R12H2RecordingExecution(current.Execution);
        var composition = new PresentationCompositionServices(
            current.Capabilities,
            current.StandardMergeAuthoring,
            current.AbMergeAuthoring,
            current.GeneralAuthoring,
            authoring,
            current.FirmwareInspection,
            naming,
            execution);
        var services = new PresentationHostServices(
            composition,
            original.FileReveal,
            original.SupportMatrix,
            original.SystemInformation,
            original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader,
            original.LocalFiles,
            original.LocalStateDirectory,
            original.VersionManagement,
            original.ManagedApplicationStartup,
            original.StableLauncherHandoff,
            original.EventBufferFormatConfigurationSessionFactory,
            original.ToolchainRuntimeConfigurationSessionFactory);
        MainWindowViewModel viewModel = PresentationTestHost.PublishCanonicalCatalog(
            services,
            ShellViewModelFactory.Create(services, ShellLanguage.English));
        return new Harness(viewModel, readiness, naming, execution);
    }

    private sealed record Harness(
        MainWindowViewModel ViewModel,
        R12H2ReadinessGate Readiness,
        R12H2NamingGate Naming,
        R12H2RecordingExecution Execution);

    /// <summary>Holds the next CtrlRAM action-readiness read after <see cref="Arm"/> until released.</summary>
    [SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass.")]
    public class R12H2ReadinessGate : DispatchProxy
    {
        private TaskCompletionSource? _armed;
        private TaskCompletionSource? _started;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ICtrlRamAuthoring Inner { get; set; } = null!;
        internal ActiveSessionSnapshot? LastSession { get; private set; }
        internal int VersionTransitions { get; private set; }

        internal Task Arm()
        {
            _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _armed, _started);
            return _started.Task;
        }

        internal void Release()
        {
            _ = _release.TrySetResult();
        }

        /// <inheritdoc />
        [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly",
            Justification = "DispatchProxy must forward the boxed ValueTask without consuming it.")]
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);
            if (targetMethod.Name == nameof(ICtrlRamAuthoring.TransitionFirmwareVersionCompilation))
            {
                VersionTransitions++;
            }
            if (targetMethod.Name == nameof(ICtrlRamAuthoring.GetActionReadinessAsync))
            {
                LastSession = (ActiveSessionSnapshot)args[3]!;
            }
            return targetMethod.Name == nameof(ICtrlRamAuthoring.GetActionReadinessAsync) &&
                Interlocked.Exchange(ref _armed, null) is { } started
                ? HoldAsync(started, (string)args[0]!, (string)args[1]!,
                    (IReadOnlyDictionary<string, string>)args[2]!, (ActiveSessionSnapshot)args[3]!,
                    (CancellationToken)args[4]!)
                : targetMethod.Invoke(Inner, args);
        }

        private async ValueTask<CapabilityActionReadinessSnapshot?> HoldAsync(
            TaskCompletionSource started,
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            ActiveSessionSnapshot session,
            CancellationToken cancellationToken)
        {
            _ = started.TrySetResult();
            await _release.Task;
            return await Inner.GetActionReadinessAsync(icId, number, slotPaths, session, cancellationToken);
        }
    }

    private sealed class R12H2NamingGate(ICompositionOutputNaming inner) : ICompositionOutputNaming
    {
        private TaskCompletionSource? _armed;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Arm()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _armed, started);
            return started.Task;
        }

        internal void Release()
        {
            _ = _release.TrySetResult();
        }

        public async ValueTask<CompositionOutputBundleProposal> PrepareBundleProposalAsync(
            ActiveSessionSnapshot acceptedSession,
            CancellationToken cancellationToken,
            CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
        {
            if (Interlocked.Exchange(ref _armed, null) is { } started)
            {
                _ = started.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }

            return await inner.PrepareBundleProposalAsync(acceptedSession, cancellationToken, ctrlRamVersionEdit);
        }

        public ValueTask<bool> IsProposalCurrentAsync(
            CompositionOutputBundleProposal proposal,
            CancellationToken cancellationToken)
        {
            return inner.IsProposalCurrentAsync(proposal, cancellationToken);
        }

        public CompositionOutputPreparation ResolveAcceptedOutput(
            ActiveSessionSnapshot acceptedSession,
            CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
        {
            return inner.ResolveAcceptedOutput(acceptedSession, ctrlRamVersionEdit);
        }

        public CompositionOutputBundleProposal ResolveAcceptedBundleProposal(
            ActiveSessionSnapshot acceptedSession,
            CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
        {
            return inner.ResolveAcceptedBundleProposal(acceptedSession, ctrlRamVersionEdit);
        }

        public CompositionOutputBundleDestinationValidation ValidateBundleDestination(
            CompositionOutputBundleIntent intent)
        {
            return inner.ValidateBundleDestination(intent);
        }

        public CompositionOutputBundleValidationIssue? ValidateName(string value)
        {
            return inner.ValidateName(value);
        }

        public ValueTask<CompositionOutputPreparation> PrepareAutomaticOutputAsync(
            ActiveSessionSnapshot acceptedSession,
            CancellationToken cancellationToken)
        {
            return inner.PrepareAutomaticOutputAsync(acceptedSession, cancellationToken);
        }
    }

    private sealed class R12H2RecordingExecution(ICompositionExecution inner) : ICompositionExecution
    {
        private readonly Lock _gate = new();
        private readonly List<AcceptedCompositionExecutionRequest> _requests = [];

        internal IReadOnlyList<AcceptedCompositionExecutionRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public ValueTask<CompositionRunResult> ExecuteAsync(
            AcceptedCompositionExecutionRequest request,
            CompositionRunProgressFeed progress,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _requests.Add(request);
            }

            return inner.ExecuteAsync(request, progress, cancellationToken);
        }
    }
}
