using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MergeWorkflowTests
{
    /// <summary>
    /// The public AB selector and accepted-session path commits the direct NT51929 Golden while
    /// preserving one shared physical TP source as two logical bindings.
    /// </summary>
    [Fact]
    public async Task Nt51929AbPublicSelectorLifecycleBuildsDirectGoldenFromOneTpFile()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ab-merge",
            "nt51929-ab-t05-d06");
        _ = CanonicalGoldenTestData.RequireDisposition(
            goldenCase,
            CanonicalGoldenTestDispositionKind.DirectFullOutput);
        JsonElement[] artifacts = [.. goldenCase.GetProperty("artifacts").EnumerateArray()];
        JsonElement dpArtifact = artifacts.Single(static artifact =>
            artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.DpAbInput);
        JsonElement tpAArtifact = artifacts.Single(static artifact =>
            artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.TpAInput);
        JsonElement tpBArtifact = artifacts.Single(static artifact =>
            artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.TpBInput);
        JsonElement expectedArtifact = artifacts.Single(static artifact =>
            artifact.GetProperty("role").GetString() == "expected");
        string canonicalDpPath = CanonicalGoldenTestData.ArtifactPath(dpArtifact);
        string canonicalTpAPath = CanonicalGoldenTestData.ArtifactPath(tpAArtifact);
        string canonicalTpBPath = CanonicalGoldenTestData.ArtifactPath(tpBArtifact);
        Assert.Equal(canonicalTpAPath, canonicalTpBPath);
        byte[] originalDp = File.ReadAllBytes(canonicalDpPath);
        byte[] originalTp = File.ReadAllBytes(canonicalTpAPath);
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51929-ab-direct-golden");
        string selectedDpPath = workspace.Write("inputs/dp-ab.bin", originalDp);
        string selectedTpPath = workspace.Write("inputs/shared-tp.bin", originalTp);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            selectedDpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            selectedTpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            selectedTpPath,
            TestContext.Current.CancellationToken);

        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.True(slot.HasFile));
        Assert.Equal(
            selectedTpPath,
            viewModel.Merge.MergeSlots.Single(static slot =>
                slot.SlotId == CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Equal(
            selectedTpPath,
            viewModel.Merge.MergeSlots.Single(static slot =>
                slot.SlotId == CompositionAddressSpaceIds.TpBInput).FilePath);
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Merge.BuildMergeCommand.CanExecute(null));

        string outputPath = workspace.PathFor("nt51929-ab-output.bin");
        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.Equal(
            File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(expectedArtifact)),
            File.ReadAllBytes(outputPath));
        Assert.Equal(originalDp, File.ReadAllBytes(selectedDpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(selectedTpPath));
        Assert.Equal(originalDp, File.ReadAllBytes(canonicalDpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(canonicalTpAPath));
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        Assert.Equal("NT51929", report.RootElement.GetProperty("IcId").GetString());
        Assert.Equal("nt51929-ab-merge", report.RootElement.GetProperty("ProfileId").GetString());
        Assert.True(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
        Assert.Equal(3, report.RootElement.GetProperty("Inputs").GetArrayLength());
    }
}
