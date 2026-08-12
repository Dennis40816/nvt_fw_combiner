using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>A CtrlRAM batch read rejects a replacement whose file identity changes in flight.</summary>
    [Fact]
    public async Task CtrlRamInputInspectionMarksChangedFileAsBlocking()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-inspection-identity");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        string replacementPath = workspace.Write("changing-ctrlram.bin", new byte[0x1660]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            if (inputs.Any(input => StringComparer.Ordinal.Equals(
                    input.Path,
                    replacementPath)))
            {
                using var stream = new FileStream(
                    replacementPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);
                stream.WriteByte(0x02);
            }
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacement.SlotId));

        Assert.False(replacement.IsInputInspectionPending);
        Assert.Equal(
            FirmwareInputInspectionSeverity.Blocking,
            replacement.InputInspectionSeverity);
        Assert.Contains(
            "file changed",
            replacement.InputInspectionStatus,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(replacement.BlocksBuild);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }
}
