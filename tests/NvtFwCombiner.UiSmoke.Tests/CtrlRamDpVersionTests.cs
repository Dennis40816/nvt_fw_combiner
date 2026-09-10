using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Base DP metadata stays independent of selected CtrlRAM replacement payloads.</summary>
public sealed class CtrlRamDpVersionTests
{
    /// <summary>Real NT51950 inputs retain the same parsed Base facts through each replacement selection.</summary>
    [Fact]
    public async Task BaseDpVersionRemainsD86AfterEachCtrlRamSelection()
    {
        string fixture = RepositoryPaths.FromRepositoryRoot(
            "testdata/golden/canonical/NT51950/ctrlram-replace/fw2.0.0/single/" +
            "nt51950-fw200-single-auto-prj-676-20260717");
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        shell.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
        shell.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51950";
        shell.WorkflowSession.WorkflowContextSetup.SelectedNumber = "single";
        shell.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        await shell.WorkflowSession.SetSlotFileAsync("replace-base", Path.Combine(fixture,
            "expected/NT51950_Flashcode_BOE1540_Faurecia_Chery_D86T80_20260709.bin"),
            TestContext.Current.CancellationToken);
        FirmwareInspectionSnapshot original = shell.Replace.ReplaceBaseSlot.CurrentInspectionProjection!;
        Assert.Equal("D86-00", original.DpVersion?.DisplayValue);

        foreach (string region in new[] { "nf", "normal", "vn" })
        {
            await shell.WorkflowSession.SetSlotFileAsync($"replace-ctrlram-{region}", Path.Combine(fixture,
                $"inputs/postbuild/nt51950-postbuild-{region}-ctrlram.bin"),
                TestContext.Current.CancellationToken);
            FirmwareInspectionSnapshot current = shell.Replace.ReplaceBaseSlot.CurrentInspectionProjection!;
            Assert.Equal(original.FileStamp, current.FileStamp);
            Assert.Equal(original.FirmwareConfig, current.FirmwareConfig);
            Assert.Equal(original.DpVersion, current.DpVersion);
            Assert.Equal(original.CmiDpCode, current.CmiDpCode);
            Assert.Equal("D86-00", Assert.Single(shell.Replace.ReplaceBaseSlot.FirmwareFacts,
                fact => fact.Label == "DP Version").Value);
        }
        Assert.False(shell.Reports.HasLoadedReport);
        Assert.Empty(shell.Reports.ReportHistoryEntries);
    }
}
