using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
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
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.HasCodeBlock &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
    }
}
