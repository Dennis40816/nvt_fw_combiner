using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM Build exposes a Backup-derived Preserve/Edit choice and validates staged bytes.</summary>
    [Fact]
    public async Task CtrlRamBuildFirmwareVersionChoiceUsesVerifiedBackupMetadata()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-choice");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(await viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        Assert.True(viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.IsCtrlRamFirmwareVersionPreserveSelected);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionEditSelected);
        Assert.True(viewModel.CanEditCtrlRamFirmwareVersion, viewModel.CtrlRamFirmwareVersionMetadataDetail);
        Assert.Matches("^[0-9A-F]{2} / [0-9A-F]{2}$", viewModel.CtrlRamFirmwareVersionCurrentValue);
        (bool preserveSucceeded, WorkbenchCtrlRamFirmwareVersionEdit? preserveEdit) =
            await viewModel.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(preserveSucceeded);
        Assert.Null(preserveEdit);

        viewModel.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        Assert.True(viewModel.IsCtrlRamFirmwareVersionEditSelected);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionPreserveSelected);

        viewModel.CtrlRamFirmwareVersionText = "A";
        viewModel.CtrlRamFirmwareSubVersionText = "04";
        (bool invalidSucceeded, _) = await viewModel.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.False(invalidSucceeded);
        Assert.Equal(viewModel.Text.CtrlRamFirmwareVersionInvalidByteDetail, viewModel.CtrlRamFirmwareVersionValidationDetail);

        viewModel.CtrlRamFirmwareVersionText = "2A";
        viewModel.CtrlRamFirmwareSubVersionText = "0C";
        (bool editSucceeded, WorkbenchCtrlRamFirmwareVersionEdit? edit) =
            await viewModel.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(editSucceeded);
        Assert.NotNull(edit);
        Assert.Equal((byte)0x2A, edit.FirmwareVersion);
        Assert.Equal((byte)0x0C, edit.FirmwareSubVersion);

        viewModel.CloseCtrlRamFirmwareVersionModal();
        Assert.False(viewModel.IsCtrlRamFirmwareVersionModalOpen);
    }

    /// <summary>Verifies a version edit cannot bypass exact evidence-backed V2 route admission.</summary>
    [Fact]
    public async Task CtrlRamBuildPropagatesConfirmedFirmwareVersionToOutputBackup()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-build");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        string outputPath = workspace.PathFor("ctrlram-version-output.bin");

        Assert.True(await viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        viewModel.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.CtrlRamFirmwareVersionText = "2A";
        viewModel.CtrlRamFirmwareSubVersionText = "0C";
        (bool editSucceeded, WorkbenchCtrlRamFirmwareVersionEdit? edit) =
            await viewModel.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(editSucceeded);
        Assert.NotNull(edit);
        viewModel.CloseCtrlRamFirmwareVersionModal();

        await viewModel.BuildReplaceAsync(outputPath, edit);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal(
            "The selected CtrlRAM Replace shape has no exact evidence-backed V2 route.",
            viewModel.LastRunResult.Detail);
        Assert.False(File.Exists(outputPath));
        Assert.True(viewModel.HasLoadedReport);
        using var report = JsonDocument.Parse(viewModel.LoadedReportJson);
        JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal("replace.workflow.not-supported", issue.GetProperty("Code").GetString());
    }

    /// <summary>The CtrlRAM metadata read yields immediately, runs off-thread, and admits only one request.</summary>
    [Fact]
    public async Task CtrlRamFirmwareVersionMetadataReadDoesNotBlockCallerOrRunConcurrently()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-async");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        WorkbenchFirmwareConfigMetadata? metadata = null;
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        int callerThread = Environment.CurrentManagedThreadId;
        int readerThread = 0;
        int readCount = 0;
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(
            baseBytes,
            workspace,
            (icId, path) =>
            {
                _ = Interlocked.Increment(ref readCount);
                readerThread = Environment.CurrentManagedThreadId;
                readerEntered.Set();
                Assert.True(releaseReader.Wait(TimeSpan.FromSeconds(10), cancellationToken));
                return metadata ??= WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path);
            });

        Task<bool> firstOpen = viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken);

        Assert.True(viewModel.IsCtrlRamFirmwareVersionMetadataLoading);
        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        Assert.False(firstOpen.IsCompleted);
        Assert.NotEqual(callerThread, readerThread);
        Assert.False(await viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        Assert.Equal(1, Volatile.Read(ref readCount));

        releaseReader.Set();
        Assert.True(await firstOpen);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionMetadataLoading);
        Assert.True(viewModel.IsCtrlRamFirmwareVersionModalOpen);
    }

    /// <summary>A base file changed while metadata is read cannot open a modal for the stale identity.</summary>
    [Fact]
    public async Task CtrlRamFirmwareVersionMetadataReadRejectsChangedFileIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-stamp-race");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        WorkbenchFirmwareConfigMetadata? metadata = null;
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(
            baseBytes,
            workspace,
            (icId, path) =>
            {
                readerEntered.Set();
                Assert.True(releaseReader.Wait(TimeSpan.FromSeconds(10), cancellationToken));
                return metadata ??= WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path);
            });
        string basePath = Assert.IsType<string>(viewModel.ReplaceBaseSlot.FilePath);

        Task<bool> open = viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken);
        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        File.SetLastWriteTimeUtc(basePath, File.GetLastWriteTimeUtc(basePath).AddMinutes(1));
        releaseReader.Set();

        Assert.False(await open);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionMetadataLoading);
    }

    /// <summary>A context generation changed while metadata is read cannot publish the old modal state.</summary>
    [Fact]
    public async Task CtrlRamFirmwareVersionMetadataReadRejectsChangedContextGeneration()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-context-race");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        WorkbenchFirmwareConfigMetadata? metadata = null;
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(
            baseBytes,
            workspace,
            (icId, path) =>
            {
                readerEntered.Set();
                Assert.True(releaseReader.Wait(TimeSpan.FromSeconds(10), cancellationToken));
                return metadata ??= WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path);
            });

        Task<bool> open = viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken);
        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        viewModel.SelectedReplaceMode = WorkbenchReplaceModes.Dp;
        releaseReader.Set();

        Assert.False(await open);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionMetadataLoading);
    }

    /// <summary>A cancellation requested during the synchronous read prevents modal publication and resets loading.</summary>
    [Fact]
    public async Task CtrlRamFirmwareVersionMetadataReadHonorsCancellationAfterReaderStarts()
    {
        CancellationToken testCancellationToken = TestContext.Current.CancellationToken;
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(testCancellationToken);
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-cancel");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(
            baseBytes,
            workspace,
            (icId, path) =>
            {
                readerEntered.Set();
                Assert.True(releaseReader.Wait(TimeSpan.FromSeconds(10), testCancellationToken));
                return WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path);
            });

        Task<bool> open = viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationSource.Token);
        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(10), testCancellationToken));
        cancellationSource.Cancel();
        releaseReader.Set();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionMetadataLoading);
    }

    /// <summary>IC, number, and slot changes revoke both Preserve and Edit confirmation intent.</summary>
    [Theory]
    [InlineData("ic", false)]
    [InlineData("number", false)]
    [InlineData("base", false)]
    [InlineData("replacement", false)]
    [InlineData("number", true)]
    public async Task CtrlRamFirmwareVersionModalLeaseRejectsChangedBuildContext(
        string contextChange,
        bool selectEdit)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create(
            $"nvt-fw-combiner-ui-ctrlram-version-lease-{contextChange}-{selectEdit}");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(await viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        if (selectEdit)
        {
            viewModel.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        }

        Assert.True(viewModel.CanConfirmCtrlRamFirmwareVersion);
        Assert.True(await viewModel.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(cancellationToken));

        switch (contextChange)
        {
            case "ic":
                viewModel.SelectedIc = "NT51927";
                break;
            case "number":
                viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
                break;
            case "base":
                viewModel.SetSlotFile("replace-base", workspace.Write("changed-base.bin", baseBytes));
                break;
            case "replacement":
                FirmwareSlotViewModel replacementSlot = viewModel.ReplaceSlots.Single(slot =>
                    !ReferenceEquals(slot, viewModel.ReplaceBaseSlot) && slot.HasFile);
                string replacementPath = Assert.IsType<string>(replacementSlot.FilePath);
                viewModel.SetSlotFile(
                    replacementSlot.SlotId,
                    workspace.Write("changed-replacement.bin", File.ReadAllBytes(replacementPath)));
                break;
            default:
                throw new InvalidOperationException($"Unsupported test context change '{contextChange}'.");
        }

        Assert.False(viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.False(viewModel.CanConfirmCtrlRamFirmwareVersion);
        Assert.False(await viewModel.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(cancellationToken));
        (bool succeeded, _) = await viewModel.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.False(succeeded);
    }

    /// <summary>Close and Preserve/Edit selection races revoke an in-flight Edit metadata completion.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CtrlRamFirmwareVersionEditReadRejectsModalStateRace(bool closeModal)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-ctrlram-version-edit-race-{closeModal}");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        int readCount = 0;
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(
            baseBytes,
            workspace,
            (icId, path) =>
            {
                if (Interlocked.Increment(ref readCount) == 2)
                {
                    readerEntered.Set();
                    Assert.True(releaseReader.Wait(TimeSpan.FromSeconds(10), cancellationToken));
                }

                return WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path);
            });

        Assert.True(await viewModel.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        viewModel.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        Task<(bool Succeeded, WorkbenchCtrlRamFirmwareVersionEdit? Edit)> confirm =
            viewModel.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(10), cancellationToken));

        if (closeModal)
        {
            viewModel.CloseCtrlRamFirmwareVersionModal();
        }
        else
        {
            viewModel.SelectCtrlRamFirmwareVersionPreserveCommand.Execute(null);
        }

        releaseReader.Set();
        (bool succeeded, WorkbenchCtrlRamFirmwareVersionEdit? edit) = await confirm;

        Assert.False(succeeded);
        Assert.Null(edit);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionMetadataLoading);
        Assert.Equal(!closeModal, viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.Equal(!closeModal, viewModel.IsCtrlRamFirmwareVersionPreserveSelected);
    }

    private static MainWindowViewModel CreateCtrlRamVersionReadyViewModel(
        byte[] baseBytes,
        TempWorkspace workspace,
        Func<string, string, WorkbenchFirmwareConfigMetadata?>? firmwareConfigMetadataReader = null)
    {
        MainWindowViewModel viewModel = firmwareConfigMetadataReader is null
            ? ShellViewModelFactory.Create()
            : new MainWindowViewModel(
                "test-shell",
                "test-app",
                ShellLanguage.English,
                firmwareConfigMetadataReader);
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = "cascade";
        OpenReplace(viewModel, "CtrlRAM");

        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        viewModel.SetSlotFile("replace-base", basePath);
        FirmwareSlotViewModel replacementSlot = viewModel.ReplaceSlots.Single(slot =>
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(candidate => candidate.Name == replacementSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        viewModel.SetSlotFile(
            replacementSlot.SlotId,
            workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]));

        Assert.True(viewModel.CanBuildReplace, viewModel.ReplaceReadinessStatus);
        return viewModel;
    }
}
