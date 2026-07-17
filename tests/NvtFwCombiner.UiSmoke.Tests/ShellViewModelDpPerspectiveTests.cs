using System.Text.Json;
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

        viewModel.ShowDpReplaceCommand.Execute(null);
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
            "Build blocked: base BIN and required DP replacement inputs are required.",
            viewModel.ReplaceReadinessStatus);
    }

    /// <summary>Verifies NT51950 DP Replace does not draw a max-length range before the base BIN is selected.</summary>
    [Fact]
    public void Nt51950DpReplaceCoverageWaitsForSelectedBaseLength()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        viewModel.ShowDpReplaceCommand.Execute(null);

        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.ReplaceCoverageSegments);
        Assert.Equal("Base BIN length: 0x40000 / 0x80000 / 0x100000", viewModel.ReplaceMemoryRangeLabel);
        Assert.Equal("Base length pending", segment.RangeLabel);
        Assert.Equal("DP base required", segment.SourceLabel);
        Assert.Equal(
            "Output range will follow the selected base BIN length.",
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

    /// <summary>Verifies Standard Merge LDC evidence does not promote NT51927/NT51928 to DP Replace support.</summary>
    [Fact]
    public void DpReplaceSlotsStayClosedForNonV2Ics()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51927";
        viewModel.ShowDpReplaceCommand.Execute(null);

        Assert.Equal(["Base flash BIN"], viewModel.ReplaceSlots.Select(slot => slot.Title));

        viewModel.SelectedIc = "NT51928";

        Assert.Equal(["Base flash BIN"], viewModel.ReplaceSlots.Select(slot => slot.Title));
        Assert.Contains("Not Supported", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);
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
        int replacementLength = baseLength - 0x1000;
        byte[] replacementBytes = CreatePattern(replacementLength, 0x20);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement-dp.bin", replacementBytes);
        string outputPath = workspace.PathFor($"nt51950-dp-replace-{baseLength:X}.bin");

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowDpReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

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
        Assert.Equal(0, output[replacementLength]);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-tp", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.LoadedReport.Operations, operation => operation.Title.Contains("restore-base-customer-info", StringComparison.Ordinal));

        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        Assert.Equal(baseLength, reportDocument.RootElement.GetProperty("Output").GetProperty("Size").GetInt64());
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

        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.PathFromRelative(baseRelativePath);
        string dpPath = golden.PathFromRelative(dpRelativePath);
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

    /// <summary>Verifies Replace Build validates the current file set without a separate manual Preview.</summary>
    [Fact]
    public async Task BuildReplaceValidatesCurrentInputsWithoutManualPreview()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-replace-gate");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x90));
        string replacementPath = workspace.Write("replacement-dp.bin", CreatePattern(0x3F000, 0x30));
        string replacementPath2 = workspace.PathFor("replacement-dp-copy.bin");
        string outputPath = workspace.PathFor("blocked-output.bin");
        File.Copy(replacementPath, replacementPath2);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowDpReplaceCommand.Execute(null);
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
        viewModel.ShowDpReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-dp", replacementPath);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Contains("0x40000 / 0x80000 / 0x100000", viewModel.LastRunResult.Detail, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace" &&
            row.RangeLabel == "Unsupported base BIN length 0x60000");
        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.ReplaceCoverageSegments);
        Assert.Equal("Unsupported 0x60000", segment.RangeLabel);
        Assert.Equal("Unsupported base", segment.SourceLabel);
        Assert.Equal(
            "This base BIN length is blocked by profile policy.",
            segment.CompactDetail);
        Assert.False(segment.IsChanged);
    }
}
