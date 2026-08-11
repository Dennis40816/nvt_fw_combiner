using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);

        Assert.True(viewModel.Replace.IsGeneralReplaceModeSelected);
        Assert.False(viewModel.Replace.IsStructuredReplaceModeSelected);
        Assert.Empty(viewModel.Replace.ReplaceSlots);
        Assert.Equal("replace-base", viewModel.Replace.ReplaceBaseSlot.SlotId);
        Assert.NotEmpty(viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Contains("explicit profile-approved", viewModel.Replace.SelectedReplaceModeDescription, StringComparison.Ordinal);
        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.Equal(
            "Build blocked: base BIN and at least one explicit replacement mapping are required.",
            viewModel.Replace.ReplaceReadinessStatus);
        GeneralReplaceMappingViewModel initial = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        Assert.False(initial.CanSelectFile);
        Assert.True(initial.IsFileSelectionPending);

        viewModel.Replace.AddGeneralReplaceMappingCommand.Execute(null);
        Assert.Equal(2, viewModel.Replace.GeneralReplaceMappings.Count);

        viewModel.WorkflowSession.RemoveGeneralMappingRow(viewModel.Replace.GeneralReplaceMappings[0]);
        _ = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        Assert.Equal(1, viewModel.Replace.GeneralReplaceMappings[0].Index);
        Assert.Equal("No replacement BIN selected", viewModel.Replace.GeneralReplaceMappings[0].DisplayName);
        Assert.Equal(string.Empty, viewModel.Replace.GeneralReplaceMappings[0].DisplayDetail);
    }

    /// <summary>Reference inspection owns General Replace BIN-selection availability.</summary>
    [Fact]
    public void GeneralReplaceMappingSelectionWaitsForReferenceInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-prerequisite");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";

        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        Assert.False(mapping.HasFile);
        Assert.True(mapping.IsFileSelectionPending);
        viewModel.SetSlotFile("replace-base", basePath);
        Assert.True(mapping.CanSelectFile);

        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        Assert.Equal(replacementPath, mapping.FilePath);
        Assert.True(
            mapping.AcceptedFileStamp is not null,
            $"{mapping.InspectionIssueMessage} | {viewModel.Replace.ReplaceReadinessStatus}");
    }

    /// <summary>Verifies a General Replace shape without an exact V2 route fails closed.</summary>
    [Fact]
    public void GeneralReplaceWithoutExactRouteStaysBlocked()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace");
        byte[] baseBytes = CreatePattern(0x40000, 0x40);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x00100";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.Contains("Not available", viewModel.Replace.ReplaceReadinessStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Reports.HasLoadedReport);
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

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal("nt51926-general-replace-dp-single-candidate", viewModel.Reports.LoadedReport.ProfileId);

        await File.WriteAllBytesAsync(
            replacementPath,
            [0xA5, 0xC3],
            TestContext.Current.CancellationToken);
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal([0xA5, 0x5A], File.ReadAllBytes(outputPath)[0x3E020..0x3E022]);

        viewModel.SetSlotFile(mapping.MappingId, replacementPath);
        Assert.True(
            mapping.AcceptedFileStamp == FileStamp.FromBytes([0xA5, 0xC3]),
            $"{mapping.InspectionIssueMessage} | {viewModel.Replace.ReplaceReadinessStatus}");
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal([0xA5, 0xC3], File.ReadAllBytes(outputPath)[0x3E020..0x3E022]);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
    }

    /// <summary>General Replace reuses the immutable Base accepted by readiness.</summary>
    [Fact]
    public async Task GeneralReplaceBuildReusesAcceptedBaseAfterPathMutation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-base-stamp");
        byte[] baseBytes = CreatePattern(0x40000, 0x26);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("general-replace.bin");

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);

        byte[] acceptedBaseBytes = [.. baseBytes];
        baseBytes[0] ^= 0xFF;
        await File.WriteAllBytesAsync(
            basePath,
            baseBytes,
            TestContext.Current.CancellationToken);
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        acceptedBaseBytes[0x3E020] = 0xA5;
        acceptedBaseBytes[0x3E021] = 0x5A;
        Assert.Equal(acceptedBaseBytes, File.ReadAllBytes(outputPath));
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
    }

    /// <summary>A queued General Replace run cannot swap or overwrite its immutable draft.</summary>
    [Fact]
    public async Task GeneralReplaceRunUsesCapturedDraftAndRejectsStalePublication()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-replace-run-snapshot");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x26));
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, sourcePath);

        Task previewTask = viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        mapping.TargetStartAddress = "0x3E030";
        await previewTask;
        await viewModel.Replace.GeneralReplaceReadinessRefreshTask;

        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement operation = Assert.Single(report.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal(0x3E020, operation.GetProperty("TargetRange").GetProperty("Start").GetInt64());
        Assert.Equal("0x3E030", mapping.TargetStartAddress);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        using var currentReport = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement currentOperation = Assert.Single(
            currentReport.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal(0x3E030, currentOperation.GetProperty("TargetRange").GetProperty("Start").GetInt64());
    }

    /// <summary>Verifies TP-touching General Replace remains fail-closed without an exact V2 route.</summary>
    [Fact]
    public void GeneralReplaceTpMappingWithoutExactRouteStaysBlocked()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51950"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-tp");
        byte[] baseBytes = File.ReadAllBytes(basePath);
        string replacementPath = workspace.Write("self-nf.bin", baseBytes[0x22C00..0x22C02]);

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x22C00";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, replacementPath);

        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.Contains("Not available", viewModel.Replace.ReplaceReadinessStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Reports.HasLoadedReport);
    }

}
