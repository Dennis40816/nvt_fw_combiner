using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class UiPerformanceObservationTests
{
    /// <summary>An oversized CtrlRAM base reaches the shared bound before any full-image allocation.</summary>
    [Fact]
    public async Task CtrlRamBaseSelectionRejectsOneBytePastSharedLimitWithoutFullAllocation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-bounded-base");
        string path = workspace.PathFor("oversized-base.bin");
        await using (var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.Asynchronous))
        {
            stream.SetLength(CompiledInputArtifactInspectionService.MaximumContentReadBytes + 1);
            await stream.FlushAsync(TestContext.Current.CancellationToken);
        }

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        Exception? exception = await Record.ExceptionAsync(() =>
            viewModel.WorkflowSession.SetSlotFileAsync(
                CompositionSlotIds.ReplaceBase,
                path,
                TestContext.Current.CancellationToken));
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        Assert.Null(exception);
        Assert.InRange(allocated, 0, 32 * 1024 * 1024);
        Assert.Equal(WorkflowInspectionAttemptState.Failed, viewModel.Replace.Inspection.State);
        Assert.Equal(
            FirmwareInputInspectionSeverity.Blocking,
            viewModel.Replace.ReplaceBaseSlot.InputInspectionSeverity);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }
}

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>A malformed CtrlRAM base locator is terminal user input, not a synchronous shell exception.</summary>
    [Fact]
    public async Task CtrlRamBaseSelectionRejectsMalformedLocatorWithoutThrowing()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Exception? exception = await Record.ExceptionAsync(() =>
            viewModel.WorkflowSession.SetSlotFileAsync(
                CompositionSlotIds.ReplaceBase,
                "invalid\0base.bin",
                TestContext.Current.CancellationToken));

        Assert.Null(exception);
        Assert.Equal(WorkflowInspectionAttemptState.Failed, viewModel.Replace.Inspection.State);
        Assert.Equal(
            FirmwareInputInspectionSeverity.Blocking,
            viewModel.Replace.ReplaceBaseSlot.InputInspectionSeverity);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }
}
