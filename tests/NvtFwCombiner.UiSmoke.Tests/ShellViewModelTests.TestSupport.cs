using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public abstract partial class ShellViewModelTestBase
{
    private protected MainWindowViewModel CreateCtrlRamVersionReadyViewModel(
        byte[] baseBytes,
        TempWorkspace workspace,
        Func<string, string, FirmwareConfigMetadataSnapshot?>? firmwareConfigMetadataReader = null)
    {
        MainWindowViewModel viewModel;
        if (firmwareConfigMetadataReader is null)
        {
            viewModel = PresentationTestHost.CreateViewModel();
        }
        else
        {
            PresentationHostServices services = PresentationTestHost.CreateServices("test-app");
            viewModel = new MainWindowViewModel(
                "test-shell",
                "test-app",
                ShellLanguage.English,
                services,
                new DelegatingFirmwareInspection(
                    TestHost.FirmwareInspectionExperience,
                    metadataReader: firmwareConfigMetadataReader));
            _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        }
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        viewModel.SetSlotFile("replace-base", basePath);
        FirmwareSlotViewModel replacementSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        CtrlRamRegionViewModel region = viewModel.Replace.CtrlRamRegions.Single(candidate => candidate.Name == replacementSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        viewModel.SetSlotFile(
            replacementSlot.SlotId,
            workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]));

        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        return viewModel;
    }

    private protected MainWindowViewModel CreateBatchInspectionViewModel(
        Func<
            string,
            IReadOnlyList<FirmwareInspectionSnapshotInput>,
            IReadOnlyList<FirmwareInspectionSnapshotResult>> reader)
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("test-app");
        var viewModel = new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: reader));
        return PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
    }

    private protected static void AssertStandardMergeInputsReady(
        MainWindowViewModel viewModel,
        JsonElement goldenCase,
        string ic)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(ic);
        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            Assert.True(
                viewModel.Merge.MergeSlots.Single(slot =>
                    slot.SlotId == StandardMergeGoldenManifest.SlotIdForAddressSpace(input.Name)).HasFile,
                $"{ic} {input.Name} was not retained by the canonical slot transition.");
        }

        Assert.True(
            viewModel.Merge.PreviewMergeCommand.CanExecute(null),
            string.Join(
                " | ",
                viewModel.Merge.MergeSlots.Select(slot =>
                    $"{slot.SlotId}:file={slot.HasFile},state={slot.SemanticState}," +
                    $"severity={slot.InputInspectionSeverity},pending={slot.IsInputInspectionPending}," +
                    $"blocks={slot.BlocksBuild},canSelect={slot.CanSelectFile}," +
                    $"status={slot.InputInspectionStatus}")));
    }

    private protected static (int Start, int Length) ParseCtrlRamRegion(CtrlRamRegionViewModel region)
    {
        ArgumentNullException.ThrowIfNull(region);
        string startHex = region.StartAddress.Split('-', StringSplitOptions.TrimEntries)[0][2..];
        string lengthHex = region.SizeHex["len 0x".Length..];
        return (
            int.Parse(startHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(lengthHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private protected static void AssertIconGeometry(FirmwareSlotViewModel slot)
    {
        Assert.True(HasDrawableIcon(slot));
    }

    private protected static bool HasDrawableIcon(FirmwareSlotViewModel slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return slot.SlotIconPathData.StartsWith('M') &&
            slot.SlotIconPathData.Contains('L');
    }

    private protected static WorkflowInspectionLifecycle CurrentInspection(MainWindowViewModel viewModel)
    {
        return viewModel.IsReplaceVisible ? viewModel.Replace.Inspection : viewModel.Merge.Inspection;
    }

    private protected static void AssertInspectionTerminal(WorkflowInspectionLifecycle lifecycle)
    {
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, lifecycle.State);
        Assert.True(lifecycle.Generation > 0);
        Assert.Equal(lifecycle.TotalWork, lifecycle.CompletedWork);
    }

}
