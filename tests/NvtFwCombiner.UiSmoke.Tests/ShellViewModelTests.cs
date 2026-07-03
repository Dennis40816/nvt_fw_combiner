using System.Globalization;
using System.Text.Json;
using Avalonia.Media;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
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
        Assert.Equal("0.5.0", viewModel.AppVersion);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "Built-in profiles" && row.Value.Contains("merge", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsToolRows, row => row.Title == "External tool binding" && row.Value.Contains("legacy-combiner-1.13.0", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row => row.Title == "Report review");
        Assert.Contains(viewModel.SettingsReadinessRows, row => row.Title == "Device context" && row.Value == "Workflow pages only");

        viewModel.SelectedTheme = "Dark";
        viewModel.SelectedStrictness = "Warn only";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("Dark theme is applied to this window.", viewModel.ThemePreferenceStatus);
        Assert.Equal("Preference is recorded; firmware gates still fail closed.", viewModel.StrictnessPreferenceStatus);
        Assert.Equal("Preference is recorded; full XAML localization is pending.", viewModel.LanguagePreferenceStatus);
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "Theme" &&
            row.Value == "Dark" &&
            row.Status == "Applied");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "Strictness" &&
            row.Value == "Warn only");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "Language" &&
            row.Value == "Traditional Chinese");
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

    /// <summary>Verifies required slot cards change tone when selected while optional slots keep the neutral tone.</summary>
    [Fact]
    public void FirmwareSlotCompletionToneHighlightsOnlyRequiredInputs()
    {
        FirmwareSlotViewModel required = new("merge-dp", "DP BIN", "Display payload");

        Assert.False(required.IsOptional);
        Assert.False(required.HasFile);
        AssertBrush("#FFF7F7", required.SlotBackgroundBrush);
        AssertBrush("#FCA5A5", required.SlotBorderBrush);
        Assert.Equal(new Thickness(1.5), required.SlotBorderThickness);
        AssertBrush("#B91C1C", required.RequirementBadgeForegroundBrush);

        required.FilePath = Path.Combine(Path.GetTempPath(), "dp.bin");

        Assert.True(required.HasFile);
        AssertBrush("#F0FDF4", required.SlotBackgroundBrush);
        AssertBrush("#86EFAC", required.SlotBorderBrush);
        AssertBrush("#15803D", required.RequirementBadgeForegroundBrush);

        FirmwareSlotViewModel optional = new("merge-ld", "LD BIN", "Optional payload", isOptional: true);

        Assert.True(optional.IsOptional);
        AssertBrush("#F8FAFC", optional.SlotBackgroundBrush);
        AssertBrush("#CBD5E1", optional.SlotBorderBrush);
        AssertBrush("#1D4ED8", optional.RequirementBadgeForegroundBrush);

        optional.FilePath = Path.Combine(Path.GetTempPath(), "ld.bin");

        Assert.True(optional.HasFile);
        AssertBrush("#F8FAFC", optional.SlotBackgroundBrush);
        AssertBrush("#CBD5E1", optional.SlotBorderBrush);
        AssertBrush("#1D4ED8", optional.RequirementBadgeForegroundBrush);
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

    /// <summary>Verifies NT51950 DP Replace writes a 0x100000 DP image and restores protected base ranges.</summary>
    [Fact]
    public async Task BuildNt51950DpReplaceRestoresBaseProtectedRanges()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-dp-replace-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            byte[] baseBytes = CreatePattern(0x100000, 0x80);
            byte[] replacementBytes = CreatePattern(0x40000, 0x20);
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
            Assert.Equal(baseBytes[0x37000], output[0x37000]);
            Assert.Equal(baseBytes[0x37FFF], output[0x37FFF]);
            Assert.Equal(replacementBytes[0x38000], output[0x38000]);
            Assert.Equal(0, output[0x50000]);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-tp", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-customer-info", StringComparison.Ordinal));
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
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51927");
        byte[] baseBytes = File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")));
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string regionPath = Path.Combine(tempRoot, "ctrlram.bin");
            File.WriteAllBytes(basePath, baseBytes);

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            const string selectedNumber = "2";
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = selectedNumber;
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);

            FirmwareSlotViewModel regionSlot = viewModel.ReplaceSlots.First(slot =>
                slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal));
            Assert.True(regionSlot.IsOptional);
            Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);
            CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(item => item.Name == regionSlot.Title);
            (int start, int length) = ParseCtrlRamRegion(region);
            File.WriteAllBytes(regionPath, baseBytes[start..(start + length)]);

            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile(regionSlot.SlotId, regionPath);

            Assert.True(viewModel.CanPreviewReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.HasLoadedReport);
            Assert.True(viewModel.LoadedReport.HasCommandOperations);
            Assert.True(viewModel.LoadedReport.HasStepOperations);
            Assert.Contains(viewModel.LoadedReport.SummaryRows, row =>
                row.Title == "Steps" &&
                row.Meta.Contains("command", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
                operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
                operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
                !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
                operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
                operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
                !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
                operation.CodeBlock.Contains("Combiner.exe ", StringComparison.Ordinal));
            Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.IsChanged);

            using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
            JsonElement postbuildOperation = reportDocument.RootElement
                .GetProperty("Operations")
                .EnumerateArray()
                .Single(operation => operation.GetProperty("OperationId").GetString()!.StartsWith(
                    "postbuild-",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                "InsertSID",
                postbuildOperation.GetProperty("Reason").GetString(),
                StringComparison.Ordinal);
            LegacyCombinerPostbuildCommandPlan expectedPlan = LegacyCombinerPostbuildPlanner.CreatePlan(
                LegacyCombinerPostbuildCatalog.Nt51927,
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [selectedNumber]));
            Assert.Equal(expectedPlan.Commands.Count, CountOccurrences(
                postbuildOperation.GetProperty("Reason").GetString()!,
                "Combiner.exe "));
            AssertJsonRange(postbuildOperation.GetProperty("TargetRange"), 0, 0x35000);
            AssertDoesNotCoverRange(postbuildOperation.GetProperty("ProcessorAllowedWriteRanges"), 0x7024, 4);
            AssertDoesNotCoverRange(postbuildOperation.GetProperty("ProcessorAllowedWriteRanges"), 0x1E254, 1);
            AssertDoesNotCoverRange(postbuildOperation.GetProperty("ProcessorAllowedWriteRanges"), 0x27254, 1);
            AssertCoversRange(postbuildOperation.GetProperty("ProcessorAllowedWriteRanges"), start, length);
            JsonElement assemblyOperation = reportDocument.RootElement
                .GetProperty("Operations")
                .EnumerateArray()
                .Single(operation => operation.GetProperty("OperationId").GetString() == "assemble-refreshed-tp");
            AssertJsonRange(assemblyOperation.GetProperty("SourceRange"), 0, 0x35000);
            AssertJsonRange(assemblyOperation.GetProperty("TargetRange"), 0, 0x35000);
            CtrlRamRegionViewModel unselectedRegion = viewModel.CtrlRamRegions.First(region =>
                region.Name != regionSlot.Title);
            (int unselectedStart, int unselectedLength) = ParseCtrlRamRegion(unselectedRegion);
            AssertDoesNotCoverRange(
                postbuildOperation.GetProperty("ProcessorAllowedWriteRanges"),
                unselectedStart,
                unselectedLength);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies unsupported CtrlRAM IC-count input returns a structured report instead of throwing.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewRejectsUnsupportedIcNumberWithReport()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-bad-number-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string replacementPath = Path.Combine(tempRoot, "nf.bin");
            File.WriteAllBytes(basePath, CreatePattern(0x40000, 0x50));
            File.WriteAllBytes(replacementPath, CreatePattern(0x0FD0, 0x70));
            Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
                ["replace-ctrlram-nf-master"] = replacementPath,
            };

            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                "NT51927",
                "4",
                "CtrlRAM",
                slotPaths,
                build: false,
                CancellationToken.None);

            Assert.False(result.Succeeded);
            using var reportDocument = JsonDocument.Parse(result.ReportJson);
            JsonElement issue = Assert.Single(reportDocument.RootElement.GetProperty("Issues").EnumerateArray());
            Assert.Equal("replace.ctrlram.ic-number-unsupported", issue.GetProperty("Code").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies one CtrlRAM Replace run can select and report multiple region replacements.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsMultipleSelectedRegions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51927");
        byte[] baseBytes = File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")));
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-multi-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string normalRightPath = Path.Combine(tempRoot, "normal-slave-r.bin");
            string vnLeftPath = Path.Combine(tempRoot, "vn-slave-l.bin");
            File.WriteAllBytes(basePath, baseBytes);

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
            File.WriteAllBytes(normalRightPath, baseBytes[normalRightStart..(normalRightStart + normalRightLength)]);
            File.WriteAllBytes(vnLeftPath, baseBytes[vnLeftStart..(vnLeftStart + vnLeftLength)]);
            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile(normalRight.SlotId, normalRightPath);
            viewModel.SetSlotFile(vnLeft.SlotId, vnLeftPath);

            Assert.Equal("2 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
            Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "Normal CtrlRAM (Slave R)");
            Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "VN CtrlRAM (Slave L)");
            Assert.True(viewModel.CanPreviewReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Title.Contains("replace-normal-slave-r", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Title.Contains("replace-vn-slave-l", StringComparison.Ordinal));
            Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
                segment.SourceLabel == "Normal CtrlRAM (Slave R)" &&
                segment.RangeLabel == "0x207D0-0x237CF (len 0x3000)");
            Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
                segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
                segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies CtrlRAM Replace can preview a golden-backed VN self replacement with traceable region naming.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsGoldenBackedVnSelfReplacement()
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
            string vnPath = Path.Combine(tempRoot, "vn-ctrlram.bin");
            byte[] baseBytes = File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")));
            File.WriteAllBytes(basePath, baseBytes);

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = "3";
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);

            FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
            Assert.Equal("Replace this area only when needed. TP position 0x2EBD0-0x3022F (len 0x1660).", vnLeft.Description);
            (int start, int length) = ParseCtrlRamRegion(
                viewModel.CtrlRamRegions.Single(region => region.Name == vnLeft.Title));
            File.WriteAllBytes(vnPath, baseBytes[start..(start + length)]);

            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile(vnLeft.SlotId, vnPath);

            Assert.True(viewModel.CanPreviewReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.Title.Contains("replace-vn-slave-l", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.HasCodeBlock &&
                operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
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

    /// <summary>Verifies a CtrlRAM replacement sliced from the same base runs through the real postbuild path.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewSelfReplacementRunsPostbuild()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51927");
        byte[] baseBytes = File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")));
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-self-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = "3";
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);

            FirmwareSlotViewModel vnLeftSlot = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
            CtrlRamRegionViewModel vnLeftRegion = viewModel.CtrlRamRegions.Single(region => region.Name == "VN CtrlRAM (Slave L)");
            (int start, int length) = ParseCtrlRamRegion(vnLeftRegion);
            string basePath = Path.Combine(tempRoot, "base-from-golden.bin");
            string replacementPath = Path.Combine(tempRoot, "self-vn-ctrlram.bin");
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(replacementPath, baseBytes[start..(start + length)]);

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
                operation.Title.Contains("replace-vn-slave-l", StringComparison.Ordinal));
            Assert.Contains(viewModel.LoadedReport.Operations, operation =>
                operation.HasCodeBlock &&
                operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies CtrlRAM Replace build commits a real postbuild output file.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildCommitsGoldenBackedSelfReplacementOutput()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51927");
        byte[] baseBytes = File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")));
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-build-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = "single";
            viewModel.ShowCtrlRamReplaceCommand.Execute(null);

            FirmwareSlotViewModel vnSlot = viewModel.ReplaceSlots.Single(slot =>
                slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
            CtrlRamRegionViewModel vnRegion = viewModel.CtrlRamRegions.Single(region => region.Name == vnSlot.Title);
            (int start, int length) = ParseCtrlRamRegion(vnRegion);
            string basePath = Path.Combine(tempRoot, "base-from-golden.bin");
            string replacementPath = Path.Combine(tempRoot, "self-vn-ctrlram.bin");
            string outputPath = Path.Combine(tempRoot, "ctrlram-build-output.bin");
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(replacementPath, baseBytes[start..(start + length)]);

            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile(vnSlot.SlotId, replacementPath);

            Assert.True(viewModel.CanBuildReplace);

            await viewModel.BuildReplaceAsync(outputPath);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.Equal(outputPath, viewModel.LastRunResult.Output);
            Assert.True(File.Exists(outputPath), outputPath);
            Assert.Equal(baseBytes.Length, new FileInfo(outputPath).Length);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
                operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));

            byte[] postbuildCleanBytes = File.ReadAllBytes(outputPath);
            string cleanBasePath = Path.Combine(tempRoot, "postbuild-clean-base.bin");
            string cleanReplacementPath = Path.Combine(tempRoot, "postbuild-clean-self-vn-ctrlram.bin");
            string cleanOutputPath = Path.Combine(tempRoot, "postbuild-clean-output.bin");
            File.WriteAllBytes(cleanBasePath, postbuildCleanBytes);
            File.WriteAllBytes(cleanReplacementPath, postbuildCleanBytes[start..(start + length)]);

            MainWindowViewModel cleanViewModel = ShellViewModelFactory.Create();
            cleanViewModel.SelectedIc = "NT51927";
            cleanViewModel.SelectedNumber = "single";
            cleanViewModel.ShowCtrlRamReplaceCommand.Execute(null);
            FirmwareSlotViewModel cleanVnSlot = cleanViewModel.ReplaceSlots.Single(slot => slot.Title == vnSlot.Title);
            cleanViewModel.SetSlotFile("replace-base", cleanBasePath);
            cleanViewModel.SetSlotFile(cleanVnSlot.SlotId, cleanReplacementPath);

            await cleanViewModel.BuildReplaceAsync(cleanOutputPath);

            Assert.True(cleanViewModel.LastRunResult.Succeeded, cleanViewModel.LastRunResult.Detail);
            Assert.Equal(postbuildCleanBytes, File.ReadAllBytes(cleanOutputPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies CtrlRAM self-replacement is byte-idempotent once the base is postbuild-canonical.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildIsIdempotentAfterPostbuildCanonicalOutput()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51927");
        byte[] baseBytes = File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput")));
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-ctrlram-idempotent-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string originalBasePath = Path.Combine(tempRoot, "base-from-golden.bin");
            string canonicalOutputPath = Path.Combine(tempRoot, "canonicalized.bin");
            string secondOutputPath = Path.Combine(tempRoot, "second-output.bin");
            File.WriteAllBytes(originalBasePath, baseBytes);

            await BuildNt51927VnSelfReplacementAsync(originalBasePath, canonicalOutputPath, tempRoot);
            await BuildNt51927VnSelfReplacementAsync(canonicalOutputPath, secondOutputPath, tempRoot);

            Assert.Equal(File.ReadAllBytes(canonicalOutputPath), File.ReadAllBytes(secondOutputPath));
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
        FirmwareSlotGroupViewModel slaveLGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Slave L");
        Assert.Equal("0/4", slaveLGroup.CountLabel);
        Assert.Equal("4 areas. None selected.", slaveLGroup.SelectionSummary);

        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        viewModel.SetSlotFile("replace-base", Path.Combine(Path.GetTempPath(), "base.bin"));
        viewModel.SetSlotFile(vnLeft.SlotId, Path.Combine(Path.GetTempPath(), "vn-slave-l.bin"));

        Assert.Equal("1 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Equal("1/4", slaveLGroup.CountLabel);
        Assert.Equal("1 selected / 4 areas.", slaveLGroup.SelectionSummary);
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
        Assert.Equal(4, viewModel.LoadedReport.SummaryRows.Count);
        Assert.Contains(viewModel.LoadedReport.SummaryRows, row =>
            row.Title == "Status" &&
            row.Detail == "Succeeded" &&
            row.Meta == "No issue");
        Assert.Equal(0, viewModel.LoadedReport.OperationCount);
        Assert.False(viewModel.LoadedReport.HasCommandOperations);
        Assert.False(viewModel.LoadedReport.HasStepOperations);

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

    /// <summary>Verifies unavailable Standard Merge profiles stay gated with a detailed report.</summary>
    [Fact]
    public async Task BuildUnavailableStandardMergeIsGatedWithDetailedReportAndNoOutput()
    {
        const string unsupportedIc = "NT00000";
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-unsupported-negative-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = unsupportedIc;

            Assert.False(viewModel.IsStandardMergeSupported);
            Assert.False(viewModel.CanBuildStandardMerge);
            Assert.Equal($"{unsupportedIc}: Standard Merge is not available yet.", viewModel.MergeReadinessStatus);

            string outputPath = Path.Combine(tempRoot, "should-not-exist.bin");
            await viewModel.BuildStandardMergeAsync(outputPath);

            Assert.False(viewModel.LastRunResult.Succeeded);
            Assert.Equal("Build failed", viewModel.LastRunResult.Title);
            Assert.Equal("No output", viewModel.LastRunResult.Output);
            Assert.Contains("Standard Merge is not available", viewModel.LastRunResult.Detail, StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath), outputPath);
            Assert.True(viewModel.HasLoadedReport);
            Assert.True(viewModel.CanOpenReport);
            Assert.True(viewModel.HasReportToast);
            ReportLineViewModel issue = Assert.Single(viewModel.LoadedReport.Issues);
            Assert.Equal("ui.run.failed", issue.Title);
            Assert.Contains("Standard Merge is not available", issue.Detail, StringComparison.Ordinal);
            Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
            Assert.Equal(issue.Title, viewModel.LoadedReport.PrimaryIssue.Title);
            Assert.False(viewModel.LoadedReport.HasInputs);
            Assert.False(viewModel.LoadedReport.HasOperations);
            Assert.Contains(viewModel.LoadedReport.SummaryRows, row =>
                row.Title == "Status" &&
                row.Detail == "1 issue(s)" &&
                row.Meta == issue.Title);
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

    private static (int Start, int Length) ParseCtrlRamRegion(CtrlRamRegionViewModel region)
    {
        string startHex = region.StartAddress.Split('-', StringSplitOptions.TrimEntries)[0][2..];
        string lengthHex = region.SizeHex["len 0x".Length..];
        return (
            int.Parse(startHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(lengthHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static async Task BuildNt51927VnSelfReplacementAsync(
        string basePath,
        string outputPath,
        string tempRoot)
    {
        byte[] baseBytes = File.ReadAllBytes(basePath);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "single";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel vnSlot = viewModel.ReplaceSlots.Single(slot =>
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        CtrlRamRegionViewModel vnRegion = viewModel.CtrlRamRegions.Single(region => region.Name == vnSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(vnRegion);
        string replacementPath = Path.Combine(tempRoot, $"self-vn-ctrlram-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(replacementPath, baseBytes[start..(start + length)]);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(vnSlot.SlotId, replacementPath);

        Assert.True(viewModel.CanBuildReplace);

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.LastRunResult.Output);
        Assert.True(File.Exists(outputPath), outputPath);
    }

    private static void AssertCoversRange(JsonElement ranges, int start, int length)
    {
        RangeSet rangeSet = new(ranges.EnumerateArray().Select(range => new ByteRange(
            range.GetProperty("Start").GetInt64(),
            range.GetProperty("Length").GetInt64())));

        Assert.True(
            rangeSet.Contains(new ByteRange(start, length)),
            FormattableString.Invariant($"Expected allowed write ranges to cover [0x{start:X}, 0x{start + length:X})."));
    }

    private static void AssertDoesNotCoverRange(JsonElement ranges, int start, int length)
    {
        Assert.DoesNotContain(ranges.EnumerateArray(), range =>
        {
            long rangeStart = range.GetProperty("Start").GetInt64();
            long rangeEnd = rangeStart + range.GetProperty("Length").GetInt64();
            return rangeStart <= start && rangeEnd >= start + length;
        });
    }

    private static void AssertJsonRange(JsonElement range, int start, int length)
    {
        Assert.Equal(start, range.GetProperty("Start").GetInt64());
        Assert.Equal(length, range.GetProperty("Length").GetInt64());
    }

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static void AssertBrush(string expectedHex, IBrush brush)
    {
        ISolidColorBrush solid = Assert.IsType<ISolidColorBrush>(brush, exactMatch: false);
        Assert.Equal(Color.Parse(expectedHex), solid.Color);
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
