using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies hexadecimal viewport labels follow the selected shell language.</summary>
    [Fact]
    public void HexEditorLabelsAreLocalized()
    {
        var english = ShellTextResources.For(ShellLanguage.English);
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);

        Assert.Equal("Address", english.HexEditorAddressColumnLabel);
        Assert.Equal("位址", traditionalChinese.HexEditorAddressColumnLabel);
        Assert.Equal("ASCII", english.HexEditorAsciiColumnLabel);
        Assert.Equal("ASCII", traditionalChinese.HexEditorAsciiColumnLabel);
    }

    /// <summary>Verifies every bindable string is populated in both supported language bundles.</summary>
    [Theory]
    [InlineData(ShellLanguage.English)]
    [InlineData(ShellLanguage.ChineseTraditional)]
    public void LocalizedShellBundlesPopulateEveryString(ShellLanguage language)
    {
        var resources = ShellTextResources.For(language);
        IEnumerable<string> emptyProperties = typeof(ShellTextResources)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Where(property => string.IsNullOrEmpty((string?)property.GetValue(resources)))
            .Select(property => property.Name);

        Assert.Empty(emptyProperties);
    }

    /// <summary>Verifies General Replace authors base BIN and explicit range rows as separate UI state.</summary>
    [Fact]
    public void GeneralReplaceUsesIndependentBaseAndEditableMappings()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        OpenReplace(viewModel, "General");

        Assert.True(viewModel.IsGeneralReplaceModeSelected);
        Assert.False(viewModel.IsStructuredReplaceModeSelected);
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.Equal("replace-base", viewModel.ReplaceBaseSlot.SlotId);
        Assert.NotEmpty(viewModel.ReplaceCoverageSegments);
        Assert.Contains("len 0x", viewModel.ReplaceMemoryRangeLabel, StringComparison.Ordinal);
        Assert.Contains("explicit profile-approved", viewModel.SelectedReplaceModeDescription, StringComparison.Ordinal);
        Assert.False(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.Equal(
            "Build blocked: base BIN and at least one explicit replacement mapping are required.",
            viewModel.ReplaceReadinessStatus);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);

        viewModel.AddGeneralReplaceMappingCommand.Execute(null);
        Assert.Equal(2, viewModel.GeneralReplaceMappings.Count);

        viewModel.RemoveGeneralMappingRow(viewModel.GeneralReplaceMappings[0]);
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
        OpenReplace(viewModel, "General");
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x00100";
        mapping.EndAddress = "0x00101";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.Contains("Ready", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildReplace, viewModel.ReplaceReadinessStatus);

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(0xA5, output[0x100]);
        Assert.Equal(0x5A, output[0x101]);
        Assert.Equal(baseBytes[0x102], output[0x102]);
        Assert.Equal(
            Application.Composition.CompositionRunPhase.PreparingReport,
            viewModel.CompositionProgress.CurrentPhase);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("general-map-1", StringComparison.Ordinal));
        Assert.True(viewModel.LoadedReport.HexDiff.IsAvailable);
        Assert.Equal("output-image", viewModel.LoadedReport.HexDiff.OutputSpaceId);
        Assert.Contains(viewModel.LoadedReport.HexDiff.VisibleRows, row =>
            row.Start == 0x100 && row.ChangedMask == 0b11);

        viewModel.SelectedLanguage = "Traditional Chinese";
        await Assert.IsType<Task>(viewModel.ReportRelocalizationTask, exactMatch: false);
        Assert.Equal("唯讀 Hex Diff", viewModel.Text.HexDiffViewportTitle);
        Assert.Equal("在變更列下方顯示原始內容", viewModel.Text.HexDiffShowOriginalRowsLabel);
        Assert.Equal("變更資訊", viewModel.Text.HexDiffChangeInformationTitle);
        Assert.Equal("變更前 SHA-256", viewModel.Text.HexDiffBeforeSha256Label);
        Assert.Equal("變更後 SHA-256", viewModel.Text.HexDiffAfterSha256Label);
        ReportHexDiffViewModel chineseHexDiff = viewModel.LoadedReport.HexDiff;
        Assert.True(chineseHexDiff.IsAvailable);
        Assert.Equal("完整 Hex Diff", chineseHexDiff.AvailabilityTitle);
        ReportHexDiffRangeViewModel chineseRange = Assert.IsType<ReportHexDiffRangeViewModel>(
            chineseHexDiff.SelectedRange);
        Assert.Contains("半開區間", chineseRange.AccessibleRange, StringComparison.Ordinal);
        Assert.Equal("預期", chineseRange.Status);
        Assert.Contains("預期", chineseRange.AccessibleLabel, StringComparison.Ordinal);
        ReportHexDiffViewportRowViewModel chineseChangedRow = Assert.Single(
            chineseHexDiff.VisibleRows,
            static row => row.Start == 0x100);
        Assert.Contains("輸出", chineseChangedRow.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("已變更", chineseChangedRow.AccessibleLabel, StringComparison.Ordinal);
        chineseHexDiff.ShowOriginalRows = true;
        Assert.Contains("原始", chineseChangedRow.AccessibleLabel, StringComparison.Ordinal);
        chineseHexDiff.JumpAddress = "0x101";
        chineseHexDiff.JumpAddressCommand.Execute(null);
        Assert.Contains("位址", chineseHexDiff.JumpStatus, StringComparison.Ordinal);
        chineseHexDiff.JumpAddress = "0x40000";
        chineseHexDiff.JumpAddressCommand.Execute(null);
        Assert.Contains("請輸入", chineseHexDiff.JumpStatus, StringComparison.Ordinal);
    }

    /// <summary>Verifies the shared UI reaches the NT51926 single full-Flash DP-only V2 route.</summary>
    [Fact]
    public async Task GeneralReplaceNt51926DpOnlyBuildUsesV2Candidate()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-51926-v2");
        byte[] baseBytes = CreatePattern(0x40000, 0x26);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("general-replace.bin");

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        OpenReplace(viewModel, "General");
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x3E020";
        mapping.EndAddress = "0x3E021";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal("nt51926-general-replace-dp-single-candidate", viewModel.LoadedReport.ProfileId);

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal([0xA5, 0x5A], File.ReadAllBytes(outputPath)[0x3E020..0x3E022]);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
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
        OpenReplace(viewModel, "General");
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x22C00";
        mapping.EndAddress = "0x22C01";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        Assert.True(viewModel.CanBuildReplace);
        Assert.Contains("run postbuild", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.HasCodeBlock &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(GetCommandOperations(viewModel.LoadedReport), operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
    }

}
