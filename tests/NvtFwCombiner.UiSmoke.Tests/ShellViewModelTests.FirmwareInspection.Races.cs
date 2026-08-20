using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>A replacement selected during Base inspection starts a successor that retains both inputs.</summary>
    [Fact]
    public async Task CtrlRamReplacementSelectionPreservesPendingBaseInspection()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-base-replacement-race");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        using var firstBaseStarted = new ManualResetEventSlim();
        using var releaseFirstBase = new ManualResetEventSlim();
        int batches = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            if (Interlocked.Increment(ref batches) == 1)
            {
                firstBaseStarted.Set();
                releaseFirstBase.Wait(TestContext.Current.CancellationToken);
            }

            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        string replacementPath = workspace.Write("vn.bin", [0x01]);

        Task baseSelection = viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        Assert.True(firstBaseStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("Inspecting", viewModel.Replace.ReplaceReadinessStatus, StringComparison.Ordinal);

        Task replacementSelection = viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(1, batches);
        }
        finally
        {
            releaseFirstBase.Set();
        }
        await Task.WhenAll(baseSelection, replacementSelection);

        Assert.False(CurrentInspection(viewModel).IsRunning);
        Assert.NotEmpty(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts);
        Assert.NotEmpty(viewModel.Replace.CtrlRamRegions);
        Assert.Contains(
            viewModel.Replace.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
    }
}
