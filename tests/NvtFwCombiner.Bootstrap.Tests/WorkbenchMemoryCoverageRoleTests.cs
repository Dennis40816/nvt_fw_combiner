namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks typed memory-coverage provenance at the Bootstrap projection boundary.</summary>
public sealed class WorkbenchMemoryCoverageRoleTests
{
    /// <summary>CtrlRAM coverage stays neutral until a typed source selection marks its physical regions.</summary>
    [Fact]
    public void CtrlRamReplaceDistinguishesBaseFirmwareFromReplacements()
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            "NT51926",
            "single",
            WorkbenchReplaceModes.CtrlRam);

        Assert.Contains(display.CoverageSegments, segment =>
            segment.Role == WorkbenchMemoryCoverageRole.BaseFirmware && !segment.IsChanged);
        Assert.Contains(display.CoverageSegments, segment =>
            segment.Role == WorkbenchMemoryCoverageRole.Standard && segment.RegionId is not null);
        Assert.DoesNotContain(display.CoverageSegments, segment => segment.IsChanged);

        string selectedRegionId = display.CoverageSegments
            .First(segment => segment.RegionId is not null)
            .RegionId!;
        WorkbenchMemoryDisplay selected = WorkbenchCompositionService.ApplyReplaceCoverageSelection(
            display,
            [selectedRegionId]);

        Assert.Contains(selected.CoverageSegments, segment =>
            segment.RegionId == selectedRegionId && segment.IsChanged);
        Assert.DoesNotContain(selected.CoverageSegments, segment =>
            segment.RegionId != selectedRegionId && segment.IsChanged);

        WorkbenchMemoryDisplay dpDisplay = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            "NT51951",
            "single",
            WorkbenchReplaceModes.Dp,
            dpBaseLength: 0x80000);
        WorkbenchMemoryDisplay unchangedDpDisplay = WorkbenchCompositionService.ApplyReplaceCoverageSelection(
            dpDisplay,
            []);
        Assert.Equal(dpDisplay.CoverageSegments, unchangedDpDisplay.CoverageSegments);
    }

    /// <summary>Masked routes expose one DiffDLM segment with canonical active Diff NF details.</summary>
    [Fact]
    public void PreserveActiveDiffNfCoverageUsesOnePrimaryDiffDlmSegment()
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            "NT51932",
            "4",
            WorkbenchReplaceModes.CtrlRam);

        WorkbenchMemoryCoverageSegment diffDlm = Assert.Single(
            display.CoverageSegments,
            static segment => segment.IsDiffDlm);
        Assert.Equal("DiffDLM", diffDlm.SourceLabel);
        Assert.Equal("0x2D100-0x30CFF (len 0x3C00)", diffDlm.RangeLabel);
        IReadOnlyList<Application.MemoryLayout.MemoryLayoutPreservationDetail> details =
            Assert.IsType<IReadOnlyList<Application.MemoryLayout.MemoryLayoutPreservationDetail>>(
                diffDlm.PreservationDetails,
                exactMatch: false);
        Assert.Equal(3, details.Count);
    }

    /// <summary>Full-artifact routes expose one DiffDLM segment without a kept-range disclosure.</summary>
    [Fact]
    public void FullArtifactReplaceCoverageHasNoPreservationPanelData()
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            "NT51926",
            "2",
            WorkbenchReplaceModes.CtrlRam);

        WorkbenchMemoryCoverageSegment diffDlm = Assert.Single(
            display.CoverageSegments,
            static segment => segment.IsDiffDlm);
        Assert.Equal("DiffDLM", diffDlm.SourceLabel);
        Assert.Empty(diffDlm.PreservationDetails ?? []);
    }

    /// <summary>DP Replace identifies both retained/restored base bytes without classifying replacements as base.</summary>
    [Fact]
    public void DpReplaceDistinguishesBaseFirmwareFromReplacementInputs()
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            "NT51951",
            "single",
            WorkbenchReplaceModes.Dp,
            dpBaseLength: 0x80000);

        Assert.Contains(display.CoverageSegments, segment =>
            segment.Role == WorkbenchMemoryCoverageRole.BaseFirmware && !segment.IsChanged);
        Assert.Contains(display.CoverageSegments, segment =>
            segment.Role == WorkbenchMemoryCoverageRole.Standard && segment.IsChanged);
        Assert.DoesNotContain(display.CoverageSegments, segment =>
            segment.Role == WorkbenchMemoryCoverageRole.BaseFirmware && segment.IsChanged);
    }

    /// <summary>Merge initializers and input mappings retain the standard role.</summary>
    [Fact]
    public void MergeCoverageDoesNotClaimBaseFirmwareProvenance()
    {
        WorkbenchMemoryDisplay standard = WorkbenchCompositionService.GetStandardMergeMemoryDisplay(
            "NT51926",
            dpInputLength: null);
        WorkbenchMemoryDisplay customized = GeneralTestDraftFactory.GetMergeDisplay(
            "NT51950",
            "0x100",
            []);

        Assert.NotEmpty(standard.CoverageSegments);
        Assert.NotEmpty(customized.CoverageSegments);
        Assert.All(standard.CoverageSegments, segment =>
            Assert.Equal(WorkbenchMemoryCoverageRole.Standard, segment.Role));
        Assert.All(customized.CoverageSegments, segment =>
            Assert.Equal(WorkbenchMemoryCoverageRole.Standard, segment.Role));
    }

    /// <summary>Every concrete coverage strip normalizes its segment widths to the shared 300-unit canvas.</summary>
    [Fact]
    public void ConcreteCoverageWidthsFillTheSharedCanvas()
    {
        WorkbenchMemoryDisplay[] displays =
        [
            WorkbenchCompositionService.GetReplaceMemoryDisplay(
                "NT51926",
                "single",
                WorkbenchReplaceModes.CtrlRam),
            WorkbenchCompositionService.GetStandardMergeMemoryDisplay("NT51926", dpInputLength: null),
            WorkbenchCompositionService.GetAbMergeMemoryDisplay("NT51929"),
            GeneralTestDraftFactory.GetMergeDisplay(
                "NT51950",
                "0x100",
                [WorkbenchCompositionService.CreateGeneralMergeAuthoringState(
                    "map-1", "input.bin", "0x0", "0x0", "0x10")]),
        ];

        Assert.All(displays, display => Assert.InRange(
            display.CoverageSegments.Sum(static segment => segment.BarWidth),
            299.999999,
            300.000001));
    }

    /// <summary>NT51950-family AB coverage follows the selected DP container and exposes only user inputs.</summary>
    [Theory]
    [InlineData(
        "NT51950",
        "single",
        0x80000L,
        "0x00000-0x7FFFF (len 0x80000)",
        "0x4A000-0x76FFF (len 0x2D000)",
        "Transform + Overlay + Postbuild")]
    [InlineData(
        "NT51950",
        "cascade",
        0x100000L,
        "0x00000-0xFFFFF (len 0x100000)",
        "0x4A000-0x76FFF (len 0x2D000)",
        "Transform + Overlay + Postbuild")]
    [InlineData(
        "NT51951",
        null,
        0x100000L,
        "0x00000-0xFFFFF (len 0x100000)",
        "0x8A000-0xB6FFF (len 0x2D000)",
        "Transform + Overlay + Postbuild")]
    public void Nt51950FamilyAbCoverageUsesCompiledDpCapacityAndOnlyFirmwareInputs(
        string icId,
        string? topologyToken,
        long dpInputLength,
        string expectedFullRange,
        string expectedTpBRange,
        string expectedTpBAction)
    {
        WorkbenchMemoryDisplay display =
            WorkbenchCompositionService.GetAbMergeMemoryDisplay(icId, topologyToken, dpInputLength);

        Assert.Equal(expectedFullRange, display.RangeLabel);
        Assert.Collection(
            display.MemoryMapRows,
            row =>
            {
                Assert.Equal(expectedFullRange, row.RangeLabel);
                Assert.Equal("DP AB", row.AfterSource);
            },
            row =>
            {
                Assert.Equal("0x0A000-0x36FFF (len 0x2D000)", row.RangeLabel);
                Assert.Equal("TPA", row.AfterSource);
            },
            row =>
            {
                Assert.Equal(expectedTpBRange, row.RangeLabel);
                Assert.Equal("TPB", row.AfterSource);
                Assert.Equal(expectedTpBAction, row.ActionLabel);
            });
        Assert.Equal(
            ["DP AB", "TPA", "TPB"],
            display.CoverageSegments.Select(static segment => segment.SourceLabel).Distinct(StringComparer.Ordinal));
        Assert.DoesNotContain(display.MemoryMapRows, static row =>
            row.Detail.Contains("CRC", StringComparison.OrdinalIgnoreCase) ||
            row.Detail.Contains("Allowed writes", StringComparison.OrdinalIgnoreCase) ||
            row.RangeLabel.Contains("Staging", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(display.CoverageSegments, segment => segment.Fill == "#CBD5E1");
        Assert.InRange(
            display.CoverageSegments.Sum(static segment => segment.BarWidth),
            299.999999,
            300.000001);
    }

    /// <summary>An unaccepted DP artifact length cannot invent an output layout the compiled profile cannot build.</summary>
    [Fact]
    public void Nt51950SingleAbCoverageFallsBackToCompiledCapacityForMismatchedDpLength()
    {
        WorkbenchMemoryDisplay display =
            WorkbenchCompositionService.GetAbMergeMemoryDisplay("NT51950", "single", dpInputLength: 0x90000);

        Assert.Equal("0x00000-0x7FFFF (len 0x80000)", display.RangeLabel);
        Assert.Contains(
            "does not match the compiled 0x80000 layout",
            display.MemoryMapRows[0].Detail,
            StringComparison.Ordinal);
    }

    /// <summary>Type A/B profiles expose TPB relocation without adding CRC or staging rows.</summary>
    [Theory]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void TypeAbCoverageShowsTpBTransformWithoutProcessorInternals(string icId)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetAbMergeMemoryDisplay(icId);

        Assert.Equal(["DP AB", "TPA", "TPB"], display.MemoryMapRows.Select(static row => row.AfterSource));
        Assert.Equal("Transform + Overlay", display.MemoryMapRows[2].ActionLabel);
        Assert.DoesNotContain(display.MemoryMapRows, static row =>
            row.Detail.Contains("CRC", StringComparison.OrdinalIgnoreCase) ||
            row.RangeLabel.Contains("Staging", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>An empty selected DP artifact is treated as absent until file inspection supplies a usable size.</summary>
    [Theory]
    [InlineData("NT51950", "single")]
    [InlineData("NT51951", null)]
    public void Nt51950FamilyAbCoverageTreatsZeroDpLengthAsAbsent(
        string icId,
        string? topologyToken)
    {
        WorkbenchMemoryDisplay omitted =
            WorkbenchCompositionService.GetAbMergeMemoryDisplay(icId, topologyToken);
        WorkbenchMemoryDisplay empty =
            WorkbenchCompositionService.GetAbMergeMemoryDisplay(icId, topologyToken, dpInputLength: 0);

        Assert.Equal(omitted.RangeLabel, empty.RangeLabel);
        Assert.Equal(omitted.MemoryMapRows, empty.MemoryMapRows);
        Assert.Equal(omitted.CoverageSegments, empty.CoverageSegments);
    }

    /// <summary>Negative file lengths remain invalid programmer input.</summary>
    [Fact]
    public void Nt51950FamilyAbCoverageRejectsNegativeDpLength()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkbenchCompositionService.GetAbMergeMemoryDisplay("NT51950", "single", dpInputLength: -1));
    }
}
