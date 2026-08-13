using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>TP selection requests paired TP/DP projections in one batch and DP cannot alter Number.</summary>
    [Fact]
    public async Task MergeTpSelectionUsesOneBatchAndDpCannotApplyVerifiedContext()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement fixture = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("tp-input"));
        var batches = new List<FirmwareInspectionSnapshotInput[]>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches.Add([.. inputs]);
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot(
                        null,
                        null,
                        input.InspectionId == "merge-dp" ? new DpVersionMetadata("0102") : null,
                        null,
                        input.InspectionId == "merge-dp"
                            ? new FirmwareContextSuggestion("NT51926", "cascade", 2, "1.4.1", 0x5102)
                            : null,
                        null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        FirmwareInspectionSnapshotInput[] pairedBatch = Assert.Single(
            batches,
            static batch => batch.Length == 2);
        Assert.Equal(["merge-dp", "merge-tp"], pairedBatch.Select(static input => input.InspectionId));
        Assert.Equal(tpPath, pairedBatch.Single(input => input.InspectionId == "merge-dp").TpPath);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);

        MainWindowViewModel incomplete = CreateBatchInspectionViewModel((_, inputs) =>
        [
            new FirmwareInspectionSnapshotResult(
                inputs[0].InspectionId,
                DpInspection(inputs.Count == 1 ? "0102" : "0202")),
        ]);
        incomplete.WorkflowSession.SelectedIc = "NT51926";
        await incomplete.WorkflowSession.SetSlotFileAsync(
            "merge-dp",
            dpPath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel incompleteDp = incomplete.Merge.MergeSlots.Single(slot =>
            slot.SlotId == "merge-dp");
        Assert.Contains(incompleteDp.FirmwareFacts, fact => fact.Value == "D01-02");

        await incomplete.WorkflowSession.SetSlotFileAsync(
            "merge-tp",
            tpPath,
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowInspectionAttemptState.Failed, incomplete.Merge.Inspection.State);
        Assert.True(incomplete.Merge.Inspection.Loading.CanRetry);
        Assert.Contains(incompleteDp.FirmwareFacts, fact => fact.Value == "D01-02");
        Assert.DoesNotContain(incompleteDp.FirmwareFacts, fact => fact.Value == "D02-02");
    }
}
