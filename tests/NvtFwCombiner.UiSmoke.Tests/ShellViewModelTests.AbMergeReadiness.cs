using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Verifies NT51950 accepts a TP BIN within the 256 KiB limit even when it exceeds the declared overlay span.</summary>
    [Fact]
    public async Task PreviewNt51950AcceptsTpInputWithinMaximum()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-950-negative");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.CanOpenReport);
        Assert.True(viewModel.Reports.HasReportToast);
        Assert.NotEmpty(viewModel.Reports.LoadedReport.Issues);
        Assert.All(viewModel.Reports.LoadedReport.Issues, static issue => Assert.Equal("warning", issue.Severity));
        Assert.False(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.True(viewModel.Reports.LoadedReport.HasInputs);
        Assert.True(viewModel.Reports.LoadedReport.HasOperations);
    }

    /// <summary>Informational AB facts cannot replace canonical session publication.</summary>
    [Fact]
    public async Task AbMergeFactsDoNotReplaceCanonicalStatus()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-canonical-gate");
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(null, null, null, null, null, null)
                {
                    AbMergeFacts = new AbMergeInputFacts(
                        input.AbMergeAddressSpaceId!,
                        []),
                })),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            path,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(static candidate =>
            candidate.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Equal(FirmwareInputInspectionSeverity.Valid, slot.InputInspectionSeverity);

        viewModel.WorkflowSession.SelectedNumber = "cascade";
        await CurrentInspection(viewModel).ActiveTask;

        Assert.Equal(path, slot.FilePath);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, slot.InputInspectionSeverity);
        Assert.True(slot.BlocksBuild);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

}
