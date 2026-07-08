using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM slots refresh to the FWConfig-selected postbuild category after base load.</summary>
    [Fact]
    public void CtrlRamBaseFirmwareRefreshesVersionedNt51926Slots()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = "cascade";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("len 0x149E", StringComparison.Ordinal));

        viewModel.SetSlotFile("replace-base", basePath);

        Assert.Contains(viewModel.CtrlRamRegions, region =>
            region.Name == "VN CtrlRAM" &&
            region.SizeHex == "len 0x1660");
        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("len 0x1660", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM" &&
            segment.RangeLabel == "0x315D0-0x32C2F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM plan rows promote readable region labels over raw postbuild filenames.</summary>
    [Fact]
    public void CtrlRamPlanRowsExposeReadablePrimaryLabels()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace + CRC" &&
            row.AfterSource == "NF_Ctrlram.bin" &&
            row.PrimaryLabel == "NF CtrlRAM");
        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace + CRC" &&
            row.AfterSource == "Normal_Ctrlram.bin" &&
            row.PrimaryLabel == "Normal CtrlRAM");
        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace + CRC" &&
            row.AfterSource == "VN_Ctrlram.bin" &&
            row.PrimaryLabel == "VN CtrlRAM");
        Assert.All(
            viewModel.ReplaceMemoryRows.Where(row => row.ActionLabel == "Replace + CRC"),
            row =>
            {
                Assert.DoesNotContain(".bin", row.PrimaryLabel, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("_", row.PrimaryLabel, StringComparison.Ordinal);
            });
    }

    /// <summary>Verifies CtrlRAM Replace exposes per-region slots and reports generated postbuild commands.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsPostbuildCommandTrace()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram");
        string basePath = workspace.Write("base.bin", baseBytes);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "2";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel regionSlot = viewModel.ReplaceSlots.First(slot =>
            slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal));
        Assert.True(regionSlot.IsOptional);
        Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);
        CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(item => item.Name == regionSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        string regionPath = workspace.Write("ctrlram.bin", baseBytes[start..(start + length)]);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(regionSlot.SlotId, regionPath);

        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.LoadedReport.HasCommandOperations);
        Assert.False(viewModel.LoadedReport.HasStepOperations);
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
            operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
            operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.IsChanged);
    }

    /// <summary>Verifies one CtrlRAM Replace run can select and report multiple region replacements.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsMultipleSelectedRegions()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-multi");
        string basePath = workspace.Write("base.bin", baseBytes);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel normalRight = viewModel.ReplaceSlots.Single(slot => slot.Title == "Normal CtrlRAM (Slave R)");
        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        (int normalRightStart, int normalRightLength) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == normalRight.Title));
        (int vnLeftStart, int vnLeftLength) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == vnLeft.Title));
        string normalRightPath = workspace.Write("normal-slave-r.bin", baseBytes[normalRightStart..(normalRightStart + normalRightLength)]);
        string vnLeftPath = workspace.Write("vn-slave-l.bin", baseBytes[vnLeftStart..(vnLeftStart + vnLeftLength)]);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(normalRight.SlotId, normalRightPath);
        viewModel.SetSlotFile(vnLeft.SlotId, vnLeftPath);

        Assert.Equal("2 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "VN CtrlRAM (Slave L)");
        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.CodeBlock.Contains("Normal_Ctrlram_R.bin", StringComparison.Ordinal) &&
            operation.CodeBlock.Contains("Normal_Ctrlram_L.bin", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Normal CtrlRAM (Slave R)" &&
            segment.RangeLabel == "0x207D0-0x237CF (len 0x3000)");
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM Replace can preview a golden-backed VN self replacement with traceable region naming.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsGoldenBackedVnSelfReplacement()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-vn-ctrlram");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        string basePath = workspace.Write("base-from-golden.bin", baseBytes);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        Assert.Equal("Replace this area only when needed. TP position 0x2EBD0-0x3022F (len 0x1660).", vnLeft.Description);
        (int start, int length) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == vnLeft.Title));
        string vnPath = workspace.Write("vn-ctrlram.bin", baseBytes[start..(start + length)]);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(vnLeft.SlotId, vnPath);

        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.HasCodeBlock &&
            operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave L");
    }

    /// <summary>Verifies a CtrlRAM replacement sliced from the same base runs through the real postbuild path.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewSelfReplacementRunsPostbuild()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-self");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel vnLeftSlot = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        CtrlRamRegionViewModel vnLeftRegion = viewModel.CtrlRamRegions.Single(region => region.Name == "VN CtrlRAM (Slave L)");
        (int start, int length) = ParseCtrlRamRegion(vnLeftRegion);
        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        string replacementPath = workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]);

        byte[] simulatedBeforePostbuild = [.. baseBytes];
        File.ReadAllBytes(replacementPath).CopyTo(simulatedBeforePostbuild.AsSpan(start, length));
        Assert.Equal(baseBytes, simulatedBeforePostbuild);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(vnLeftSlot.SlotId, replacementPath);

        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.HasCodeBlock &&
            operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        AssertAcceptedPostbuildOnlyOutputDifferences(reportDocument.RootElement, "postbuild-threechip");
    }

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

        Assert.True(viewModel.CanPreviewReplace);
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
            Assert.True(viewModel.CanPreviewReplace, caseId);

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
            Assert.True(viewModel.CanPreviewReplace, viewModel.ReplacePreviewUnavailableReason);

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

    /// <summary>Verifies NT51927 three-chip CtrlRAM Replace exposes both right and left slave slots.</summary>
    [Fact]
    public void CtrlRamReplaceSlotsIncludeNt51927RightAndLeftSlaves()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "Normal CtrlRAM (Slave L)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "MP CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "MP CtrlRAM (Slave L)");
        Assert.Contains(viewModel.CtrlRamRegions, region => region.Name == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.CtrlRamRegions, region => region.Name == "Normal CtrlRAM (Slave L)");
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Master" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Slave R" && !group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Slave L" && !group.IsExpanded);
        Assert.True(viewModel.IsReplaceCoverageGrouped);
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Master" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave R" && !group.IsExpanded);
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave L" && !group.IsExpanded);
    }

    /// <summary>Verifies the Replace selection overview keeps collapsed CtrlRAM choices discoverable.</summary>
    [Fact]
    public void ReplaceSelectionOverviewTracksSelectedCtrlRamTargets()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-selection");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.Equal("0 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains("Build blocked", viewModel.ReplaceSelectionStatusLabel, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "Base flash BIN");
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "CtrlRAM replacement");
        FirmwareSlotGroupViewModel slaveLGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Slave L");
        Assert.Equal("0/4", slaveLGroup.CountLabel);
        Assert.Equal("4 areas. None selected.", slaveLGroup.SelectionSummary);

        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        viewModel.SetSlotFile("replace-base", workspace.PathFor("base.bin"));
        viewModel.SetSlotFile(vnLeft.SlotId, workspace.PathFor("vn-slave-l.bin"));

        slaveLGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Slave L");
        Assert.Equal("1 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Equal("1/4", slaveLGroup.CountLabel);
        Assert.Equal("1 selected / 4 areas.", slaveLGroup.SelectionSummary);
        Assert.Equal("Ready for Build", viewModel.ReplaceSelectionStatusLabel);
        Assert.Empty(viewModel.ReplaceSelectionMissingRows);
        Assert.Contains(viewModel.ReplaceSelectionRows, row =>
            row.Title == "VN CtrlRAM (Slave L)" &&
            row.Detail == "vn-slave-l.bin" &&
            row.Meta.Contains("0x2EBD0-0x3022F", StringComparison.Ordinal));
        Assert.Contains("Build will validate", viewModel.ReplaceSelectionRunHint, StringComparison.Ordinal);

        Assert.False(viewModel.IsReplaceSelectionModalOpen);
        viewModel.ShowReplaceSelectionCommand.Execute(null);
        Assert.True(viewModel.IsReplaceSelectionModalOpen);
        viewModel.CloseReplaceSelectionCommand.Execute(null);
        Assert.False(viewModel.IsReplaceSelectionModalOpen);
    }
}
