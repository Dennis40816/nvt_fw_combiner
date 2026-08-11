using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Each explicit DP reinspection advances one coherent canonical authoring revision.</summary>
    [Fact]
    public async Task RepeatedDpInspectionAdvancesCanonicalBatchRevision()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-authoring-revision");
        string referencePath = workspace.Write("reference.bin", new byte[0x40000]);
        string firstDpPath = workspace.Write("dp-first.bin", new byte[0x40000]);
        string secondDpPath = workspace.Write("dp-second.bin", new byte[0x40000]);
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
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        viewModel.SetSlotFile(CompositionSlotIds.ReplaceBase, referencePath);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;
        viewModel.SetSlotFile(CompositionSlotIds.ReplaceDp, firstDpPath);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        revisions.Clear();
        await viewModel.WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync();
        long stableRevision = Assert.Single(revisions.Distinct());
        revisions.Clear();
        await viewModel.WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync();
        long repeatedRevision = Assert.Single(revisions.Distinct());
        Assert.True(repeatedRevision > stableRevision);

        revisions.Clear();
        viewModel.SetSlotFile(CompositionSlotIds.ReplaceDp, secondDpPath);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;
        Assert.True(Assert.Single(revisions.Distinct()) > repeatedRevision);
    }
}
