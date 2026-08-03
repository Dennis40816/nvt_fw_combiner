using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Informational AB facts cannot replace canonical session publication.</summary>
    [Fact]
    public async Task AbMergeFactsDoNotReplaceCanonicalStatus()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-canonical-gate");
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                input.InspectionId,
                new WorkbenchFirmwareInspection(null, null, null, null, null, null)
                {
                    AbMergeFacts = new WorkbenchAbMergeInputFacts(
                        input.AbMergeAddressSpaceId!,
                        []),
                })),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        foreach (string slotId in new[]
                 {
                     CompositionAddressSpaceIds.DpAbInput,
                     CompositionAddressSpaceIds.TpAInput,
                     CompositionAddressSpaceIds.TpBInput,
                 })
        {
            await viewModel.WorkflowSession.SetSlotFileAsync(
                slotId,
                workspace.Write($"{slotId}.bin", [0xA5]),
                TestContext.Current.CancellationToken);
        }

        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>A topology transition rejects terminal health from the former exact compilation.</summary>
    [Fact]
    public async Task Nt51950TopologyChangeReinspectsSelectedAbInput()
    {
        const int singleCapacity = 0x80000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-topology-readiness");
        byte[] dp = new byte[singleCapacity];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, singleCapacity / 2, major: 0x07, minor: 0x08, jira: 0x456);
        string path = workspace.Write("single-dp-ab.bin", dp);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            path,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(static candidate =>
            candidate.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Equal(WorkbenchInputInspectionSeverity.Valid, slot.InputInspectionSeverity);

        viewModel.WorkflowSession.SelectedNumber = "cascade";
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal(path, slot.FilePath);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, slot.InputInspectionSeverity);
        Assert.True(slot.BlocksBuild);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

}
