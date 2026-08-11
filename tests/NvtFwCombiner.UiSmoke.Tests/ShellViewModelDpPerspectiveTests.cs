using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>DP Replace workflow and perspective smoke coverage.</summary>
public sealed partial class DpReplaceWorkflowTests
{
    /// <summary>Verifies Replace keeps the same visual-first coverage model as Merge.</summary>
    [Fact]
    public void ReplaceCoverageUsesReadableInclusiveSegments()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-coverage");
        string basePath = workspace.Write("base-40000.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.NotEmpty(viewModel.Replace.ReplaceCoverageSegments);
        Assert.All(viewModel.Replace.ReplaceCoverageSegments, segment =>
        {
            Assert.Contains("-", segment.RangeLabel, StringComparison.Ordinal);
            Assert.Contains("len 0x", segment.RangeLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("..", segment.RangeLabel, StringComparison.Ordinal);
        });
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment => segment.SourceLabel == "Base flash");
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel.Contains("Restored", StringComparison.Ordinal) ||
            segment.SourceLabel.Contains("Preserved", StringComparison.Ordinal));
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment => segment.SourceLabel is "Changed DP BIN" or "Changed LDC BIN");
        Assert.Equal(
            "Build blocked: Reference FlashCode and required DP replacement inputs are required.",
            viewModel.Replace.ReplaceReadinessStatus);
    }

    /// <summary>Verifies NT51950 DP Replace does not draw a max-length range before the base BIN is selected.</summary>
    [Fact]
    public void Nt51950DpReplaceCoverageWaitsForSelectedBaseLength()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);

        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal("Pending", segment.RangeLabel);
        Assert.Equal("Pending input", segment.SourceLabel);
        Assert.Contains("required inputs", segment.Detail, StringComparison.Ordinal);
    }

    /// <summary>Verifies Merge coverage rows expose final ownership without report-level operation text.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void DpPerspectiveMergeCoverageWaitsForSelectedDpLength(string icId)
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;

        MemoryMapRowViewModel initialRow = Assert.Single(viewModel.Merge.MergeMemoryRows);
        Assert.Equal("Pending", initialRow.RangeLabel);
        Assert.Equal("Select", initialRow.ActionLabel);
        Assert.Equal("No output -> Pending input", initialRow.FlowLabel);
        Assert.Equal("Memory layout pending", viewModel.Merge.MergeMemoryRangeLabel);
        MemoryCoverageSegmentViewModel pendingSegment = Assert.Single(viewModel.Merge.MergeCoverageSegments);
        Assert.Equal("Pending", pendingSegment.RangeLabel);
        Assert.Equal("Pending input", pendingSegment.SourceLabel);
        Assert.Contains("required inputs", pendingSegment.Detail, StringComparison.Ordinal);
        Assert.All(viewModel.Merge.MergeCoverageSegments, segment =>
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
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-initial");
        string dpPath = workspace.Write("dp-40000.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.SetSlotFile("merge-dp", dpPath);

        Assert.Equal("0x00000-0x3FFFF (len 0x40000)", viewModel.Merge.MergeMemoryRangeLabel);
        Assert.Contains(viewModel.Merge.MergeMemoryRows, row => row.AfterSource == "DP BIN");
        Assert.Contains(viewModel.Merge.MergeMemoryRows, row => row.AfterSource == "TP BIN");
        Assert.All(viewModel.Merge.MergeCoverageSegments, segment =>
        {
            Assert.DoesNotContain("0xFFFFF", segment.RangeLabel, StringComparison.Ordinal);
        });
    }

    /// <summary>Verifies canonical DP slots and the NT51928-only LDC slot are exposed independently.</summary>
    [Fact]
    public void GenFlashDpReplaceSlotsIncludeLdcOnlyForNt51928()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51927";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);

        Assert.True(viewModel.Replace.IsStructuredReplaceModeSelected);
        Assert.Equal(
            ["replace-base", "replace-dp"],
            viewModel.Replace.ReplaceSlots.Select(static slot => slot.SlotId));

        viewModel.WorkflowSession.SelectedIc = "NT51928";

        Assert.True(viewModel.Replace.IsStructuredReplaceModeSelected);
        Assert.Equal(
            ["replace-base", "replace-dp", "replace-ldc"],
            viewModel.Replace.ReplaceSlots.Select(static slot => slot.SlotId));
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
    }

    /// <summary>DP inputs do not display an undeclared IC-specific container hint.</summary>
    [Fact]
    public void DpSlotsDoNotInventInitialCodeAndLdcHint()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51951";
        viewModel.ShowMergeCommand.Execute(null);

        FirmwareSlotViewModel mergeDp = Assert.Single(
            viewModel.Merge.MergeSlots,
            slot => slot.SlotId == CompositionSlotIds.MergeDp);
        Assert.DoesNotContain("Initial Code + LDC", mergeDp.Description, StringComparison.Ordinal);

        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);
        FirmwareSlotViewModel replaceDp = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            slot => slot.SlotId == CompositionSlotIds.ReplaceDp);
        Assert.DoesNotContain("Initial Code + LDC", replaceDp.Description, StringComparison.Ordinal);

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        Assert.DoesNotContain("Initial Code + LDC", viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == CompositionSlotIds.MergeDp).Description, StringComparison.Ordinal);
    }

    /// <summary>Verifies NT51950 DP Replace restores only TP bytes while customer information follows replacement DP.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public async Task BuildNt51950DpReplaceUsesSelectedBaseLengthAndRestoresBaseTpRange(int baseLength)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-replace");
        byte[] baseBytes = CreatePattern(baseLength, 0x80);
        byte[] replacementBytes = CreatePattern(baseLength, 0x20);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement-dp.bin", replacementBytes);
        string outputPath = workspace.PathFor($"nt51950-dp-replace-{baseLength:X}.bin");

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        Assert.All(
            viewModel.Replace.ReplaceSlots.Where(static slot => slot.HasFile),
            static slot => Assert.True(
                slot.InputInspectionSeverity is not null && !slot.BlocksBuild,
                $"{slot.SlotId}: {slot.InputInspectionSeverity}; {slot.InputInspectionStatus}"));
        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.Replace.BuildReplaceCommand.CanExecute(null));
        Assert.True(viewModel.Replace.CanBuildReplace);

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Replace.CanBuildReplace);

        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath), outputPath);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(baseLength, output.Length);
        Assert.Equal(replacementBytes[0x9FFF], output[0x9FFF]);
        Assert.Equal(baseBytes[0x0A000], output[0x0A000]);
        Assert.Equal(baseBytes[0x36FFF], output[0x36FFF]);
        Assert.Equal(replacementBytes[0x37000], output[0x37000]);
        Assert.Equal(replacementBytes[0x37FFF], output[0x37FFF]);
        Assert.Equal(replacementBytes[^1], output[^1]);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.Contains(viewModel.Reports.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-tp", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.Reports.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-customer-info", StringComparison.Ordinal));

        using var reportDocument = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        Assert.Equal(baseLength, reportDocument.RootElement.GetProperty("Output").GetProperty("Size").GetInt64());
    }

    /// <summary>Verifies golden-backed NT51950/NT51951 DP Replace accepts the real 0x40000 and 0x80000 base lengths.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public async Task PreviewDpReplaceAcceptsGoldenBackedBaseLengths(
        string icId,
        int expectedLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc(icId[2..]);
        string basePath = golden.ExpectedOutputPath(goldenCase);
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = icId;
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", dpPath);

        Assert.Equal(
            $"0x00000-0x{expectedLength - 1:X5} (len 0x{expectedLength:X})",
            viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Contains(viewModel.Replace.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace" && row.AfterSource == "Changed DP BIN");
        Assert.Contains(viewModel.Replace.ReplaceMemoryRows, row =>
            row.ActionLabel == "Restore" && row.AfterSource == "Base flash");

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        using var reportDocument = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement root = reportDocument.RootElement;
        Assert.Equal(expectedLength, root.GetProperty("Output").GetProperty("Size").GetInt64());
        Assert.Equal(0, root.GetProperty("Issues").GetArrayLength());
        Assert.Contains(root.GetProperty("Operations").EnumerateArray(), operation =>
            operation.GetProperty("OperationId").GetString() == "replace-dp-container" &&
            operation.GetProperty("TargetRange").GetProperty("Length").GetInt64() == expectedLength);
    }

    /// <summary>Verifies Replace Build validates the current file set without a separate manual Preview.</summary>
    [Fact]
    public async Task BuildReplaceValidatesCurrentInputsWithoutManualPreview()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-replace-gate");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x90));
        string replacementPath = workspace.Write("replacement-dp.bin", CreatePattern(0x40000, 0x30));
        string replacementPath2 = workspace.PathFor("replacement-dp-copy.bin");
        string outputPath = workspace.PathFor("blocked-output.bin");
        File.Copy(replacementPath, replacementPath2);

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.Replace.CanBuildReplace);

        viewModel.SetSlotFile("replace-dp", replacementPath2);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.Replace.CanBuildReplace);
        Assert.True(viewModel.Replace.BuildReplaceCommand.CanExecute(null));

        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.CanOpenReport);
        Assert.False(viewModel.Reports.LoadedReport.HasPrimaryIssue);
    }

    /// <summary>Verifies NT51950 DP Replace rejects unapproved base lengths instead of assuming 0x100000.</summary>
    [Fact]
    public async Task PreviewNt51950DpReplaceRejectsUnsupportedBaseLength()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-replace-invalid");
        string basePath = workspace.Write("base-60000.bin", new byte[0x60000]);
        string replacementPath = workspace.Write("replacement-dp.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Contains("0x40000 / 0x80000 / 0x100000", viewModel.RunSession.LastRunResult.Detail, StringComparison.Ordinal);
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal("Pending", segment.RangeLabel);
        Assert.Equal("Pending input", segment.SourceLabel);
        Assert.False(segment.IsChanged);
    }
}
