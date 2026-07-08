using System.Globalization;
using System.Text.Json;
using Avalonia;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Smoke coverage for shell view-model surfaces used by the Avalonia UI.</summary>
public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies Settings exposes catalog-backed status without requiring workflow context.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        string expectedVersion = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("VERSION")).Trim();
        Assert.Equal(expectedVersion, viewModel.AppVersion);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "Built-in profiles" && row.Value.Contains("merge", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsToolRows, row => row.Title == "CRC/header refresh" && row.Value == "Configured");
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row => row.Title == "Report review");
        Assert.Contains(viewModel.SettingsReadinessRows, row => row.Title == "Device context" && row.Value == "Workflow pages only");

        viewModel.SelectedTheme = "Dark";
        viewModel.SelectedStrictness = "Warn only";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("設定", viewModel.SettingsPreview.Title);
        Assert.Equal("建立", viewModel.Text.BuildActionLabel);
        Assert.Equal("首頁 > 設定", viewModel.NavigationPath);
        Assert.Equal("目前視窗已套用暗色主題。", viewModel.ThemePreferenceStatus);
        Assert.Equal("只調整 UI review 語氣；韌體 gate 仍維持 fail-closed。", viewModel.StrictnessPreferenceStatus);
        Assert.Equal("繁體中文介面已套用並會在啟動時還原。", viewModel.LanguagePreferenceStatus);
        Assert.Contains(viewModel.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.RequirementLabel == "必填" &&
            slot.DisplayName == "尚未選擇 BIN");
        Assert.Equal("必填", viewModel.ReplaceBaseSlot.RequirementLabel);
        Assert.Equal("尚未選擇 BIN", viewModel.ReplaceBaseSlot.DisplayName);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "內建 profiles" && row.Status == "已串接");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "主題" &&
            row.Value == "Dark" &&
            row.Status == "已儲存");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "審查嚴格度" &&
            row.Value == "Warn only" &&
            row.Status == "已儲存");
        Assert.Contains(viewModel.SettingsPreferenceRows, row =>
            row.Title == "語言" &&
            row.Value == "Traditional Chinese" &&
            row.Status == "已套用");
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row =>
            row.Title == "Report history 儲存" &&
            row.Status == "已啟用");
        Assert.Contains(viewModel.SettingsReadinessRows, row =>
            row.Title == "偏好設定" &&
            row.Value == "本機儲存");
        Assert.DoesNotContain(viewModel.SettingsPreferenceRows, row =>
            row.Status.Contains("Pending", StringComparison.OrdinalIgnoreCase));
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

    /// <summary>Verifies General Merge uses its own mapping editor state and hides IC Number context.</summary>
    [Fact]
    public void GeneralMergeUsesEditableMappingsAndOwnOutputLength()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SelectedMergeMode = "General";

        Assert.True(viewModel.IsGeneralMergeModeSelected);
        Assert.False(viewModel.IsNormalMergeModeSelected);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950: refresh profile, slots, validation", viewModel.DeviceContextStatus);
        Assert.Equal("0x100000", viewModel.GeneralMergeOutputLength);
        Assert.Equal(
            $"NT51950_FlashCode_DxxxxTxxxx_{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.bin",
            viewModel.MergeOutputFileName);
        _ = Assert.Single(viewModel.GeneralMergeMappings);

        viewModel.AddGeneralMergeMappingCommand.Execute(null);

        Assert.Equal(2, viewModel.GeneralMergeMappings.Count);
        viewModel.RemoveGeneralMergeMappingRow(viewModel.GeneralMergeMappings[0]);
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.GeneralMergeMappings);
        Assert.Equal(1, mapping.Index);
        Assert.Equal("No source BIN selected", mapping.DisplayName);
        Assert.Contains("reserved", viewModel.MergeMemorySummary, StringComparison.OrdinalIgnoreCase);
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
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-slot-tone");
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
        AssertBrush("#FEF2F2", required.SlotBackgroundBrush);
        AssertBrush("#FCA5A5", required.SlotBorderBrush);
        Assert.Equal(new Thickness(1.5), required.SlotBorderThickness);
        AssertBrush("#B91C1C", required.RequirementBadgeForegroundBrush);

        required.FilePath = workspace.PathFor("dp.bin");

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

        optional.FilePath = workspace.PathFor("ld.bin");

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
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        viewModel.SetSlotFile("replace-base", basePath);

        Assert.True(viewModel.ReplaceBaseSlot.HasFirmwareFacts);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW" && fact.Value == "1.4.1");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "FW" &&
            fact.Value == "0x01.0x00");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "PID" && fact.Value == "0x5102");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Refresh" && fact.Value == "51926_1.4.1");
    }

    /// <summary>Verifies DP BIN slots expose gen_flash DP version facts and mark missing evidence.</summary>
    [Fact]
    public void DpFirmwareSlotShowsGenFlashVersionOrTodo()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51926 = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("tp-input"));

        viewModel.SetSlotFile("merge-dp", dpPath);
        viewModel.SetSlotFile("merge-tp", tpPath);

        FirmwareSlotViewModel dpSlot = viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP" &&
            fact.Value == "D0102" &&
            !fact.IsWarning);
        Assert.StartsWith(
            "NT51926_FlashCode_D0102T0100_",
            viewModel.MergeOutputFileName,
            StringComparison.Ordinal);

        viewModel.SelectedIc = "NT51950";
        JsonElement nt51950 = golden.CaseByIc("51950");
        string nt51950DpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("dp-input"));
        viewModel.SetSlotFile("merge-dp", nt51950DpPath);

        dpSlot = viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP" &&
            fact.Value == "D????" &&
            fact.IsWarning);
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
            "Build blocked: base BIN and at least one explicit replacement mapping are required.",
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

    /// <summary>Verifies General Replace UI runs a DP explicit mapping through Preview and Build.</summary>
    [Fact]
    public async Task GeneralReplacePreviewAndBuildUseExplicitMappingRows()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace");
        byte[] baseBytes = CreatePattern(0x40000, 0x40);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("general-replace.bin");

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowGeneralReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x00100";
        mapping.EndAddress = "0x00101";
        viewModel.SetGeneralReplaceMappingFile(mapping.MappingId, replacementPath);

        Assert.True(viewModel.CanPreviewReplace);
        Assert.Contains("Ready", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildReplace);

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(0xA5, output[0x100]);
        Assert.Equal(0x5A, output[0x101]);
        Assert.Equal(baseBytes[0x102], output[0x102]);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("general-map-1", StringComparison.Ordinal));
    }

    /// <summary>Verifies General Replace UI routes TP-touching explicit mappings through postbuild.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRunsPostbuildForTpMapping()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.PathFromRelative("expected/51950/dp-256k/flash.bin");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-tp");
        byte[] baseBytes = File.ReadAllBytes(basePath);
        string replacementPath = workspace.Write("self-nf.bin", baseBytes[0x22C00..0x22C02]);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowGeneralReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x22C00";
        mapping.EndAddress = "0x22C01";
        viewModel.SetGeneralReplaceMappingFile(mapping.MappingId, replacementPath);

        Assert.True(viewModel.CanPreviewReplace);
        Assert.Contains("run postbuild", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
            operation.HasCodeBlock &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
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
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc(ic);
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-{ic}");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = $"NT{ic}";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.BuildMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildStandardMerge);

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.BuildMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildStandardMerge);

        string outputPath = workspace.PathFor("selected-output.bin");
        await viewModel.BuildStandardMergeAsync(outputPath);

        string expectedPath = golden.ExpectedOutputPath(goldenCase);
        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.LastRunResult.Output);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(outputPath));
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(outputPath, viewModel.LoadedReport.OutputArtifactPath);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal(1, viewModel.ReportToastOpacity);
        Assert.Equal("Build report generated", viewModel.ReportToastText);
    }

    /// <summary>Verifies General Merge UI runs explicit mapping rows through Preview and Build.</summary>
    [Fact]
    public async Task GeneralMergePreviewAndBuildUseExplicitMappingRows()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-merge");
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13, 0x14]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SelectedMergeMode = "General";
        viewModel.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x1";
        mapping.TargetStartAddress = "0x4";
        mapping.Length = "0x3";
        List<string> propertyChanges = [];
        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                propertyChanges.Add(args.PropertyName);
            }
        };

        Assert.True(viewModel.SetGeneralMergeMappingFile(mapping.MappingId, source));

        Assert.Contains(nameof(MainWindowViewModel.MergeReadinessStatus), propertyChanges);
        Assert.Contains("maps 1 source BIN", viewModel.MergeReadinessStatus, StringComparison.Ordinal);
        Assert.True(viewModel.CanPreviewMerge);
        Assert.True(viewModel.CanBuildMerge);
        Assert.False(viewModel.CanBuildStandardMerge);

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildMerge);
        Assert.False(viewModel.CanBuildStandardMerge);

        string outputPath = workspace.PathFor("general-merge.bin");
        await viewModel.BuildMergeAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.LastRunResult.Output);
        Assert.Equal(
            [0, 0, 0, 0, 0x11, 0x12, 0x13, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            File.ReadAllBytes(outputPath));

        using var document = JsonDocument.Parse(viewModel.LoadedReportJson);
        JsonElement root = document.RootElement;
        Assert.Equal("nt51950-general-merge-workbench", root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-merge", root.GetProperty("ExperienceId").GetString());
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("CopyRange", operation.GetProperty("Kind").GetString());
        Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
    }

    /// <summary>Verifies Standard Merge Build validates the current context without a separate manual Preview.</summary>
    [Fact]
    public async Task BuildStandardMergeValidatesCurrentInputsWithoutManualPreview()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-merge-gate");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.CanPreviewStandardMerge);
        Assert.True(viewModel.CanBuildStandardMerge);

        JsonProperty firstInput = goldenCase.GetProperty("inputs").EnumerateObject().First();
        string replacementCopyPath = workspace.PathFor($"{firstInput.Name}-copy.bin");
        File.Copy(golden.ManifestPath(firstInput.Value), replacementCopyPath);
        viewModel.SetSlotFile(StandardMergeGoldenManifest.SlotIdForAddressSpace(firstInput.Name), replacementCopyPath);

        Assert.True(viewModel.CanPreviewStandardMerge);
        Assert.True(viewModel.CanBuildStandardMerge);

        viewModel.SelectedIc = "NT51927";

        Assert.True(viewModel.CanPreviewStandardMerge);
        Assert.True(viewModel.CanBuildStandardMerge);

        string outputPath = workspace.PathFor("blocked-standard-merge.bin");
        await viewModel.BuildStandardMergeAsync(outputPath);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal("Build blocked", viewModel.LastRunResult.Title);
        Assert.False(File.Exists(outputPath), outputPath);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Equal("input.address-space.length-mismatch", viewModel.LoadedReport.PrimaryIssue.Title);
        Assert.Contains("tp-input", viewModel.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
    }

    /// <summary>Verifies an NT51950 preview with NT51926 TP input is blocked with a reopenable detailed report.</summary>
    [Fact]
    public async Task PreviewNt51950WithNt51926InputsFailsWithDetailedReportAndNoOutput()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-950-negative");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.CanPreviewStandardMerge);
        Assert.True(viewModel.CanBuildStandardMerge);

        string outputPath = workspace.PathFor("should-not-exist.bin");
        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal("Preview blocked", viewModel.LastRunResult.Title);
        Assert.Equal("No output", viewModel.LastRunResult.Output);
        Assert.False(File.Exists(outputPath), outputPath);
        Assert.True(viewModel.CanBuildStandardMerge);
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

    /// <summary>Gets every owner-approved gen_flash Standard Merge golden case.</summary>
    public static TheoryData<string> StandardMergeGoldenCases()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        return golden.CaseIds();
    }


}
