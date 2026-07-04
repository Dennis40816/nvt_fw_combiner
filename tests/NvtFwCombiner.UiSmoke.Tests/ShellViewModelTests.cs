using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia;
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
            row.Status == "Session");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "Strictness" &&
            row.Value == "Warn only");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "Language" &&
            row.Value == "Traditional Chinese");
    }

    /// <summary>Verifies breadcrumbs show page hierarchy while Back returns to the previous page.</summary>
    [Fact]
    public void NavigationTrailShowsHierarchyAndBackReturnsToPreviousPage()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Replace", viewModel.NavigationPath);
        Assert.DoesNotContain("Merge > Replace", viewModel.NavigationPath, StringComparison.Ordinal);
        Assert.False(viewModel.NavigationTrail[^1].IsChevronVisible);

        viewModel.GoBackCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Merge", viewModel.NavigationPath);
    }

    /// <summary>Verifies command-line launch arguments select a reviewable UI state.</summary>
    [Fact]
    public void UiLaunchOptionsParsePageReportAndOpenReport()
    {
        var options = UiLaunchOptions.Parse(
            ["--page", "merge", "--load-report", "preview-report.json", "--open-report"]);

        Assert.Equal(ShellPage.Merge, options.Page);
        Assert.Equal("preview-report.json", options.ReportPath);
        Assert.True(options.OpenReport);
        Assert.Empty(options.Issues);
    }

    /// <summary>Verifies invalid UI launch arguments fail as reportable issues.</summary>
    [Fact]
    public void UiLaunchOptionsCollectsInvalidArguments()
    {
        var options = UiLaunchOptions.Parse(
            ["--page=unknown", "--report=", "--open-report"]);

        Assert.Null(options.Page);
        Assert.Null(options.ReportPath);
        Assert.True(options.OpenReport);
        Assert.Contains(options.Issues, issue => issue.Contains("Unsupported --page value", StringComparison.Ordinal));
        Assert.Contains(options.Issues, issue => issue.Contains("--report requires a value.", StringComparison.Ordinal));
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
        Assert.Equal(["Normal", "AB Code", "General"], viewModel.MergeModeChoices);
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
        Assert.Equal(FirmwareSlotKind.Dp, required.SlotKind);
        Assert.Equal("DP BIN", required.SlotIconTooltip);
        AssertIconGeometry(required);
        AssertBrush("#EFF6FF", required.SlotIconBackgroundBrush);
        AssertBrush("#BFDBFE", required.SlotIconBorderBrush);
        AssertBrush("#1D4ED8", required.SlotIconForegroundBrush);
        Assert.Equal("No BIN selected", required.DisplayName);
        Assert.Equal(string.Empty, required.DisplayDetail);
        AssertBrush("#FFF7F7", required.SlotBackgroundBrush);
        AssertBrush("#FCA5A5", required.SlotBorderBrush);
        Assert.Equal(new Thickness(1.5), required.SlotBorderThickness);
        AssertBrush("#B91C1C", required.RequirementBadgeForegroundBrush);

        required.FilePath = Path.Combine(Path.GetTempPath(), "dp.bin");

        Assert.True(required.HasFile);
        Assert.Equal("dp.bin", required.DisplayName);
        Assert.Equal(required.FilePath, required.DisplayDetail);
        AssertIconGeometry(required);
        AssertBrush("#EFF6FF", required.SlotIconBackgroundBrush);
        AssertBrush("#F0FDF4", required.SlotBackgroundBrush);
        AssertBrush("#86EFAC", required.SlotBorderBrush);
        Assert.Equal(new Thickness(1), required.SlotBorderThickness);
        AssertBrush("#15803D", required.RequirementBadgeForegroundBrush);

        FirmwareSlotViewModel optional = new("merge-ld", "LD BIN", "Optional payload", isOptional: true);

        Assert.True(optional.IsOptional);
        Assert.Equal(FirmwareSlotKind.Dp, optional.SlotKind);
        AssertIconGeometry(optional);
        AssertBrush("#F8FAFC", optional.SlotBackgroundBrush);
        AssertBrush("#CBD5E1", optional.SlotBorderBrush);
        Assert.Equal(new Thickness(1), optional.SlotBorderThickness);
        AssertBrush("#1D4ED8", optional.RequirementBadgeForegroundBrush);

        optional.FilePath = Path.Combine(Path.GetTempPath(), "ld.bin");

        Assert.True(optional.HasFile);
        AssertBrush("#F8FAFC", optional.SlotBackgroundBrush);
        AssertBrush("#CBD5E1", optional.SlotBorderBrush);
        AssertBrush("#1D4ED8", optional.RequirementBadgeForegroundBrush);
    }

    /// <summary>Verifies slot type icons distinguish DP, TP, CtrlRAM and base BIN inputs.</summary>
    [Fact]
    public void FirmwareSlotTypeIconsExposeInputCategories()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Contains(viewModel.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.SlotKind == FirmwareSlotKind.Dp &&
            HasDrawableIcon(slot));
        Assert.Contains(viewModel.MergeSlots, slot =>
            slot.Title == "TP BIN" &&
            slot.SlotKind == FirmwareSlotKind.Tp &&
            HasDrawableIcon(slot));
        Assert.Equal(FirmwareSlotKind.Base, viewModel.ReplaceBaseSlot.SlotKind);
        AssertIconGeometry(viewModel.ReplaceBaseSlot);
        Assert.Equal("Base firmware BIN", viewModel.ReplaceBaseSlot.SlotIconTooltip);

        viewModel.ShowDpReplaceCommand.Execute(null);

        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-dp" &&
            slot.SlotKind == FirmwareSlotKind.Dp &&
            HasDrawableIcon(slot));

        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.All(
            viewModel.ReplaceSlots.Where(slot => !ReferenceEquals(slot, viewModel.ReplaceBaseSlot)),
            slot =>
            {
                Assert.Equal(FirmwareSlotKind.CtrlRam, slot.SlotKind);
                Assert.Equal("CtrlRAM BIN", slot.SlotIconTooltip);
                AssertIconGeometry(slot);
                AssertBrush("#F5F3FF", slot.SlotIconBackgroundBrush);
                AssertBrush("#DDD6FE", slot.SlotIconBorderBrush);
                AssertBrush("#6D28D9", slot.SlotIconForegroundBrush);
            });
    }

    /// <summary>Verifies base BIN slots expose FWConfig facts decoded from the selected flash image.</summary>
    [Fact]
    public void BaseFirmwareSlotShowsFwConfigFacts()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        string basePath = Path.Combine(
            FindRepositoryRoot(),
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "expected",
            "51926",
            "flash.bin");

        viewModel.SetSlotFile("replace-base", basePath);

        Assert.True(viewModel.ReplaceBaseSlot.HasFirmwareFacts);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW" && fact.Value == "1.4.1");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "FW" &&
            fact.Value.Contains("0x01.0x00", StringComparison.Ordinal) &&
            fact.Value.Contains("bar OK", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "PID" && fact.Value == "0x5102");
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
        Assert.Contains("explicit profile-approved", viewModel.SelectedReplaceModeDescription, StringComparison.Ordinal);
        Assert.False(viewModel.CanPreviewReplace);
        Assert.Equal(
            "Preview blocked: workbench General Replace execution wiring is pending; compiled mappings must still pass profile bounds and protected-range checks.",
            viewModel.ReplaceReadinessStatus);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);

        viewModel.AddGeneralReplaceMappingCommand.Execute(null);
        Assert.Equal(2, viewModel.GeneralReplaceMappings.Count);

        viewModel.RemoveGeneralReplaceMappingRow(viewModel.GeneralReplaceMappings[0]);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);
        Assert.Equal(1, viewModel.GeneralReplaceMappings[0].Index);
        Assert.Equal("No replacement BIN selected", viewModel.GeneralReplaceMappings[0].DisplayName);
        Assert.Equal(string.Empty, viewModel.GeneralReplaceMappings[0].DisplayDetail);
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

    /// <summary>Verifies Merge coverage rows expose final ownership without report-level operation text.</summary>
    [Fact]
    public void MergeCoverageUsesCompactFinalOwnershipText()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";

        MemoryMapRowViewModel initialRow = Assert.Single(
            viewModel.MergeMemoryRows,
            row => row.ActionLabel == "Initialize");
        Assert.Equal("DP BIN length (max end 0xFFFFF)", initialRow.RangeLabel);
        Assert.Contains("Max inclusive end is 0xFFFFF", initialRow.Detail, StringComparison.Ordinal);
        Assert.Equal("No output -> Reserved", initialRow.FlowLabel);
        Assert.Contains(viewModel.MergeCoverageSegments, segment =>
            segment.SourceLabel == "TP BIN" &&
            segment.CompactDetail == "Final bytes come from TP BIN.");
        Assert.Contains(viewModel.MergeCoverageSegments, segment =>
            segment.SourceLabel == "DP BIN" &&
            segment.CompactDetail == "Final bytes come from DP BIN.");
        Assert.All(viewModel.MergeCoverageSegments, segment =>
        {
            Assert.NotEqual("Preserved", segment.ChangeLabel);
            Assert.DoesNotContain("CopyRange", segment.CompactDetail, StringComparison.Ordinal);
            Assert.DoesNotContain("Copies source", segment.CompactDetail, StringComparison.Ordinal);
        });
    }

    /// <summary>Verifies NT51950/NT51951 Initial display follows the selected DP BIN length.</summary>
    [Fact]
    public void Nt51950InitialRowUsesSelectedDpInputLength()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-initial-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string dpPath = Path.Combine(tempRoot, "dp-40000.bin");
            File.WriteAllBytes(dpPath, new byte[0x40000]);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();

            viewModel.SelectedIc = "NT51950";
            viewModel.SetSlotFile("merge-dp", dpPath);

            MemoryMapRowViewModel initialRow = Assert.Single(
                viewModel.MergeMemoryRows,
                row => row.ActionLabel == "Initialize");
            Assert.Equal("0x00000-0x3FFFF (len 0x40000)", initialRow.RangeLabel);
            Assert.Contains("selected DP BIN length", initialRow.Detail, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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

    /// <summary>Verifies NT51950 DP Replace output follows the selected base length and restores TP bytes from base.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public async Task BuildNt51950DpReplaceUsesSelectedBaseLengthAndRestoresBaseTpRange(int baseLength)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-dp-replace-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            byte[] baseBytes = CreatePattern(baseLength, 0x80);
            int replacementLength = baseLength - 0x1000;
            byte[] replacementBytes = CreatePattern(replacementLength, 0x20);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement-dp.bin");
            string outputPath = Path.Combine(tempRoot, $"nt51950-dp-replace-{baseLength:X}.bin");
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(replacementPath, replacementBytes);

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51950";
            viewModel.ShowDpReplaceCommand.Execute(null);
            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile("replace-dp", replacementPath);

            Assert.True(viewModel.CanPreviewReplace);
            Assert.False(viewModel.BuildReplaceCommand.CanExecute(null));
            Assert.False(viewModel.CanBuildReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.CanBuildReplace);

            await viewModel.BuildReplaceAsync(outputPath);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(File.Exists(outputPath), outputPath);
            byte[] output = File.ReadAllBytes(outputPath);
            Assert.Equal(baseLength, output.Length);
            Assert.Equal(replacementBytes[0x9FFF], output[0x9FFF]);
            Assert.Equal(baseBytes[0x0A000], output[0x0A000]);
            Assert.Equal(baseBytes[0x36FFF], output[0x36FFF]);
            Assert.Equal(replacementBytes[0x37000], output[0x37000]);
            Assert.Equal(0, output[replacementLength]);
            Assert.True(viewModel.HasLoadedReport);
            Assert.Contains(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-tp", StringComparison.Ordinal));

            using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
            Assert.Equal(baseLength, reportDocument.RootElement.GetProperty("Output").GetProperty("Size").GetInt64());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies golden-backed NT51950/NT51951 DP Replace accepts the real 0x40000 and 0x80000 base lengths.</summary>
    [Theory]
    [InlineData(
        "NT51950",
        "expected/51950/dp-256k/flash.bin",
        "inputs/51950/dp-256k/dp.bin",
        0x40000)]
    [InlineData(
        "NT51951",
        "expected/51951/dp-512k/flash.bin",
        "inputs/51951/dp-512k/dp.bin",
        0x80000)]
    public async Task PreviewDpReplaceAcceptsGoldenBackedBaseLengths(
        string icId,
        string baseRelativePath,
        string dpRelativePath,
        int expectedLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dpRelativePath);

        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string basePath = Path.Combine(goldenRoot, baseRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string dpPath = Path.Combine(goldenRoot, dpRelativePath.Replace('/', Path.DirectorySeparatorChar));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = icId;
        viewModel.ShowDpReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", dpPath);

        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace" &&
            row.RangeLabel == $"0x00000-0x{expectedLength - 1:X5} (len 0x{expectedLength:X})");

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        JsonElement root = reportDocument.RootElement;
        Assert.Equal(expectedLength, root.GetProperty("Output").GetProperty("Size").GetInt64());
        Assert.Equal(0, root.GetProperty("Issues").GetArrayLength());
        Assert.Contains(root.GetProperty("Operations").EnumerateArray(), operation =>
            operation.GetProperty("OperationId").GetString() == "replace-dp-container" &&
            operation.GetProperty("TargetRange").GetProperty("Length").GetInt64() == expectedLength);
    }

    /// <summary>Verifies Replace Build requires a successful Preview for the exact current file set.</summary>
    [Fact]
    public async Task BuildReplaceRequiresFreshPreviewAfterInputChange()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-replace-gate-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement-dp.bin");
            string replacementPath2 = Path.Combine(tempRoot, "replacement-dp-copy.bin");
            string outputPath = Path.Combine(tempRoot, "blocked-output.bin");
            File.WriteAllBytes(basePath, CreatePattern(0x40000, 0x90));
            File.WriteAllBytes(replacementPath, CreatePattern(0x3F000, 0x30));
            File.Copy(replacementPath, replacementPath2);

            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51950";
            viewModel.ShowDpReplaceCommand.Execute(null);
            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile("replace-dp", replacementPath);

            Assert.True(viewModel.CanPreviewReplace);
            Assert.False(viewModel.CanBuildReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.CanBuildReplace);

            viewModel.SetSlotFile("replace-dp", replacementPath2);

            Assert.True(viewModel.CanPreviewReplace);
            Assert.False(viewModel.CanBuildReplace);
            Assert.False(viewModel.BuildReplaceCommand.CanExecute(null));

            await viewModel.BuildReplaceAsync(outputPath);

            Assert.False(viewModel.LastRunResult.Succeeded);
            Assert.Equal("Build blocked", viewModel.LastRunResult.Title);
            Assert.False(File.Exists(outputPath), outputPath);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.CanBuildReplace);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies NT51950 DP Replace rejects unapproved base lengths instead of assuming 0x100000.</summary>
    [Fact]
    public async Task PreviewNt51950DpReplaceRejectsUnsupportedBaseLength()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-dp-replace-invalid-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            string basePath = Path.Combine(tempRoot, "base-60000.bin");
            string replacementPath = Path.Combine(tempRoot, "replacement-dp.bin");
            File.WriteAllBytes(basePath, new byte[0x60000]);
            File.WriteAllBytes(replacementPath, new byte[0x40000]);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();

            viewModel.SelectedIc = "NT51950";
            viewModel.ShowDpReplaceCommand.Execute(null);
            viewModel.SetSlotFile("replace-base", basePath);
            viewModel.SetSlotFile("replace-dp", replacementPath);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.False(viewModel.LastRunResult.Succeeded);
            Assert.Contains("0x40000 / 0x80000 / 0x100000", viewModel.LastRunResult.Detail, StringComparison.Ordinal);
            Assert.Contains(viewModel.ReplaceMemoryRows, row =>
                row.ActionLabel == "Replace" &&
                row.RangeLabel == "Unsupported base BIN length 0x60000");
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
            viewModel.SelectedIc = "NT51927";
            viewModel.SelectedNumber = "2";
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

            Assert.True(viewModel.CanPreviewReplace);
            Assert.False(viewModel.CanBuildReplace);

            await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
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

            await cleanViewModel.PreviewReplaceCommand.ExecuteAsync(null);
            Assert.True(cleanViewModel.CanBuildReplace);

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

    /// <summary>Verifies owner-approved CtrlRAM Replace fixtures when a manifest is present.</summary>
    [Fact]
    public async Task CtrlRamReplaceFixtureCasesBuildWhenManifestPresent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = FindRepositoryRoot();
        string fixtureRoot = Path.Combine(repositoryRoot, "testdata", "golden", "ctrlram-replace");
        string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        bool enforceExpectedOutput = manifestDocument.RootElement.TryGetProperty("runnerStatus", out JsonElement runnerStatus) &&
            runnerStatus.GetString() == "ready-for-private-golden";
        foreach (JsonElement fixtureCase in manifestDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            Assert.Equal("CtrlRAM", fixtureCase.GetProperty("mode").GetString());
            string caseId = fixtureCase.GetProperty("id").GetString()!;
            string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-private-ctrlram-{Guid.NewGuid():N}");
            try
            {
                _ = Directory.CreateDirectory(tempRoot);
                MainWindowViewModel viewModel = ShellViewModelFactory.Create();
                viewModel.SelectedIc = fixtureCase.GetProperty("ic").GetString()!;
                viewModel.SelectedNumber = fixtureCase.GetProperty("icNum").GetString()!;
                viewModel.ShowCtrlRamReplaceCommand.Execute(null);
                viewModel.SetSlotFile(
                    "replace-base",
                    GoldenFixturePath(fixtureRoot, fixtureCase.GetProperty("base")));

                foreach (JsonElement replacement in fixtureCase.GetProperty("replacementInputs").EnumerateArray())
                {
                    string slotId = replacement.GetProperty("slotId").GetString()!;
                    Assert.Contains(viewModel.ReplaceSlots, slot => slot.SlotId == slotId);
                    viewModel.SetSlotFile(slotId, GoldenFixturePath(fixtureRoot, replacement.GetProperty("file")));
                }

                string outputPath = Path.Combine(tempRoot, $"{caseId}.bin");
                Assert.True(viewModel.CanPreviewReplace, caseId);

                await viewModel.PreviewReplaceCommand.ExecuteAsync(null);
                Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
                Assert.True(viewModel.CanBuildReplace, caseId);

                await viewModel.BuildReplaceAsync(outputPath);
                Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
                Assert.True(File.Exists(outputPath), outputPath);

                if (enforceExpectedOutput)
                {
                    Assert.True(fixtureCase.TryGetProperty("expectedOutput", out JsonElement expectedOutput), caseId);
                    Assert.Equal(
                        File.ReadAllBytes(GoldenFixturePath(fixtureRoot, expectedOutput)),
                        File.ReadAllBytes(outputPath));
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

    /// <summary>Verifies reports stay behind an explicit action until opened.</summary>
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
        Assert.Equal("No report", viewModel.ReportActionLabel);
        Assert.Equal("Preview or Build creates one", viewModel.ReportActionStatus);

        viewModel.LoadReportJson(json, "preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.Equal("Open report", viewModel.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.ReportActionStatus);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal(1, viewModel.ReportToastOpacity);
        Assert.Equal(json, viewModel.LoadedReportJson);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927).json", viewModel.ReportSaveFileName);
        Assert.True(viewModel.ShowReportCommand.CanExecute(null));
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Succeeded", viewModel.LoadedReport.OutcomeTitle);
        Assert.Contains("No reported issues", viewModel.LoadedReport.OutcomeDetail, StringComparison.Ordinal);
        Assert.Equal("Ready for audit", viewModel.LoadedReport.NextStepTitle);
        Assert.Contains(viewModel.LoadedReport.TriageRows, row =>
            row.Title == "1. Result" &&
            row.Detail == "Succeeded" &&
            row.Meta == "No issue");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "0" &&
            row.Meta == "No blocking issue");
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

    /// <summary>Verifies report loading errors still produce a reopenable report modal.</summary>
    [Fact]
    public void ReportReviewErrorsUseModalState()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportError("Startup report", "missing preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.Equal(string.Empty, viewModel.LoadedReportJson);
        Assert.Equal("Load failed", viewModel.ReportActionStatus);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal("Report issue: Startup report", viewModel.ReportToastText);
        Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Report load failed", viewModel.LoadedReport.OutcomeTitle);
        Assert.Equal("Start with this issue", viewModel.LoadedReport.NextStepTitle);
        Assert.Equal("Load error", viewModel.LoadedReport.PrimaryIssue.Title);
        Assert.Contains("missing preview-report.json", viewModel.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "1" &&
            row.Meta == "Load error");

        viewModel.ShowReportCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.HasReportToast);
    }

    /// <summary>Verifies report triage points users to the first issue and command evidence.</summary>
    [Fact]
    public void ReportReviewTriagePrioritizesIssueAndCommandEvidence()
    {
        const string json = /*lang=json,strict*/ """
            {
              "ProfileId": "nt51927-ctrlram-replace",
              "IcId": "NT51927",
              "ExperienceId": "ctrlram-replace",
              "CompositionKind": "Replace",
              "RunId": "ui-smoke-command",
              "StartedAtUtc": "2026-07-01T00:00:00Z",
              "Inputs": [
                {
                  "AddressSpaceId": "base-input",
                  "BindingId": "base",
                  "Size": 524288,
                  "Sha256": "abcdef0123456789",
                  "ArtifactId": "base.bin"
                }
              ],
              "Operations": [
                {
                  "Sequence": 900,
                  "OperationId": "run-ctrlram-postbuild",
                  "Kind": "run-external-processor",
                  "Status": "planned",
                  "OverlapPolicy": "reject",
                  "TargetSpaceId": "output-image",
                  "TargetRange": {
                    "Start": 0,
                    "EndExclusive": 524288,
                    "Length": 524288
                  },
                  "ProcessorId": "legacy-combiner",
                  "Reason": "Run approved staged Combiner command: Combiner.exe /bin work.bin /mmap mmap.h."
                }
              ],
              "Mutations": [],
              "Issues": [
                {
                  "Code": "processor.tool.missing",
                  "Message": "Combiner executable is not available.",
                  "OperationId": "run-ctrlram-postbuild"
                }
              ],
              "Output": {
                "FileName": "No output",
                "Size": 0,
                "Committed": false,
                "Sha256": ""
              }
            }
            """;

        var report = ReportReviewViewModel.FromJson(json, "preview-report.json");

        Assert.Equal("Needs attention", report.OutcomeTitle);
        Assert.Equal("Start with this issue", report.NextStepTitle);
        Assert.Equal("processor.tool.missing", report.PrimaryIssue.Title);
        Assert.Contains(report.TriageRows, row =>
            row.Title == "1. First issue" &&
            row.Detail == "processor.tool.missing" &&
            row.Meta == "run-ctrlram-postbuild");
        Assert.Contains(report.TriageRows, row =>
            row.Title == "3. Evidence" &&
            row.Detail == "Combiner commands" &&
            row.Meta == "1 external command(s)");
        Assert.Contains(report.EvidenceRows, row =>
            row.Title == "Commands" &&
            row.Detail == "1" &&
            row.Meta == "external processors");
        Assert.True(report.ShouldExpandIssues);
        Assert.True(report.ShouldExpandCommandOperations);
        Assert.False(report.ShouldExpandStepOperations);
        ReportLineViewModel command = Assert.Single(report.CommandOperations);
        Assert.Contains("Combiner.exe", command.CodeBlock, StringComparison.Ordinal);
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
        Assert.Equal("Reserved -> TP BIN", copyRow.FlowLabel);
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

            Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
            Assert.False(viewModel.BuildMergeCommand.CanExecute(null));
            Assert.False(viewModel.CanBuildStandardMerge);

            await viewModel.PreviewMergeCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
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

    /// <summary>Verifies Standard Merge Build requires a successful Preview for the exact current context.</summary>
    [Fact]
    public async Task BuildStandardMergeRequiresFreshPreviewAfterInputChange()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == "51926");
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-merge-gate-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            MainWindowViewModel viewModel = ShellViewModelFactory.Create();
            viewModel.SelectedIc = "NT51926";

            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                string sourcePath = ManifestPath(goldenRoot, input.Value);
                string copiedPath = Path.Combine(tempRoot, $"{input.Name}.bin");
                File.Copy(sourcePath, copiedPath);
                viewModel.SetSlotFile(SlotIdForAddressSpace(input.Name), copiedPath);
            }

            Assert.True(viewModel.CanPreviewStandardMerge);
            Assert.False(viewModel.CanBuildStandardMerge);

            await viewModel.PreviewMergeCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.CanBuildStandardMerge);

            JsonProperty firstInput = goldenCase.GetProperty("inputs").EnumerateObject().First();
            string replacementCopyPath = Path.Combine(tempRoot, $"{firstInput.Name}-copy.bin");
            File.Copy(ManifestPath(goldenRoot, firstInput.Value), replacementCopyPath);
            viewModel.SetSlotFile(SlotIdForAddressSpace(firstInput.Name), replacementCopyPath);

            Assert.True(viewModel.CanPreviewStandardMerge);
            Assert.False(viewModel.CanBuildStandardMerge);

            await viewModel.PreviewMergeCommand.ExecuteAsync(null);

            Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
            Assert.True(viewModel.CanBuildStandardMerge);

            viewModel.SelectedIc = "NT51927";

            Assert.True(viewModel.CanPreviewStandardMerge);
            Assert.False(viewModel.CanBuildStandardMerge);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies an NT51950 preview with NT51926 TP input is blocked with a reopenable detailed report.</summary>
    [Fact]
    public async Task PreviewNt51950WithNt51926InputsFailsWithDetailedReportAndNoOutput()
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

            Assert.True(viewModel.CanPreviewStandardMerge);
            Assert.False(viewModel.CanBuildStandardMerge);

            string outputPath = Path.Combine(tempRoot, "should-not-exist.bin");
            await viewModel.PreviewMergeCommand.ExecuteAsync(null);

            Assert.False(viewModel.LastRunResult.Succeeded);
            Assert.Equal("Preview blocked", viewModel.LastRunResult.Title);
            Assert.Equal("No output", viewModel.LastRunResult.Output);
            Assert.False(File.Exists(outputPath), outputPath);
            Assert.False(viewModel.CanBuildStandardMerge);
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

    private static string GoldenFixturePath(string fixtureRoot, JsonElement manifestFile)
    {
        string relativePath = manifestFile.GetProperty("path").GetString()!;
        string candidate = Path.GetFullPath(Path.Combine(
            fixtureRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string fullFixtureRoot = Path.GetFullPath(fixtureRoot);
        string relativeToRoot = Path.GetRelativePath(fullFixtureRoot, candidate);
        return !relativeToRoot.StartsWith("..", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relativeToRoot)
            ? candidate
            : throw new InvalidOperationException($"Private fixture path escapes root: {relativePath}");
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

    private static void AssertBrush(string expectedHex, IBrush brush)
    {
        ISolidColorBrush solid = Assert.IsType<ISolidColorBrush>(brush, exactMatch: false);
        Assert.Equal(Color.Parse(expectedHex), solid.Color);
    }

    private static void AssertIconGeometry(FirmwareSlotViewModel slot)
    {
        Assert.True(HasDrawableIcon(slot));
    }

    private static bool HasDrawableIcon(FirmwareSlotViewModel slot)
    {
        return slot.SlotIconPathData.StartsWith('M') &&
            slot.SlotIconPathData.Contains('L');
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
