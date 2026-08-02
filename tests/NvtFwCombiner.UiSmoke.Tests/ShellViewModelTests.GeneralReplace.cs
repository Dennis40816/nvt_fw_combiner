using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies immutable language bundles are reused instead of rebuilt by each shell projection.</summary>
    [Theory]
    [InlineData(ShellLanguage.English)]
    [InlineData(ShellLanguage.ChineseTraditional)]
    public void LocalizedShellBundlesAreCached(ShellLanguage language)
    {
        Assert.Same(ShellTextResources.For(language), ShellTextResources.For(language));
    }

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

    /// <summary>Verifies a General Replace shape without an exact V2 route fails closed.</summary>
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

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal(
            "The selected General Replace shape has no exact evidence-backed V2 route.",
            viewModel.LastRunResult.Detail);
        Assert.True(viewModel.Reports.HasLoadedReport);
        using (var previewReport = JsonDocument.Parse(viewModel.Reports.LoadedReportJson))
        {
            JsonElement issue = Assert.Single(previewReport.RootElement.GetProperty("Issues").EnumerateArray());
            Assert.Equal("replace.workflow.not-supported", issue.GetProperty("Code").GetString());
        }

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal(
            "The selected General Replace shape has no exact evidence-backed V2 route.",
            viewModel.LastRunResult.Detail);
        Assert.False(File.Exists(outputPath));
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
        Assert.Equal("nt51926-general-replace-dp-single-candidate", viewModel.Reports.LoadedReport.ProfileId);

        await File.WriteAllBytesAsync(
            replacementPath,
            [0xA5, 0xC3],
            TestContext.Current.CancellationToken);
        await viewModel.BuildReplaceAsync(outputPath);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Contains(
            "no longer matches",
            viewModel.LastRunResult.Detail,
            StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));

        viewModel.SetSlotFile(mapping.MappingId, replacementPath);
        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal([0xA5, 0xC3], File.ReadAllBytes(outputPath)[0x3E020..0x3E022]);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
    }

    /// <summary>Verifies TP-touching General Replace remains fail-closed without an exact V2 route.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRunsPostbuildForTpMapping()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51950"));
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

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal(
            "The selected General Replace shape has no exact evidence-backed V2 route.",
            viewModel.LastRunResult.Detail);
        Assert.True(viewModel.Reports.HasLoadedReport);
        ReportLineViewModel issue = Assert.Single(viewModel.Reports.LoadedReport.Issues);
        Assert.Contains("no exact evidence-backed V2 route", issue.Detail, StringComparison.Ordinal);
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement reportIssue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal("replace.workflow.not-supported", reportIssue.GetProperty("Code").GetString());
    }

}
