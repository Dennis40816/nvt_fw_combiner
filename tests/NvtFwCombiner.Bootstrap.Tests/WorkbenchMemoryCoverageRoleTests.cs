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
        WorkbenchMemoryDisplay customized = WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
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
            WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
                "0x100",
                [new WorkbenchGeneralMergeMappingInput("map-1", "input.bin", "0x0", "0x0", "0x10")]),
        ];

        Assert.All(displays, display => Assert.InRange(
            display.CoverageSegments.Sum(static segment => segment.BarWidth),
            299.999999,
            300.000001));
    }

    /// <summary>AB postbuild rows distinguish full-image staging from the exact TPB write authority.</summary>
    [Fact]
    public void Nt51950AbPostbuildDisplayShowsStagingScopeAndAllowedWrites()
    {
        WorkbenchMemoryDisplay display =
            WorkbenchCompositionService.GetAbMergeMemoryDisplay("NT51950", "single");

        WorkbenchMemoryMapRow postbuild = Assert.Single(
            display.MemoryMapRows,
            static row => row.ActionLabel == "Postbuild");

        Assert.StartsWith("Staging/read scope: Postbuild AB work ", postbuild.RangeLabel, StringComparison.Ordinal);
        Assert.Contains(
            "Allowed writes: 0x4A100-0x4A103 (len 0x4), 0x4A110-0x4A113 (len 0x4), " +
            "0x4A130-0x4A133 (len 0x4).",
            postbuild.Detail,
            StringComparison.Ordinal);
    }

    /// <summary>NT51950-family AB output banks use typed work-buffer colors instead of the unknown gray fallback.</summary>
    [Theory]
    [InlineData("NT51950", "single")]
    [InlineData("NT51951", null)]
    public void Nt51950FamilyAbCoverageColorsCompiledWorkBuffers(string icId, string? topologyToken)
    {
        WorkbenchMemoryDisplay display =
            WorkbenchCompositionService.GetAbMergeMemoryDisplay(icId, topologyToken);

        Assert.Contains(display.CoverageSegments, segment =>
            segment.SourceLabel == "A bank work" && segment.Fill == "#16A34A");
        Assert.Contains(display.CoverageSegments, segment =>
            segment.SourceLabel == "Postbuild AB work" && segment.Fill == "#7C3AED");
        Assert.DoesNotContain(display.CoverageSegments, segment => segment.Fill == "#CBD5E1");
    }
}
