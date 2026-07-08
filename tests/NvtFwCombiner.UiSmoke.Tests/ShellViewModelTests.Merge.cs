using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
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
