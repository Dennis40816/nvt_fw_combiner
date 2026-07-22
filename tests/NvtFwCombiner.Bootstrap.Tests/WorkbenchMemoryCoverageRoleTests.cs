namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks typed memory-coverage provenance at the Bootstrap projection boundary.</summary>
public sealed class WorkbenchMemoryCoverageRoleTests
{
    /// <summary>CtrlRAM retained base bytes are typed independently from changed replacement bytes.</summary>
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
            segment.Role == WorkbenchMemoryCoverageRole.Standard && segment.IsChanged);
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
}
