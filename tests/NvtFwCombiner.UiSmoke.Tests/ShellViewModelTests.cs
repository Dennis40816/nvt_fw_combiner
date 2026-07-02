using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Smoke coverage for shell view-model surfaces used by the Avalonia UI.</summary>
public sealed class ShellViewModelTests
{
    /// <summary>Verifies Settings exposes catalog-backed status without requiring workflow context.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.Equal("0.5.0-dev.0", viewModel.AppVersion);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "Built-in profiles" && row.Value.Contains("merge", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsToolRows, row => row.Title == "External tool binding" && row.Value.Contains("legacy-combiner-1.13.0", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row => row.Title == "Report review");
        Assert.Contains(viewModel.SettingsReadinessRows, row => row.Title == "Device context" && row.Value == "Workflow pages only");
    }

    /// <summary>Verifies breadcrumb history can return to an earlier page level.</summary>
    [Fact]
    public void NavigationTrailReturnsToEarlierPage()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowSettingsCommand.Execute(null);
        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Settings > Replace", viewModel.NavigationPath);

        viewModel.NavigationTrail[1].NavigateCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Settings", viewModel.NavigationPath);
    }

    /// <summary>Verifies Normal Merge hides IC Number while preserving row layout space.</summary>
    [Fact]
    public void NormalMergeHidesNumberSelectorButKeepsPlaceholder()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.True(viewModel.IsNormalMergeModeSelected);
        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950: refresh profile, slots, validation", viewModel.DeviceContextStatus);

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsNumberSelectorVisible);
        Assert.False(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950 / single: refresh profile, slots, validation", viewModel.DeviceContextStatus);
    }

    /// <summary>Verifies Standard Merge slots follow the selected profile instead of exposing LD globally.</summary>
    [Fact]
    public void MergeSlotsFollowSelectedProfileRequiredInputs()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51926";

        Assert.Equal(["DP BIN", "TP BIN"], viewModel.MergeSlots.Select(slot => slot.Title));
        Assert.DoesNotContain(viewModel.MergeSlots, slot => slot.Title.Contains("LD", StringComparison.Ordinal));

        viewModel.SelectedIc = "NT51928";

        Assert.Equal(["DP BIN", "TP BIN", "LD BIN"], viewModel.MergeSlots.Select(slot => slot.Title));
    }

    /// <summary>Verifies General Replace authors base BIN and explicit range rows as separate UI state.</summary>
    [Fact]
    public void GeneralReplaceUsesIndependentBaseAndEditableMappings()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowGeneralReplaceCommand.Execute(null);

        Assert.True(viewModel.IsGeneralReplaceModeSelected);
        Assert.False(viewModel.IsStructuredReplaceModeSelected);
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.Equal("replace-base", viewModel.ReplaceBaseSlot.SlotId);
        Assert.NotEmpty(viewModel.ReplaceCoverageSegments);
        Assert.Contains("len 0x", viewModel.ReplaceMemoryRangeLabel, StringComparison.Ordinal);
        Assert.Equal(
            "Preview blocked: base BIN, replacement BIN, and an approved range are required.",
            viewModel.ReplaceReadinessStatus);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);

        viewModel.AddGeneralReplaceMappingCommand.Execute(null);
        Assert.Equal(2, viewModel.GeneralReplaceMappings.Count);

        viewModel.RemoveGeneralReplaceMappingRow(viewModel.GeneralReplaceMappings[0]);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);
        Assert.Equal(1, viewModel.GeneralReplaceMappings[0].Index);
    }

    /// <summary>Verifies Replace keeps the same visual-first coverage model as Merge.</summary>
    [Fact]
    public void ReplaceCoverageUsesReadableInclusiveSegments()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowDpReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.NotEmpty(viewModel.ReplaceCoverageSegments);
        Assert.All(viewModel.ReplaceCoverageSegments, segment =>
        {
            Assert.Contains("-", segment.RangeLabel, StringComparison.Ordinal);
            Assert.Contains("len 0x", segment.RangeLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("..", segment.RangeLabel, StringComparison.Ordinal);
        });
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.SourceLabel == "Restored TP");
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.SourceLabel is "Changed DP BIN" or "Changed LDC BIN");
        Assert.Equal(
            "Preview blocked: base BIN and required DP replacement inputs are required.",
            viewModel.ReplacePreviewUnavailableReason);
        Assert.Equal("Build blocked: run a valid DP Preview first.", viewModel.ReplaceBuildUnavailableReason);
    }

    /// <summary>Verifies DP Replace hides LDC except for the NT51928 evidence-backed slot.</summary>
    [Fact]
    public void DpReplaceSlotsExposeLdcOnlyForNt51928()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51927";
        viewModel.ShowDpReplaceCommand.Execute(null);

        Assert.DoesNotContain(viewModel.ReplaceSlots, slot => slot.Title.Contains("LDC", StringComparison.Ordinal));

        viewModel.SelectedIc = "NT51928";

        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "LDC replacement BIN");
    }

    /// <summary>Verifies NT51950 DP Replace writes a 0x100000 DP perspective image and restores TP bytes from base.</summary>
    [Fact]
    public async Task BuildNt51950DpReplaceRestoresBaseTpRange()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-dp-replace-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            byte[] baseBytes = CreatePattern(0x100000, 0x80);
            byte[] replacementBytes = CreatePattern(0x50000, 0x20);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement-dp.bin");
            string outputPath = Path.Combine(tempRoot, "nt51950-dp-replace.bin");
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(replacementPath, replacementBytes);

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51950";
            viewModel.ShowDpReplaceCommand.Execute(null);
            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile("replace-dp", replacementPath);

            Assert.True(viewModel.CanBuildReplace);

            await viewModel.BuildReplaceAsync(outputPath);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(File.Exists(outputPath), outputPath);
            byte[] output = File.ReadAllBytes(outputPath);
            Assert.Equal(0x100000, output.Length);
            Assert.Equal(replacementBytes[0x9FFF], output[0x9FFF]);
            Assert.Equal(baseBytes[0x0A000], output[0x0A000]);
            Assert.Equal(baseBytes[0x36FFF], output[0x36FFF]);
            Assert.Equal(replacementBytes[0x37000], output[0x37000]);
            Assert.Equal(0, output[0x50000]);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-tp", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies CtrlRAM Replace exposes per-region slots and reports generated postbuild commands.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsPostbuildCommandTrace()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string regionPath = Path.Combine(tempRoot, "ctrlram.bin");
            File.WriteAllBytes(basePath, CreatePattern(0x40000, 0x10));
            File.WriteAllBytes(regionPath, CreatePattern(0x4000, 0x50));

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = "2";
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);

            FirmwareSlotViewModel regionSlot = viewModel.ReplaceSlots.First(slot =>
                slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal));
            Assert.True(regionSlot.IsOptional);
            Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);

            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile(regionSlot.SlotId, regionPath);

            Assert.True(viewModel.CanPreviewReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
                operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal));
            Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.IsChanged);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies CtrlRAM Replace can preview a golden-backed fake VN slot with traceable region naming.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsGoldenBackedFakeVnSlot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51927");
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-vn-ctrlram-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base-from-golden.bin");
            string fakeVnPath = Path.Combine(tempRoot, "fake-vn-ctrlram.bin");
            File.Copy(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")), basePath);
            File.WriteAllBytes(fakeVnPath, CreatePattern(0x1660, 0x70));

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = "3";
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);

            FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
            Assert.Equal("Replace this area only when needed. TP position 0x2EBD0-0x3022F (len 0x1660).", vnLeft.Description);

            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile(vnLeft.SlotId, fakeVnPath);

            Assert.True(viewModel.CanPreviewReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Title.Contains("replace-vn-slave-l", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal));
            Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
                segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
                segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
            Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave L");
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
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.Equal("0 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains("Preview blocked", viewModel.ReplaceSelectionStatusLabel, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "Base flash BIN");
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "CtrlRAM replacement");

        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        viewModel.SetSlotFile("replace-base", Path.Combine(Path.GetTempPath(), "base.bin"));
        viewModel.SetSlotFile(vnLeft.SlotId, Path.Combine(Path.GetTempPath(), "vn-slave-l.bin"));

        Assert.Equal("1 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Equal("Ready for Preview", viewModel.ReplaceSelectionStatusLabel);
        Assert.Empty(viewModel.ReplaceSelectionMissingRows);
        Assert.Contains(viewModel.ReplaceSelectionRows, row =>
            row.Title == "VN CtrlRAM (Slave L)" &&
            row.Detail == "vn-slave-l.bin" &&
            row.Meta.Contains("0x2EBD0-0x3022F", StringComparison.Ordinal));
        Assert.Contains("Preview will generate", viewModel.ReplaceSelectionRunHint, StringComparison.Ordinal);

        Assert.False(viewModel.IsReplaceSelectionModalOpen);
        viewModel.ShowReplaceSelectionCommand.Execute(null);
        Assert.True(viewModel.IsReplaceSelectionModalOpen);
        viewModel.CloseReplaceSelectionCommand.Execute(null);
        Assert.False(viewModel.IsReplaceSelectionModalOpen);
    }

    /// <summary>Verifies reports stay behind the icon entry until explicitly opened.</summary>
    [Fact]
    public void ReportReviewUsesToastAndModalState()
    {
        const string json = /*lang=json,strict*/ """
            {
              "ProfileId": "nt51927-standard-merge-gen-flash",
              "IcId": "NT51927",
              "ExperienceId": "standard-merge",
              "CompositionKind": "Merge",
              "RunId": "ui-smoke",
              "StartedAtUtc": "2026-07-01T00:00:00Z",
              "Inputs": [],
              "Operations": [],
              "Mutations": [],
              "Issues": [],
              "Output": {
                "FileName": "preview.bin",
                "Size": 0,
                "Committed": false,
                "Sha256": "abcdef"
              }
            }
            """;
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.False(viewModel.CanOpenReport);
        Assert.False(viewModel.ShowReportCommand.CanExecute(null));

        viewModel.LoadReportJson(json, "preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal(1, viewModel.ReportToastOpacity);
        Assert.Equal(json, viewModel.LoadedReportJson);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927).json", viewModel.ReportSaveFileName);
        Assert.True(viewModel.ShowReportCommand.CanExecute(null));
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);

        viewModel.ShowReportCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal(0, viewModel.ReportToastOpacity);

        viewModel.CloseReportCommand.Execute(null);

        Assert.False(viewModel.IsReportModalOpen);
    }

    /// <summary>Verifies memory-map rows expose readable operation details without relying on tooltips.</summary>
    [Fact]
    public void MergeMemoryRowsExposeReadableOperationDetails()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51926";

        MemoryMapRowViewModel copyRow = Assert.Single(
            viewModel.MergeMemoryRows,
            row => row.RangeLabel == "0x00000-0x3BFFF (len 0x3C000)" && row.ActionLabel == "Copy");
        Assert.Equal("Blank output 0x00 -> TP BIN", copyRow.FlowLabel);
        Assert.Contains("Sequence 100", copyRow.Detail, StringComparison.Ordinal);
        Assert.Contains("Reason:", copyRow.Detail, StringComparison.Ordinal);
    }

    /// <summary>Verifies the Merge ViewModel command path builds each approved golden case byte-for-byte.</summary>
    [Theory]
    [MemberData(nameof(StandardMergeGoldenCases))]
    public async Task BuildMergeFromViewModelMatchesGolden(string ic)
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == ic);
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-{ic}-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = $"NT{ic}";

            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                string sourcePath = ManifestPath(goldenRoot, input.Value);
                string copiedPath = Path.Combine(tempRoot, $"{input.Name}.bin");
                File.Copy(sourcePath, copiedPath);
                viewModel.SetSlotFile(SlotIdForAddressSpace(input.Name), copiedPath);
            }

            Assert.True(viewModel.BuildMergeCommand.CanExecute(null));
            Assert.True(viewModel.CanBuildStandardMerge);

            string outputPath = Path.Combine(tempRoot, "selected-output.bin");
            await viewModel.BuildStandardMergeAsync(outputPath);

            string expectedPath = ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"));
            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.Equal(outputPath, viewModel.LastRunResult.Output);
            Assert.True(File.Exists(outputPath), outputPath);
            Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(outputPath));
            Assert.True(viewModel.HasLoadedReport);
            Assert.True(viewModel.HasReportToast);
            Assert.Equal(1, viewModel.ReportToastOpacity);
            Assert.Equal("Build report generated", viewModel.ReportToastText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies an NT51950 run with NT51926 TP input is blocked with a reopenable detailed report.</summary>
    [Fact]
    public async Task BuildNt51950WithNt51926InputsFailsWithDetailedReportAndNoOutput()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51926");
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-950-negative-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51950";

            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                string sourcePath = ManifestPath(goldenRoot, input.Value);
                string copiedPath = Path.Combine(tempRoot, $"{input.Name}.bin");
                File.Copy(sourcePath, copiedPath);
                viewModel.SetSlotFile(SlotIdForAddressSpace(input.Name), copiedPath);
            }

            Assert.True(viewModel.CanBuildStandardMerge);

            string outputPath = Path.Combine(tempRoot, "should-not-exist.bin");
            await viewModel.BuildStandardMergeAsync(outputPath);

            Assert.False(viewModel.LastRunResult.Succeeded);
            Assert.Equal("Build blocked", viewModel.LastRunResult.Title);
            Assert.Equal("No output", viewModel.LastRunResult.Output);
            Assert.False(File.Exists(outputPath), outputPath);
            Assert.True(viewModel.HasLoadedReport);
            Assert.True(viewModel.CanOpenReport);
            Assert.True(viewModel.HasReportToast);
            ReportLineViewModel issue = Assert.Single(viewModel.LoadedReport.Issues);
            Assert.Equal("input.address-space.length-mismatch", issue.Title);
            Assert.Contains("tp-input", issue.Detail, StringComparison.Ordinal);
            Assert.Contains("actual 245760 bytes", issue.Detail, StringComparison.Ordinal);
            Assert.Contains("declared 225280 bytes", issue.Detail, StringComparison.Ordinal);
            Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
            Assert.Equal(issue.Title, viewModel.LoadedReport.PrimaryIssue.Title);
            Assert.True(viewModel.LoadedReport.HasInputs);
            Assert.True(viewModel.LoadedReport.HasOperations);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Gets every owner-approved gen_flash Standard Merge golden case.</summary>
    public static TheoryData<string> StandardMergeGoldenCases()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        TheoryData<string> cases = [];
        foreach (JsonElement goldenCase in manifestDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            cases.Add(goldenCase.GetProperty("ic").GetString()!);
        }

        return cases;
    }

    private static string SlotIdForAddressSpace(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "merge-dp",
            "tp-input" => "merge-tp",
            "ld-input" => "merge-ld",
            _ => throw new InvalidOperationException($"Unknown address space '{addressSpaceId}'."),
        };
    }

    private static string ManifestPath(string goldenRoot, JsonElement manifestFile)
    {
        string relativePath = manifestFile.GetProperty("path").GetString()!;
        return Path.Combine(goldenRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + (index % 251)));
        }

        return bytes;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SPEC.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
