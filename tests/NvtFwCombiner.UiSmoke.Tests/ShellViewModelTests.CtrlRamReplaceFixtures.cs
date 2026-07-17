using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM Replace build commits a real postbuild output file.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildCommitsGoldenBackedSelfReplacementOutput()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-build");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "single";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel vnSlot = viewModel.ReplaceSlots.Single(slot =>
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        CtrlRamRegionViewModel vnRegion = viewModel.CtrlRamRegions.Single(region => region.Name == vnSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(vnRegion);
        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        string replacementPath = workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]);
        string outputPath = workspace.PathFor("ctrlram-build-output.bin");

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(vnSlot.SlotId, replacementPath);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildReplace);

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.LastRunResult.Output);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(baseBytes.Length, new FileInfo(outputPath).Length);
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(outputPath, viewModel.LoadedReport.OutputArtifactPath);
        Assert.Contains(GetCommandOperations(viewModel.LoadedReport), operation =>
            operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        using var firstReportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        AssertAcceptedPostbuildOnlyOutputDifferences(firstReportDocument.RootElement, "postbuild-single");

        byte[] postbuildCleanBytes = File.ReadAllBytes(outputPath);
        string cleanBasePath = workspace.Write("postbuild-clean-base.bin", postbuildCleanBytes);
        string cleanReplacementPath = workspace.Write("postbuild-clean-self-vn-ctrlram.bin", postbuildCleanBytes[start..(start + length)]);
        string cleanOutputPath = workspace.PathFor("postbuild-clean-output.bin");

        MainWindowViewModel cleanViewModel = ShellViewModelFactory.Create();
        cleanViewModel.SelectedIc = "NT51927";
        cleanViewModel.SelectedNumber = "single";
        cleanViewModel.ShowCtrlRamReplaceCommand.Execute(null);
        FirmwareSlotViewModel cleanVnSlot = cleanViewModel.ReplaceSlots.Single(slot => slot.Title == vnSlot.Title);
        cleanViewModel.SetSlotFile("replace-base", cleanBasePath);
        cleanViewModel.SetSlotFile(cleanVnSlot.SlotId, cleanReplacementPath);

        await cleanViewModel.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(cleanViewModel.CanBuildReplace);

        await cleanViewModel.BuildReplaceAsync(cleanOutputPath);

        Assert.True(cleanViewModel.LastRunResult.Succeeded, cleanViewModel.LastRunResult.Detail);
        Assert.True(cleanViewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(cleanOutputPath, cleanViewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal(postbuildCleanBytes, File.ReadAllBytes(cleanOutputPath));
        using var cleanReportDocument = JsonDocument.Parse(cleanViewModel.LoadedReportJson);
        AssertNoOutputDifferences(cleanReportDocument.RootElement);
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
            viewModel.SelectedIc = fixtureCase.GetProperty("ic").GetString()!;
            viewModel.SelectedNumber = fixtureCase.GetProperty("icNum").GetString()!;
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);
            fixtures.SetBaseSlot(viewModel, fixtureCase);
            fixtures.SetReplacementSlots(viewModel, fixtureCase);

            string outputPath = workspace.PathFor($"{caseId}.bin");
            Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null), caseId);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);
            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            if (caseId.StartsWith("nt51926-", StringComparison.Ordinal))
            {
                Assert.Contains(GetCommandOperations(viewModel.LoadedReport), operation =>
                    operation.Facts.Any(fact =>
                        fact.Label == "Processor" &&
                        fact.Value.Contains("nfc.nt51926.ctrlram-postbuild-fw1.4.1", StringComparison.Ordinal)) &&
                    operation.CodeBlock.Contains("0x32F50", StringComparison.Ordinal));
            }

            Assert.True(viewModel.CanBuildReplace, caseId);

            await viewModel.BuildReplaceAsync(outputPath);
            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
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
