using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>Detailed Vector CtrlRAM uses its distinct typed presentation role.</summary>
    [Fact]
    public void VectorCtrlRamUsesDedicatedCoverageFillRole()
    {
        Assert.Equal(
            MemoryCoverageFillRole.CtrlRamVector,
            UiCompositionRunner.ResolveCtrlRamCoverageFillRole(CtrlRamRegionRole.Vector));
    }

    /// <summary>Verifies NT51926 keeps DiffDLM in a dedicated cascade group.</summary>
    [Fact]
    public void Nt51926DiffDlmBelongsToCascade()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal(["Cascade", "Common"], viewModel.Replace.ReplaceSlotGroups.Select(group => group.Title));
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups[0].Slots, slot => slot.SlotId == "replace-ctrlram-diff");
        Assert.DoesNotContain(viewModel.Replace.ReplaceSlotGroups[1].Slots, slot => slot.SlotId == "replace-ctrlram-diff");
        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
    }
}
