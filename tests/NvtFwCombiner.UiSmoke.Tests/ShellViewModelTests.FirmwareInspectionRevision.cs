using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Each explicit CtrlRAM reinspection advances one coherent canonical authoring revision.</summary>
    [Fact]
    public async Task RepeatedCtrlRamInspectionAdvancesCanonicalBatchRevision()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-authoring-revision");
        string referencePath = workspace.Write("reference.bin", ReadCtrlRamReference());
        string firstDpPath = workspace.Write("dp-first.bin", ReadCtrlRamNormalSource());
        string secondDpPath = workspace.Write("dp-second.bin", ReadCtrlRamNormalSource());
        var revisions = new List<long>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            revisions.AddRange(inputs.Select(static input => input.AuthoringRevision));
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile(CompositionSlotIds.ReplaceBase, referencePath);
        await CurrentInspection(viewModel).ActiveTask;
        viewModel.SetSlotFile("replace-ctrlram-normal", firstDpPath);
        await CurrentInspection(viewModel).ActiveTask;

        revisions.Clear();
        await viewModel.WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync();
        long stableRevision = Assert.Single(revisions.Distinct());
        revisions.Clear();
        await viewModel.WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync();
        long repeatedRevision = Assert.Single(revisions.Distinct());
        Assert.True(repeatedRevision > stableRevision);

        revisions.Clear();
        viewModel.SetSlotFile("replace-ctrlram-normal", secondDpPath);
        await CurrentInspection(viewModel).ActiveTask;
        Assert.True(Assert.Single(revisions.Distinct()) > repeatedRevision);
    }
}
