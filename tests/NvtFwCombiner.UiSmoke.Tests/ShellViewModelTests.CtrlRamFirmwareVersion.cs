using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM Build exposes a Backup-derived Preserve/Edit choice and validates staged bytes.</summary>
    [Fact]
    public void CtrlRamBuildFirmwareVersionChoiceUsesVerifiedBackupMetadata()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-choice");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(viewModel.TryOpenCtrlRamFirmwareVersionModal());
        Assert.True(viewModel.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.IsCtrlRamFirmwareVersionPreserveSelected);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionEditSelected);
        Assert.True(viewModel.CanEditCtrlRamFirmwareVersion, viewModel.CtrlRamFirmwareVersionMetadataDetail);
        Assert.Matches("^[0-9A-F]{2} / [0-9A-F]{2}$", viewModel.CtrlRamFirmwareVersionCurrentValue);
        Assert.True(viewModel.TryCreateCtrlRamFirmwareVersionEdit(out WorkbenchCtrlRamFirmwareVersionEdit? preserveEdit));
        Assert.Null(preserveEdit);

        viewModel.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        Assert.True(viewModel.IsCtrlRamFirmwareVersionEditSelected);
        Assert.False(viewModel.IsCtrlRamFirmwareVersionPreserveSelected);

        viewModel.CtrlRamFirmwareVersionText = "A";
        viewModel.CtrlRamFirmwareSubVersionText = "04";
        Assert.False(viewModel.TryCreateCtrlRamFirmwareVersionEdit(out _));
        Assert.Equal(viewModel.Text.CtrlRamFirmwareVersionInvalidByteDetail, viewModel.CtrlRamFirmwareVersionValidationDetail);

        viewModel.CtrlRamFirmwareVersionText = "2A";
        viewModel.CtrlRamFirmwareSubVersionText = "0C";
        Assert.True(viewModel.TryCreateCtrlRamFirmwareVersionEdit(out WorkbenchCtrlRamFirmwareVersionEdit? edit));
        Assert.NotNull(edit);
        Assert.Equal((byte)0x2A, edit.FirmwareVersion);
        Assert.Equal((byte)0x0C, edit.FirmwareSubVersion);

        viewModel.CloseCtrlRamFirmwareVersionModal();
        Assert.False(viewModel.IsCtrlRamFirmwareVersionModalOpen);
    }

    /// <summary>Verifies the user-confirmed CtrlRAM version edit reaches the Backup through the declared postbuild path.</summary>
    [Fact]
    public async Task CtrlRamBuildPropagatesConfirmedFirmwareVersionToOutputBackup()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-build");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        string outputPath = workspace.PathFor("ctrlram-version-output.bin");

        Assert.True(viewModel.TryOpenCtrlRamFirmwareVersionModal());
        viewModel.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.CtrlRamFirmwareVersionText = "2A";
        viewModel.CtrlRamFirmwareSubVersionText = "0C";
        Assert.True(viewModel.TryCreateCtrlRamFirmwareVersionEdit(out WorkbenchCtrlRamFirmwareVersionEdit? edit));
        Assert.NotNull(edit);
        viewModel.CloseCtrlRamFirmwareVersionModal();

        await viewModel.BuildReplaceAsync(outputPath, edit);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath), outputPath);
        WorkbenchFirmwareConfigMetadata? outputMetadata =
            WorkbenchCompositionService.TryReadFirmwareConfigMetadata("NT51926", outputPath);
        Assert.NotNull(outputMetadata);
        Assert.True(outputMetadata.IsFirmwareVersionBarValid);
        Assert.Equal((byte)0x2A, outputMetadata.FirmwareVersion);
        Assert.Equal((byte)0x0C, outputMetadata.FirmwareSubVersion);
    }

    private static MainWindowViewModel CreateCtrlRamVersionReadyViewModel(byte[] baseBytes, TempWorkspace workspace)
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = "cascade";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        viewModel.SetSlotFile("replace-base", basePath);
        FirmwareSlotViewModel replacementSlot = viewModel.ReplaceSlots.Single(slot =>
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(candidate => candidate.Name == replacementSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        viewModel.SetSlotFile(
            replacementSlot.SlotId,
            workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]));

        Assert.True(viewModel.CanBuildReplace, viewModel.ReplaceBuildUnavailableReason);
        return viewModel;
    }
}
