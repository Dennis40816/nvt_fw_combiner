using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>AB Code requires consent before changing IC, then rebuilds topology and retains a compatible input.</summary>
    [Fact]
    public async Task AbMergeSelectionAcceptsInspectedIcAndRebuildsCompatibleContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-merge-ic-suggestion");
        string dpPath = workspace.Write("NT51950_dp-ab.bin", [0x01]);
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
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        Assert.False(viewModel.Merge.HasAbMergeTopologyChoices);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            dpPath,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51950", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        Assert.Equal("NT51929", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            dpPath,
            viewModel.Merge.MergeSlots.Single(slot =>
                slot.SlotId == CompositionAddressSpaceIds.DpAbInput).FilePath);

        viewModel.WorkflowSession.AcceptFirmwareIcMismatchCommand.Execute(null);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
        Assert.Equal(
            ["single", "cascade"],
            viewModel.Merge.AbMergeTopologyChoices.Select(static choice => choice.Token));
        Assert.Equal("single", viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(
            dpPath,
            viewModel.Merge.MergeSlots.Single(slot =>
                slot.SlotId == CompositionAddressSpaceIds.DpAbInput).FilePath);
        Assert.Equal("NT51950", batches[^1].IcId);
        Assert.Contains(
            batches[^1].Inputs,
            input => input.InspectionId == CompositionAddressSpaceIds.DpAbInput && input.Path == dpPath);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }
}
