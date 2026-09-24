using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Surviving Replace workflows and Standard DP perspective smoke coverage.</summary>
public sealed partial class ReplaceSurfaceCompatibilityTests
{
    /// <summary>General Replace retains readable coverage ranges through the shared display projection.</summary>
    [Fact]
    public void ReplaceCoverageUsesReadableInclusiveSegments()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-coverage");
        using var golden = StandardMergeGoldenManifest.Load();
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Replace.SelectedReplaceMode = Domain.Composition.ExperienceIds.GeneralReplace;
        viewModel.SetSlotFile("replace-base", golden.ExpectedOutputPath(golden.CaseByIc("51926")));
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, workspace.Write("replacement.bin", [0xA5, 0x5A]));

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
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment => segment.IsSelectedForWrite);
        Assert.True(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>Verifies NT51950 CtrlRAM Replace does not draw a max-length range before the base BIN is selected.</summary>
    [Fact]
    public void Nt51950CtrlRamCoverageWaitsForSelectedBaseLength()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

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
                "DP BIN",
                "0x37000-0x37FFF (len 0x1000)",
                MemoryCoverageFillRole.Neutral,
                "Supplied by DP BIN. TP overlay does not write here."),
            segment => AssertCoverageSegment(
                segment,
                "DP BIN",
                $"0x38000-0x{capacity - 1:X5} (len 0x{capacity - 0x38000:X})",
                MemoryCoverageFillRole.Dp,
                "Output range will be copied from DP BIN."));
        Assert.DoesNotContain(viewModel.Merge.MergeCoverageSegments, segment => segment.IsChanged);
        MemoryMapRowViewModel protectedPlan = Assert.Single(viewModel.Merge.MergeMemoryRows, row => row.RangeLabel.StartsWith("0x37000-0x37FFF", StringComparison.Ordinal));
        Assert.Equal("Initialization: 0x00", protectedPlan.BeforeSource);
        Assert.Equal("DP BIN", protectedPlan.AfterSource);
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
                "DP BIN",
                "0x37000-0x37FFF (len 0x1000)",
                MemoryCoverageFillRole.Neutral,
                "由 DP BIN 提供。TP 覆寫不會寫入此範圍。"),
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

        var chinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);
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

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        Assert.Equal(Domain.Composition.ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
        Assert.DoesNotContain("Initial Code + LDC", viewModel.Merge.MergeSlots.Single(
            slot => slot.SlotId == CompositionSlotIds.MergeDp).Description, StringComparison.Ordinal);
    }

    /// <summary>Verifies Replace Build validates the current file set without a separate manual Preview.</summary>
    [Fact]
    public async Task BuildReplaceValidatesCurrentInputsWithoutManualPreview()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-replace-gate");
        string basePath = workspace.Write("base.bin", ReadCtrlRamReference());
        string replacementPath = workspace.Write("replacement-dp.bin", ReadCtrlRamNormalSource());
        string replacementPath2 = workspace.PathFor("replacement-dp-copy.bin");
        string outputPath = workspace.PathFor("blocked-output.bin");
        File.Copy(replacementPath, replacementPath2);

        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile("replace-ctrlram-normal", replacementPath);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(viewModel.Replace.CanBuildReplace);

        viewModel.SetSlotFile("replace-ctrlram-normal", replacementPath2);

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

    /// <summary>CtrlRAM rejects an invalid required Base and relocalizes its terminal blocking diagnostic.</summary>
    [Fact]
    public async Task PreviewCtrlRamRejectsUnsupportedBaseAndRelocalizesDiagnostic()
    {
        using var workspace = TempWorkspace.Create("ctrlram-invalid-base");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Replace.SelectedReplaceMode = Domain.Composition.ExperienceIds.CtrlRamReplace;
        viewModel.SetSlotFile("replace-base", workspace.Write("base.bin", new byte[0x60000]));
        viewModel.SetSlotFile("replace-ctrlram-normal", workspace.Write("ctrlram.bin", new byte[0x1000]));

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.NotEmpty(viewModel.RunSession.LastRunResult.Detail);
        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, baseSlot.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Error, baseSlot.SemanticState);
        string englishDiagnostic = baseSlot.InputInspectionStatus;
        string englishLabel = baseSlot.SemanticStateLabel;
        Assert.NotEmpty(englishDiagnostic);
        Assert.False(viewModel.Replace.CaptureRunContext(Domain.Composition.ExperienceIds.CtrlRamReplace).IsPublicationCurrent);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, baseSlot.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Error, baseSlot.SemanticState);
        Assert.Equal(englishDiagnostic, baseSlot.InputInspectionStatus);
        Assert.NotEqual(englishLabel, baseSlot.SemanticStateLabel);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
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
