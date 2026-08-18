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
        Assert.True(viewModel.Replace.ShowsGenericCoverageStateLegend);
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
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel is "Replacement DP BIN" or "Replacement LDC BIN");
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
        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal("Not available", segment.RangeLabel);
        Assert.Equal("Waiting for Base BIN", segment.SourceLabel);
        Assert.Contains("Base BIN", segment.Detail, StringComparison.Ordinal);

        viewModel.SelectedLanguage = "Traditional Chinese";

        segment = Assert.Single(viewModel.Replace.ReplaceCoverageSegments);
        MemoryMapRowViewModel row = Assert.Single(viewModel.Replace.ReplaceMemoryRows);
        Assert.Equal("無法取得", segment.RangeLabel);
        Assert.Equal("等待 Base BIN", segment.SourceLabel);
        Assert.Equal("無法取得", row.RangeLabel);
        Assert.Equal("瀏覽", row.ActionLabel);
        Assert.Equal("無輸出 -> 等待 Base BIN", row.FlowLabel);
        Assert.Contains("載入並檢查 Base BIN", segment.Detail, StringComparison.Ordinal);
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
        Assert.Equal("Not available", initialRow.RangeLabel);
        Assert.Equal("Browse", initialRow.ActionLabel);
        Assert.Equal("No output -> Waiting for DP BIN", initialRow.FlowLabel);
        Assert.Equal("Waiting for DP BIN", viewModel.Merge.MergeMemoryRangeLabel);
        MemoryCoverageSegmentViewModel pendingSegment = Assert.Single(viewModel.Merge.MergeCoverageSegments);
        Assert.Equal("Not available", pendingSegment.RangeLabel);
        Assert.Equal("Waiting for DP BIN", pendingSegment.SourceLabel);
        Assert.Contains("DP BIN", pendingSegment.Detail, StringComparison.Ordinal);
        Assert.All(viewModel.Merge.MergeCoverageSegments, segment =>
        {
            Assert.NotEqual("Preserved", segment.ChangeLabel);
            Assert.DoesNotContain("CopyRange", segment.CompactDetail, StringComparison.Ordinal);
            Assert.DoesNotContain("Copies source", segment.CompactDetail, StringComparison.Ordinal);
        });
    }

    /// <summary>Verifies DP Perspective coverage uses final writers while protected customer information stays neutral.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public void DpPerspectiveCoverageUsesWriterColorsAndProtectedCustomerInformation(
        string icId,
        int capacity)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-initial");
        string dpPath = workspace.Write($"dp-{capacity:X}.bin", new byte[capacity]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.SetSlotFile("merge-dp", dpPath);

        Assert.Equal(
            $"0x00000-0x{capacity - 1:X5} (len 0x{capacity:X})",
            viewModel.Merge.MergeMemoryRangeLabel);
        Assert.Contains(viewModel.Merge.MergeMemoryRows, row => row.AfterSource == "DP BIN");
        Assert.Contains(viewModel.Merge.MergeMemoryRows, row => row.AfterSource == "TP BIN");
        Assert.Collection(
            viewModel.Merge.MergeCoverageSegments,
            segment => AssertCoverageSegment(
                segment,
                "DP BIN",
                "0x00000-0x09FFF (len 0xA000)",
                MemoryCoverageFillRole.Dp,
                "Output range will be copied from DP BIN."),
            segment => AssertCoverageSegment(
                segment,
                "TP BIN",
                "0x0A000-0x36FFF (len 0x2D000)",
                MemoryCoverageFillRole.Tp,
                "Output range will be overlaid from TP BIN."),
            segment => AssertCoverageSegment(
                segment,
                "Reserved",
                "0x37000-0x37FFF (len 0x1000)",
                MemoryCoverageFillRole.Neutral,
                "Protected customer information is supplied by DP BIN; TP overlay does not write here."),
            segment => AssertCoverageSegment(
                segment,
                "DP BIN",
                $"0x38000-0x{capacity - 1:X5} (len 0x{capacity - 0x38000:X})",
                MemoryCoverageFillRole.Dp,
                "Output range will be copied from DP BIN."));
        Assert.DoesNotContain(viewModel.Merge.MergeCoverageSegments, segment => segment.IsChanged);
        Assert.Contains(viewModel.Merge.MergeCoverageSegments, segment =>
            segment.ChangeLabel == "Will write");
    }

    /// <summary>Language changes reproject visible and assistive Memory information without semantic drift.</summary>
    [Fact]
    public void DpPerspectiveCoverageRelocalizesVisibleAndAccessibleDetails()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-memory-language");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.SetSlotFile(
            "merge-dp",
            workspace.Write("dp-40000.bin", new byte[0x40000]));
        string[] ranges = [.. viewModel.Merge.MergeCoverageSegments.Select(static segment => segment.RangeLabel)];
        MemoryCoverageFillRole[] fillRoles =
            [.. viewModel.Merge.MergeCoverageSegments.Select(static segment => segment.FillRole)];

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal(ranges, viewModel.Merge.MergeCoverageSegments.Select(static segment => segment.RangeLabel));
        Assert.Equal(fillRoles, viewModel.Merge.MergeCoverageSegments.Select(static segment => segment.FillRole));
        Assert.Collection(
            viewModel.Merge.MergeCoverageSegments,
            segment => AssertCoverageSegment(
                segment,
                "DP BIN",
                "0x00000-0x09FFF (len 0xA000)",
                MemoryCoverageFillRole.Dp,
                "輸出範圍將由 DP BIN 複製。"),
            segment => AssertCoverageSegment(
                segment,
                "TP BIN",
                "0x0A000-0x36FFF (len 0x2D000)",
                MemoryCoverageFillRole.Tp,
                "輸出範圍將由 TP BIN 覆寫。"),
            segment => AssertCoverageSegment(
                segment,
                "保留區",
                "0x37000-0x37FFF (len 0x1000)",
                MemoryCoverageFillRole.Neutral,
                "受保護的客戶資訊由 DP BIN 提供；TP 覆寫不會寫入此範圍。"),
            segment => AssertCoverageSegment(
                segment,
                "DP BIN",
                "0x38000-0x3FFFF (len 0x8000)",
                MemoryCoverageFillRole.Dp,
                "輸出範圍將由 DP BIN 複製。"));
        Assert.All(viewModel.Merge.MergeCoverageSegments, segment =>
        {
            Assert.DoesNotContain("Compiled operation", segment.Detail, StringComparison.Ordinal);
            Assert.DoesNotContain("Sequence", segment.Detail, StringComparison.Ordinal);
            Assert.DoesNotContain("Reason:", segment.Detail, StringComparison.Ordinal);
        });
        Assert.Contains(
            viewModel.Merge.MergeCoverageSegments,
            segment => segment.Detail.Contains("編譯操作", StringComparison.Ordinal) &&
                segment.Detail.Contains("順序", StringComparison.Ordinal) &&
                !segment.Detail.Contains("Reason:", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.Merge.MergeCoverageSegments, segment => segment.IsChanged);
        Assert.Contains(viewModel.Merge.MergeCoverageSegments, segment =>
            segment.ChangeLabel == "將寫入");

        ShellTextResources chinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        var unmapped = new MemoryMapRowViewModel(
            "0x00004-0x00007 (len 0x4)",
            new MemoryPlanSource(MemoryPlanSourceKind.NoOutput),
            MemoryPlanActionKind.Project,
            new MemoryPlanSource(MemoryPlanSourceKind.Unmapped),
            chinese.GetMemoryPlanDetail(MemoryPlanDetailKind.Unmapped),
            chinese);
        Assert.Equal("未對應", unmapped.AfterSource);
        Assert.Equal("投影", unmapped.ActionLabel);
        Assert.Equal("此實體範圍未指定來源。", unmapped.Detail);
        Assert.NotEqual(
            chinese.GetMemoryPlanSourceLabel(new MemoryPlanSource(MemoryPlanSourceKind.Reserved)),
            unmapped.AfterSource);

        string conflictDetail = chinese.FormatMemoryLayoutConflictDetail(
            ["mapping-1", "mapping-2"]);
        var conflict = new MemoryMapRowViewModel(
            "0x00002-0x00003 (len 0x2)",
            new MemoryPlanSource(MemoryPlanSourceKind.Output),
            MemoryPlanActionKind.Blocked,
            new MemoryPlanSource(MemoryPlanSourceKind.OverlapError),
            conflictDetail,
            chinese);
        Assert.Equal("輸出", conflict.BeforeSource);
        Assert.Equal("已阻擋", conflict.ActionLabel);
        Assert.Equal("範圍重疊錯誤", conflict.AfterSource);
        Assert.Contains("下列對應的輸出範圍重疊", conflict.Detail, StringComparison.Ordinal);
        Assert.Contains("mapping-1", conflict.Detail, StringComparison.Ordinal);
        Assert.Contains("mapping-2", conflict.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Mappings:", conflict.Detail, StringComparison.Ordinal);
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
        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
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
            row.ActionLabel == "Replace" && row.AfterSource == "Replacement DP BIN");
        Assert.Contains(viewModel.Replace.ReplaceMemoryRows, row =>
            row.ActionLabel == "Restore" && row.AfterSource == "Base flash");
        Assert.Collection(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => AssertCoverageSegment(
                segment,
                "Replacement DP BIN",
                "0x00000-0x09FFF (len 0xA000)",
                MemoryCoverageFillRole.Dp,
                "Output range will be written from DP BIN."),
            segment => AssertCoverageSegment(
                segment,
                "Base flash",
                "0x0A000-0x36FFF (len 0x2D000)",
                MemoryCoverageFillRole.Kept,
                "Output range will restore bytes from the base firmware."),
            segment =>
            {
                AssertCoverageSegment(
                    segment,
                    "Reserved",
                    "0x37000-0x37FFF (len 0x1000)",
                    MemoryCoverageFillRole.Neutral,
                    "Protected customer information is supplied by the DP replacement BIN; TP restore does not write here.");
                Assert.False(segment.UsesKeptPattern);
            },
            segment => AssertCoverageSegment(
                segment,
                "Replacement DP BIN",
                $"0x38000-0x{expectedLength - 1:X5} (len 0x{expectedLength - 0x38000:X})",
                MemoryCoverageFillRole.Dp,
                "Output range will be written from DP BIN."));

        viewModel.SelectedLanguage = "Traditional Chinese";

        MemoryCoverageSegmentViewModel protectedCustomerInformation = Assert.Single(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => segment.AddressRangeLabel == "0x37000-0x37FFF");
        Assert.Equal("保留區", protectedCustomerInformation.SourceLabel);
        Assert.Equal(MemoryCoverageFillRole.Neutral, protectedCustomerInformation.FillRole);
        Assert.False(protectedCustomerInformation.UsesKeptPattern);
        Assert.Equal(
            "受保護的客戶資訊由替換用 DP BIN 提供；TP 還原不會寫入此範圍。",
            protectedCustomerInformation.CompactDetail);
        MemoryMapRowViewModel protectedRow = Assert.Single(
            viewModel.Replace.ReplaceMemoryRows,
            row => row.RangeLabel.StartsWith("0x37000-0x37FFF", StringComparison.Ordinal));
        Assert.Equal("替換", protectedRow.ActionLabel);
        Assert.Equal("保留區", protectedRow.AfterSource);
        Assert.Contains(viewModel.Replace.ReplaceMemoryRows, row =>
            row.ActionLabel == "還原" && row.AfterSource == "基礎韌體");

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
        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, baseSlot.InputInspectionSeverity);
        string englishDiagnostic = baseSlot.InputInspectionStatus;
        Assert.Equal("Base BIN needs attention", viewModel.Replace.ReplaceMemoryRangeLabel);
        MemoryMapRowViewModel row = Assert.Single(viewModel.Replace.ReplaceMemoryRows);
        Assert.Equal(MemoryPlanActionKind.Blocked, row.Action);
        Assert.Equal(baseSlot.InputInspectionStatus, row.Detail);
        Assert.DoesNotContain("Load and inspect", row.Detail, StringComparison.Ordinal);
        MemoryCoverageSegmentViewModel segment = Assert.Single(viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal("Not available", segment.RangeLabel);
        Assert.Equal("Base BIN needs attention", segment.SourceLabel);
        Assert.Equal(baseSlot.InputInspectionStatus, segment.Detail);
        Assert.False(segment.IsChanged);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("Base BIN 需要處理", viewModel.Replace.ReplaceMemoryRangeLabel);
        row = Assert.Single(viewModel.Replace.ReplaceMemoryRows);
        segment = Assert.Single(viewModel.Replace.ReplaceCoverageSegments);
        Assert.NotEqual(englishDiagnostic, baseSlot.InputInspectionStatus);
        Assert.Equal(baseSlot.InputInspectionStatus, row.Detail);
        Assert.Equal(baseSlot.InputInspectionStatus, segment.Detail);
        Assert.DoesNotContain(englishDiagnostic, row.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("載入並檢查", row.Detail, StringComparison.Ordinal);
    }

    private static void AssertCoverageSegment(
        MemoryCoverageSegmentViewModel segment,
        string sourceLabel,
        string rangeLabel,
        MemoryCoverageFillRole fillRole,
        string compactDetail)
    {
        Assert.Equal(sourceLabel, segment.SourceLabel);
        Assert.Equal(rangeLabel, segment.RangeLabel);
        Assert.Equal(fillRole, segment.FillRole);
        Assert.Equal(compactDetail, segment.CompactDetail);
        Assert.Contains(compactDetail, segment.AccessibleDetail, StringComparison.Ordinal);
    }
}
