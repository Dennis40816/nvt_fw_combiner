using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamExternalGoldenTests
{
    /// <summary>Verifies an exact V2 CtrlRAM Replace build commits a real postbuild output file.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildCommitsGoldenBackedSelfReplacementOutput()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-build");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        string outputPath = workspace.PathFor("ctrlram-build-output.bin");

        CanonicalCtrlRamTestData.SetBaseSlot(viewModel, fixtureCase);
        CanonicalCtrlRamTestData.SetReplacementSlots(viewModel, fixtureCase);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.Replace.CanBuildReplace);

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Replace.CanBuildReplace);

        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(0x40000, new FileInfo(outputPath).Length);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(outputPath, viewModel.Reports.LoadedReport.OutputArtifactPath);
        Assert.Equal(
            CompositionRunPhase.PreparingReport,
            viewModel.RunSession.CompositionProgress.CurrentPhase);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Equal(10, postbuild.RuntimeCommands.Count);
        Assert.All(postbuild.RuntimeCommands, command =>
            Assert.Contains("Combiner.exe", command.ArgumentListEvidence, StringComparison.OrdinalIgnoreCase));
        using var firstReportDocument = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        AssertAcceptedPostbuildOnlyOutputDifferences(firstReportDocument.RootElement, "postbuild-twochip");
    }

    /// <summary>Verifies the second input-only canonical topology is a mandatory UI build smoke.</summary>
    [Fact]
    public async Task CanonicalThreeChipCtrlRamInputEvidenceBuilds()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-canonical-ctrlram");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = fixtureCase.GetProperty("ic").GetString()!;
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);
        CanonicalCtrlRamTestData.SetBaseSlot(viewModel, fixtureCase);
        viewModel.WorkflowSession.SelectedNumber = "3";
        await CurrentInspection(viewModel).ActiveTask;
        CanonicalCtrlRamTestData.SetReplacementSlots(viewModel, fixtureCase);

        string outputPath = workspace.PathFor("nt51927-3chip-self-20260705.bin");
        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Replace.CanBuildReplace);

        await viewModel.Replace.BuildReplaceAsync(outputPath);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath), outputPath);
    }

    /// <summary>
    /// Verifies the canonical NT51926 Common FW 1.4.1 selective VN case through
    /// the ViewModel preview and committed-build path.
    /// </summary>
    [Fact]
    public async Task CanonicalNt51926SelectiveVnPreviewAndBuildMatchArchivedOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        JsonElement standardMergeCase = CanonicalGoldenTestData.LoadDirectCase(
            "standard-merge",
            "nt51926-gen-flash");
        JsonElement ctrlRamCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw141-cascade2-auto-prj-597-20260717");
        string basePath = CanonicalGoldenTestData.ArtifactPath(
            CanonicalGoldenTestData.Artifact(standardMergeCase, "tp-input"));
        string vnPath = CanonicalGoldenTestData.ArtifactPath(
            CanonicalGoldenTestData.Artifact(ctrlRamCase, "postbuild-vn-ctrlram"));
        string expectedPath = CanonicalGoldenTestData.ArtifactPath(
            CanonicalGoldenTestData.Artifact(ctrlRamCase, "selective-vn-regression-output"));

        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51926-selective-vn");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-vn",
            vnPath,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Contains(postbuild.Facts, fact =>
            fact.Label == "Processor" &&
            fact.Value.Contains("nfc.nt51926.ctrlram-postbuild-fw1.4.1", StringComparison.Ordinal));
        Assert.Contains(postbuild.RangeRows, range =>
            range.Kind == "Processor write" &&
            range.Range == "0x32F50-0x3304F (len 0x100)");
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("0x32F50", StringComparison.Ordinal));
        Assert.True(viewModel.Replace.CanBuildReplace);

        string outputPath = workspace.PathFor("nt51926-selective-vn.bin");
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(outputPath));
    }

}
