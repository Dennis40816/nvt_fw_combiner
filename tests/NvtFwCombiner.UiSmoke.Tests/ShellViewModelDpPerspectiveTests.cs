using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies Replace keeps the same visual-first coverage model as Merge.</summary>
    [Fact]
    public void ReplaceCoverageUsesReadableInclusiveSegments()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-coverage");
        string basePath = workspace.Write("base-40000.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        OpenReplace(viewModel, "DP");
        viewModel.SetSlotFile("replace-base", basePath);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.NotEmpty(viewModel.ReplaceCoverageSegments);
        Assert.All(viewModel.ReplaceCoverageSegments, segment =>
        {
            Assert.Contains("-", segment.RangeLabel, StringComparison.Ordinal);
            Assert.Contains("len 0x", segment.RangeLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("..", segment.RangeLabel, StringComparison.Ordinal);
        });
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.SourceLabel == "Base flash");
        Assert.DoesNotContain(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel.Contains("Restored", StringComparison.Ordinal) ||
            segment.SourceLabel.Contains("Preserved", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.SourceLabel is "Changed DP BIN" or "Changed LDC BIN");
        Assert.Equal(
            "Build blocked: Reference FlashCode and required DP replacement inputs are required.",
            viewModel.ReplaceReadinessStatus);
    }

    /// <summary>Verifies NT51950 DP Replace does not draw a max-length range before the base BIN is selected.</summary>
    [Fact]
    public void Nt51950DpReplaceCoverageWaitsForSelectedBaseLength()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, "DP");

        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.ReplaceCoverageSegments);
        Assert.Equal("Reference FlashCode length: 0x40000 / 0x80000 / 0x100000", viewModel.ReplaceMemoryRangeLabel);
        Assert.Equal("Reference length pending", segment.RangeLabel);
        Assert.Equal("Reference FlashCode required", segment.SourceLabel);
        Assert.Equal(
            "Output range will follow the selected Reference FlashCode length.",
            segment.CompactDetail);
        Assert.Contains("actual DP Replace length", segment.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00000-0xFFFFF", segment.RangeLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("max", segment.Detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies Merge coverage rows expose final ownership without report-level operation text.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void DpPerspectiveMergeCoverageWaitsForSelectedDpLength(string icId)
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SelectedIc = icId;

        MemoryMapRowViewModel initialRow = Assert.Single(
            viewModel.MergeMemoryRows,
            row => row.ActionLabel == "Initialize");
        Assert.Equal("Selected DP BIN length pending", initialRow.RangeLabel);
        Assert.Contains("Supported DP lengths are 0x40000 / 0x80000 / 0x100000", initialRow.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("0xFFFFF", initialRow.Detail, StringComparison.Ordinal);
        Assert.Equal("No output -> Reserved", initialRow.FlowLabel);
        Assert.Equal("Selected DP BIN length pending", viewModel.MergeMemoryRangeLabel);
        _ = Assert.Single(viewModel.MergeMemoryRows);
        MemoryCoverageSegmentViewModel pendingSegment = Assert.Single(viewModel.MergeCoverageSegments);
        Assert.Equal("Selected DP BIN length pending", pendingSegment.RangeLabel);
        Assert.Equal("DP length pending", pendingSegment.SourceLabel);
        Assert.Equal(
            "Output range will follow the selected DP BIN length.",
            pendingSegment.CompactDetail);
        Assert.Contains("Select a DP BIN before final ownership is drawn", pendingSegment.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("0xFFFFF", pendingSegment.RangeLabel, StringComparison.Ordinal);
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
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-initial");
        string dpPath = workspace.Write("dp-40000.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        viewModel.SetSlotFile("merge-dp", dpPath);

        MemoryMapRowViewModel initialRow = Assert.Single(
            viewModel.MergeMemoryRows,
            row => row.ActionLabel == "Initialize");
        Assert.Equal("0x00000-0x3FFFF (len 0x40000)", initialRow.RangeLabel);
        Assert.Contains("selected DP BIN length", initialRow.Detail, StringComparison.Ordinal);
        Assert.Equal("0x00000-0x3FFFF (len 0x40000)", viewModel.MergeMemoryRangeLabel);
        Assert.All(viewModel.MergeCoverageSegments, segment =>
        {
            Assert.DoesNotContain("0xFFFFF", segment.RangeLabel, StringComparison.Ordinal);
        });
    }

    /// <summary>Verifies canonical DP slots and the NT51928-only LDC slot are exposed independently.</summary>
    [Fact]
    public void GenFlashDpReplaceSlotsIncludeLdcOnlyForNt51928()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51927";
        OpenReplace(viewModel, "DP");

        Assert.True(viewModel.IsStructuredReplaceModeSelected);
        Assert.Equal(
            ["replace-base", "replace-dp"],
            viewModel.ReplaceSlots.Select(static slot => slot.SlotId));

        viewModel.SelectedIc = "NT51928";

        Assert.True(viewModel.IsStructuredReplaceModeSelected);
        Assert.Equal(
            ["replace-base", "replace-dp", "replace-ldc"],
            viewModel.ReplaceSlots.Select(static slot => slot.SlotId));
        Assert.Equal(
            "Reference FlashCode length: 0x40000 / 0x80000",
            viewModel.ReplaceMemoryRangeLabel);
    }

    /// <summary>NT51951 DP inputs explain that the container includes Initial Code and LDC.</summary>
    [Fact]
    public void Nt51951DpSlotsExposeInitialCodeAndLdcHint()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51951";
        viewModel.ShowMergeCommand.Execute(null);

        FirmwareSlotViewModel mergeDp = Assert.Single(
            viewModel.MergeSlots,
            slot => slot.SlotId == WorkbenchSlotIds.MergeDp);
        Assert.EndsWith("(Initial Code + LDC)", mergeDp.Description, StringComparison.Ordinal);

        OpenReplace(viewModel, "DP");
        FirmwareSlotViewModel replaceDp = Assert.Single(
            viewModel.ReplaceSlots,
            slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.EndsWith("(Initial Code + LDC)", replaceDp.Description, StringComparison.Ordinal);

        viewModel.SelectedIc = "NT51950";
        Assert.DoesNotContain("Initial Code + LDC", viewModel.MergeSlots.Single(slot => slot.SlotId == WorkbenchSlotIds.MergeDp).Description, StringComparison.Ordinal);
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

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, "DP");
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        Assert.All(
            viewModel.ReplaceSlots.Where(static slot => slot.HasFile),
            static slot => Assert.True(
                slot.InputInspectionSeverity is not null && !slot.BlocksBuild,
                $"{slot.SlotId}: {slot.InputInspectionSeverity}; {slot.InputInspectionStatus}"));
        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.BuildReplaceCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildReplace);

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
        Assert.Equal(replacementBytes[0x37FFF], output[0x37FFF]);
        Assert.Equal(replacementBytes[^1], output[^1]);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-tp", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-customer-info", StringComparison.Ordinal));

        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
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
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = icId;
        OpenReplace(viewModel, "DP");
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

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, "DP");
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildReplace);

        viewModel.SetSlotFile("replace-dp", replacementPath2);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildReplace);
        Assert.True(viewModel.BuildReplaceCommand.CanExecute(null));

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
    }

    /// <summary>Verifies NT51950 DP Replace rejects unapproved base lengths instead of assuming 0x100000.</summary>
    [Fact]
    public async Task PreviewNt51950DpReplaceRejectsUnsupportedBaseLength()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-replace-invalid");
        string basePath = workspace.Write("base-60000.bin", new byte[0x60000]);
        string replacementPath = workspace.Write("replacement-dp.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, "DP");
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Contains("0x40000 / 0x80000 / 0x100000", viewModel.LastRunResult.Detail, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace" &&
            row.RangeLabel == "Unsupported Reference FlashCode length 0x60000");
        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.ReplaceCoverageSegments);
        Assert.Equal("Unsupported 0x60000", segment.RangeLabel);
        Assert.Equal("Unsupported reference", segment.SourceLabel);
        Assert.Equal(
            "This Reference FlashCode length is blocked by profile policy.",
            segment.CompactDetail);
        Assert.False(segment.IsChanged);
    }
}
