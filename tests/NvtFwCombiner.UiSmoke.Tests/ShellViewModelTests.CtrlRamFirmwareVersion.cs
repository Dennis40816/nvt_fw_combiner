using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>CtrlRAM keeps the verified edit lease through proposal creation, then closes both authoring modals.</summary>
    [Fact]
    public async Task CtrlRamConfirmedEditOpensOutputDeliveryForExactAcceptedSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-output-confirmation");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.True(viewModel.OutputDelivery.HasCtrlRamOptions);
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        using var destination = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-build-settings-state");
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("operator-edited-bundle");
        (bool succeeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(succeeded);

        Assert.True(await viewModel.OutputDelivery.PrepareModeSpecificAsync());

        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.OutputDelivery.IsReplaceOutput);
        Assert.Equal("nt51926-ctrlram-replace.bin", viewModel.OutputDelivery.OutputFileName);
        Assert.True(viewModel.OutputDelivery.BundleEnabled);
        Assert.Equal(destination.Root, viewModel.OutputDelivery.ParentDirectory);
        Assert.Equal("operator-edited-bundle", viewModel.OutputDelivery.BundleFolderName);
    }

    /// <summary>Verifies CtrlRAM Build exposes a Backup-derived Preserve/Edit choice and validates staged bytes.</summary>
    [Fact]
    public async Task CtrlRamBuildFirmwareVersionChoiceUsesVerifiedBackupMetadata()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-choice");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionPreserveSelected);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionEditSelected);
        Assert.True(viewModel.Replace.CanEditCtrlRamFirmwareVersion, viewModel.Replace.CtrlRamFirmwareVersionMetadataDetail);
        Assert.Matches("^[0-9A-F]{2} / [0-9A-F]{2}$", viewModel.Replace.CtrlRamFirmwareVersionCurrentValue);
        (bool preserveSucceeded, CtrlRamFirmwareVersionDraftState? preserveEdit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(preserveSucceeded);
        Assert.Null(preserveEdit);

        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionEditSelected);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionPreserveSelected);

        viewModel.Replace.CtrlRamFirmwareVersionText = "A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "04";
        (bool invalidSucceeded, _) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.False(invalidSucceeded);
        Assert.Equal(viewModel.Text.CtrlRamFirmwareVersionInvalidByteDetail, viewModel.Replace.CtrlRamFirmwareVersionValidationDetail);

        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        (bool editSucceeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(editSucceeded);
        Assert.NotNull(edit);
        Assert.Equal((byte)0x2A, edit.FirmwareVersion);
        Assert.Equal((byte)0x0C, edit.FirmwareSubVersion);
        Assert.Equal("nt51926-ctrlram-replace.bin", viewModel.Replace.CreateCtrlRamReplaceOutputFileName(edit));

        viewModel.Replace.CloseCtrlRamFirmwareVersionModal();
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
    }

    /// <summary>Verifies the confirmed version reaches the output Backup through the admitted V2 postbuild route.</summary>
    [Fact]
    public async Task CtrlRamBuildPropagatesConfirmedFirmwareVersionToOutputBackup()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-build");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        string outputPath = workspace.PathFor("ctrlram-version-output.bin");

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        (bool editSucceeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(editSucceeded);
        Assert.NotNull(edit);
        viewModel.Replace.CloseCtrlRamFirmwareVersionModal();

        await viewModel.Replace.BuildReplaceAsync(outputPath, edit);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath));
        FirmwareConfigMetadataSnapshot? outputMetadata =
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(TestProjection, "NT51926", outputPath);
        Assert.NotNull(outputMetadata);
        Assert.Equal(0x2A, outputMetadata.FirmwareVersion);
        Assert.Equal(0xD5, outputMetadata.FirmwareVersionBar);
        Assert.Equal(0x0C, outputMetadata.FirmwareSubVersion);
        Assert.True(viewModel.Reports.HasLoadedReport);
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        Assert.Empty(report.RootElement.GetProperty("Issues").EnumerateArray());
        JsonElement validation = Assert.Single(report.RootElement.GetProperty("Validations").EnumerateArray());
        Assert.Equal("verify-nvt-fwconfig-backup-version", validation.GetProperty("RuleId").GetString());
        Assert.Equal("Passed", validation.GetProperty("Status").GetString());
    }

    /// <summary>Build reuses accepted CtrlRAM bytes even when the selected path changes later.</summary>
    [Fact]
    public async Task CtrlRamBuildReusesAcceptedBytesAfterSelectedPathChanges()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-stale-build");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        string previewSha256 = viewModel.Reports.LoadedReport.OutputSha256;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(static slot =>
            slot.HasFile && slot.SlotId != CompositionSlotIds.ReplaceBase);
        string replacementPath = Assert.IsType<string>(replacement.FilePath);
        byte[] changed = File.ReadAllBytes(replacementPath);
        changed[0] ^= 0x01;
        File.WriteAllBytes(replacementPath, changed);
        string outputPath = workspace.PathFor("accepted-output.bin");

        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(previewSha256, viewModel.Reports.LoadedReport.OutputSha256);
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

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        if (selectEdit)
        {
            viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        }

        Assert.True(viewModel.Replace.CanConfirmCtrlRamFirmwareVersion);
        Assert.True(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(cancellationToken));

        switch (contextChange)
        {
            case "ic":
                viewModel.WorkflowSession.SelectedIc = "NT51927";
                break;
            case "number":
                viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
                break;
            case "base":
                viewModel.SetSlotFile("replace-base", workspace.Write("changed-base.bin", baseBytes));
                break;
            case "replacement":
                FirmwareSlotViewModel replacementSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
                    !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) && slot.HasFile);
                string replacementPath = Assert.IsType<string>(replacementSlot.FilePath);
                viewModel.SetSlotFile(
                    replacementSlot.SlotId,
                    workspace.Write("changed-replacement.bin", File.ReadAllBytes(replacementPath)));
                break;
            default:
                throw new InvalidOperationException($"Unsupported test context change '{contextChange}'.");
        }

        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.False(viewModel.Replace.CanConfirmCtrlRamFirmwareVersion);
        Assert.False(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(cancellationToken));
        (bool succeeded, _) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.False(succeeded);
    }

    /// <summary>Build confirmation and execution retain the immutable bytes accepted by the verified session.</summary>
    [Fact]
    public async Task CtrlRamFirmwareVersionBuildConfirmationUsesAcceptedBytesAfterDiskMutation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-content-lease");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        string acceptedOutputSha256 = viewModel.Reports.LoadedReport.OutputSha256;
        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        string basePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        byte[] changed = File.ReadAllBytes(basePath);
        changed[^1] ^= 0x01;
        File.WriteAllBytes(basePath, changed);

        Assert.True(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(
            cancellationToken));
        (bool succeeded, _) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(
            cancellationToken);
        Assert.True(succeeded);

        string outputPath = workspace.PathFor("accepted-session-output.bin");
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(acceptedOutputSha256, viewModel.Reports.LoadedReport.OutputSha256);
    }

    /// <summary>Open and Edit use canonical inspection facts even when the accepted Base path changes or disappears.</summary>
    [Theory]
    [InlineData("overwrite-before-open")]
    [InlineData("delete-before-open")]
    [InlineData("overwrite-after-open")]
    [InlineData("delete-after-open")]
    public async Task CtrlRamFirmwareVersionEditUsesAcceptedFactsWithoutPathReread(string mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutation);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-ctrlram-version-{mutation}");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        string basePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        CompiledInputVersionObservation acceptedMetadata = Assert.Single(
            viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection?
                .InputSlotStatus?.Observation.Versions ?? [],
            static version => version.Kind == CompiledInputVersionKind.TpReferenceFirmwareConfig);

        if (mutation.EndsWith("before-open", StringComparison.Ordinal))
        {
            MutateAcceptedPath(basePath, mutation);
        }

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        Assert.Equal(
            FormattableString.Invariant(
                $"{acceptedMetadata.Major:X2} / {acceptedMetadata.Minor:X2}"),
            viewModel.Replace.CtrlRamFirmwareVersionCurrentValue);
        if (mutation.EndsWith("after-open", StringComparison.Ordinal))
        {
            MutateAcceptedPath(basePath, mutation);
        }

        Assert.True(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(
            cancellationToken));
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        (bool succeeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(succeeded);
        Assert.NotNull(edit);

        string outputPath = workspace.PathFor($"{mutation}-output.bin");
        await viewModel.Replace.BuildReplaceAsync(outputPath, edit);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        FirmwareConfigMetadataSnapshot outputMetadata = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(
                TestProjection,
                "NT51926",
                outputPath));
        Assert.Equal(0x2A, outputMetadata.FirmwareVersion);
        Assert.Equal(0x0C, outputMetadata.FirmwareSubVersion);
    }

    private static void MutateAcceptedPath(string path, string mutation)
    {
        if (mutation.StartsWith("delete", StringComparison.Ordinal))
        {
            File.Delete(path);
            return;
        }

        byte[] changed = File.ReadAllBytes(path);
        changed[^1] ^= 0x01;
        File.WriteAllBytes(path, changed);
    }

}
