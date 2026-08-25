using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>
    /// NT51951 follows the complete public DP-first lifecycle: TP remains gated until DP fixes the
    /// capacity, DP publishes its typed TP prerequisite, and the accepted pair builds the direct
    /// owner Golden byte-for-byte through the product host.
    /// </summary>
    [Fact]
    public async Task Nt51951StandardMergeDpFirstPublicLifecycleBuildsDirectGolden()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51951-dp-first");
        JsonElement goldenCase = golden.CaseByIc("51951");
        string canonicalDpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string canonicalTpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        byte[] originalDp = File.ReadAllBytes(canonicalDpPath);
        byte[] originalTp = File.ReadAllBytes(canonicalTpPath);
        string selectedDpPath = workspace.Write("inputs/dp-input.bin", originalDp);
        string selectedTpPath = workspace.Write("inputs/tp-input.bin", originalTp);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51951";
        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tpSlot = viewModel.Merge.MergeSlots.Single(slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);

        Assert.True(dpSlot.CanSelectFile);
        Assert.False(tpSlot.CanSelectFile);
        Assert.True(tpSlot.IsSemanticStatePendingInput);
        Assert.Equal("Waiting for DP BIN", tpSlot.SelectionReadinessLabel);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            selectedTpPath,
            TestContext.Current.CancellationToken);

        Assert.False(tpSlot.HasFile);
        Assert.Null(tpSlot.FilePath);
        Assert.Null(tpSlot.CurrentInspectionProjection);
        Assert.True(tpSlot.IsSemanticStatePendingInput);
        Assert.False(viewModel.Merge.CanBuildMerge);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            selectedDpPath,
            TestContext.Current.CancellationToken);

        Assert.True(dpSlot.HasFile);
        Assert.True(tpSlot.CanSelectFile);
        Assert.False(tpSlot.IsSemanticStatePendingInput);
        FirmwareSlotFactViewModel pending = Assert.Single(dpSlot.FirmwareFacts);
        Assert.Equal("DP Version", pending.Label);
        Assert.Equal("Waiting for TP BIN", pending.Value);
        Assert.True(pending.IsPendingInput);
        Assert.False(viewModel.Merge.CanBuildMerge);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            selectedTpPath,
            TestContext.Current.CancellationToken);

        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.True(slot.HasFile));
        Assert.DoesNotContain(dpSlot.FirmwareFacts, static fact => fact.IsPendingInput || fact.IsUnknown);
        Assert.Contains(dpSlot.FirmwareFacts, static fact =>
            fact.Label == "DP Version" && fact.Value == "D05-00");
        Assert.Contains(dpSlot.FirmwareFacts, static fact =>
            fact.Label == "Jira Index" && fact.Value == "AUTO_PRJ-695");
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Merge.BuildMergeCommand.CanExecute(null));

        string outputPath = workspace.PathFor("nt51951-standard-merge.bin");
        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.Equal(golden.ReadExpectedOutput(goldenCase), File.ReadAllBytes(outputPath));
        Assert.Equal(originalDp, File.ReadAllBytes(selectedDpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(selectedTpPath));
        Assert.Equal(originalDp, File.ReadAllBytes(canonicalDpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(canonicalTpPath));
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        Assert.Equal("NT51951", report.RootElement.GetProperty("IcId").GetString());
        Assert.Equal(
            "nt51951-standard-merge-dp-perspective",
            report.RootElement.GetProperty("ProfileId").GetString());
        Assert.True(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }
}
