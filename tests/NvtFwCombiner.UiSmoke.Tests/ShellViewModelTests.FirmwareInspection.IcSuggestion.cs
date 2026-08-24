using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Standard Merge forwards the selected slot's inspected IC to the shared mismatch prompt.</summary>
    [Fact]
    public async Task StandardMergeSelectionPromptsForInspectedIcProfileMismatch()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-standard-merge-ic-suggestion");
        string tpPath = workspace.Write("NT51950_tp.bin", [0x01]);
        var batches = new List<(string IcId, FirmwareInspectionSnapshotInput[] Inputs)>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            batches.Add((icId, [.. inputs]));
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot("NT51950", null, null, null, null, null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            tpPath,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51950", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        Assert.Equal("NT51950_tp.bin", viewModel.WorkflowSession.FirmwareIcMismatchFileName);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
        _ = Assert.Single(batches);
        Assert.Equal("NT51926", batches[0].IcId);

        viewModel.WorkflowSession.AcceptFirmwareIcMismatchCommand.Execute(null);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(2, batches.Count);
        Assert.Equal("NT51950", batches[^1].IcId);
        Assert.Contains(
            batches[^1].Inputs,
            static input => input.InspectionId == CompositionSlotIds.MergeTp);
    }

    /// <summary>A perfect-family IC hint remains advisory until the user confirms the existing mismatch prompt.</summary>
    [Fact]
    public async Task PerfectFamilyIcHintRequiresConfirmationBeforeChangingAbContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-perfect-family-context");
        string inputPath = workspace.Write("NT51932_dp-ab.bin", [0x01]);
        var batches = new List<(string IcId, FirmwareInspectionSnapshotInput[] Inputs)>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            batches.Add((icId, [.. inputs]));
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot("NT51932", null, null, null, null, null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        string originalNumber = viewModel.WorkflowSession.SelectedNumber;
        string originalOutputName = viewModel.Merge.MergeOutputFileName;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            inputPath,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51932", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        Assert.Equal("NT51929", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(originalNumber, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(originalOutputName, viewModel.Merge.MergeOutputFileName);
        Assert.Equal(
            inputPath,
            viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput).FilePath);
        _ = Assert.Single(batches);
        Assert.Equal("NT51929", batches[0].IcId);

        viewModel.WorkflowSession.AcceptFirmwareIcMismatchCommand.Execute(null);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51932", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(originalNumber, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(
            inputPath,
            viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput).FilePath);
        Assert.Equal(2, batches.Count);
        Assert.Equal("NT51932", batches[^1].IcId);
        Assert.Contains(
            batches[^1].Inputs,
            static input => input.InspectionId == CompositionAddressSpaceIds.DpAbInput);
    }
}
