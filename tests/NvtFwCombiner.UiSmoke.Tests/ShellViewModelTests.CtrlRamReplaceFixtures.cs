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
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
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
                Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
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

    /// <summary>Verifies sentinel CtrlRAM inputs land in each selected NT51927 multi-chip output range.</summary>
    [Theory]
    [InlineData("nt51927-2chip-self-20260705", 8, "replace-ctrlram-vn-slave-r")]
    [InlineData("nt51927-3chip-self-20260705", 12, "replace-ctrlram-vn-slave-l")]
    public async Task CtrlRamReplaceSentinelInputsReachNt51927MultiChipRanges(
        string caseId,
        int expectedSlotCount,
        string expectedVnSlotId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string fixtureRoot = Path.Combine(repositoryRoot, "testdata", "golden", "ctrlram-replace");
        string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement fixtureCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("id").GetString() == caseId);
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-sentinel-ctrlram-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = fixtureCase.GetProperty("ic").GetString()!;
            viewModel.SelectedNumber = fixtureCase.GetProperty("icNum").GetString()!;
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);
            viewModel.SetSlotFile("replace-base", RepositoryPaths.ManifestPath(fixtureRoot, fixtureCase.GetProperty("base")));

            List<(string SlotId, int Start, byte[] Bytes)> expectedWrites = [];
            int seedOffset = 0;
            foreach (JsonElement replacement in fixtureCase.GetProperty("replacementInputs").EnumerateArray())
            {
                string slotId = replacement.GetProperty("slotId").GetString()!;
                string regionName = replacement.GetProperty("regionName").GetString()!;
                FirmwareSlotViewModel slot = viewModel.ReplaceSlots.Single(candidate => candidate.SlotId == slotId);
                CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(candidate => candidate.Name == regionName);
                (int start, int length) = ParseCtrlRamRegion(region);
                byte[] sentinel = CreatePattern(length, unchecked((byte)(0x31 + (seedOffset * 0x17))));
                string replacementPath = Path.Combine(tempRoot, $"{slotId}.bin");
                File.WriteAllBytes(replacementPath, sentinel);

                Assert.Equal(regionName, slot.Title);
                viewModel.SetSlotFile(slotId, replacementPath);
                expectedWrites.Add((slotId, start, sentinel));
                seedOffset++;
            }

            Assert.Equal(expectedSlotCount, expectedWrites.Count);
            Assert.Contains(expectedWrites, item => item.SlotId == expectedVnSlotId && item.Bytes.Length == 0x1660);
            Assert.True(
                viewModel.PreviewReplaceCommand.CanExecute(null),
                viewModel.ReplacePreviewUnavailableReason);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.CanBuildReplace, viewModel.ReplaceBuildUnavailableReason);
            string outputPath = Path.Combine(tempRoot, $"{caseId}-sentinel-output.bin");

            await viewModel.BuildReplaceAsync(outputPath);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            byte[] output = File.ReadAllBytes(outputPath);
            foreach ((_, int start, byte[] expectedBytes) in expectedWrites)
            {
                Assert.Equal(expectedBytes, output[start..(start + expectedBytes.Length)]);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
