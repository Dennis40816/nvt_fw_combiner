using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies an exact V2 CtrlRAM Replace build commits a real postbuild output file.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildCommitsGoldenBackedSelfReplacementOutput()
    {
        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        Assert.NotNull(fixtures);
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-build");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";
        OpenReplace(viewModel, "CtrlRAM");

        string outputPath = workspace.PathFor("ctrlram-build-output.bin");

        fixtures.SetBaseSlot(viewModel, fixtureCase);
        fixtures.SetReplacementSlots(viewModel, fixtureCase);

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
            Application.Composition.CompositionRunPhase.PreparingReport,
            viewModel.RunSession.CompositionProgress.CurrentPhase);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Equal(10, postbuild.RuntimeCommands.Count);
        Assert.All(postbuild.RuntimeCommands, command =>
            Assert.Contains("Combiner.exe", command.ArgumentListEvidence, StringComparison.OrdinalIgnoreCase));
        using var firstReportDocument = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        AssertAcceptedPostbuildOnlyOutputDifferences(firstReportDocument.RootElement, "postbuild-twochip");
    }

    /// <summary>Verifies owner-approved CtrlRAM Replace fixtures when a manifest is present.</summary>
    [Fact]
    public async Task CtrlRamReplaceFixtureCasesBuildWhenManifestPresent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        if (fixtures is null)
        {
            return;
        }

        foreach (JsonElement fixtureCase in fixtures.Cases)
        {
            Assert.Equal("CtrlRAM", fixtureCase.GetProperty("mode").GetString());
            string caseId = fixtureCase.GetProperty("id").GetString()!;
            using var workspace = TempWorkspace.Create("nvt-fw-combiner-private-ctrlram");
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.WorkflowSession.SelectedIc = fixtureCase.GetProperty("ic").GetString()!;
            viewModel.WorkflowSession.SelectedNumber = fixtureCase.GetProperty("icNum").GetString()!;
            OpenReplace(viewModel, "CtrlRAM");
            fixtures.SetBaseSlot(viewModel, fixtureCase);
            fixtures.SetReplacementSlots(viewModel, fixtureCase);

            string outputPath = workspace.PathFor($"{caseId}.bin");
            Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null), caseId);

            await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
            Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
            if (caseId.StartsWith("nt51926-", StringComparison.Ordinal))
            {
                Assert.Contains(GetCommandOperations(viewModel.Reports.LoadedReport), operation =>
                    operation.Facts.Any(fact =>
                        fact.Label == "Processor" &&
                        fact.Value.Contains("nfc.nt51926.ctrlram-postbuild-fw1.4.1", StringComparison.Ordinal)) &&
                    operation.RangeRows.Any(range =>
                        range.Kind == "Processor write" &&
                        range.Range == "0x32F50-0x3304F (len 0x100)") &&
                    operation.RuntimeCommands.Any(command =>
                        command.ArgumentListEvidence.Contains("0x32F50", StringComparison.Ordinal)));
            }

            Assert.True(viewModel.Replace.CanBuildReplace, caseId);

            await viewModel.Replace.BuildReplaceAsync(outputPath);
            Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
            Assert.True(File.Exists(outputPath), outputPath);

            if (fixtures.EnforceExpectedOutput)
            {
                Assert.True(fixtures.TryGetExpectedOutputPath(fixtureCase, out string? expectedOutputPath), caseId);
                Assert.Equal(
                    File.ReadAllBytes(expectedOutputPath!),
                    File.ReadAllBytes(outputPath));
            }
        }
    }

}
