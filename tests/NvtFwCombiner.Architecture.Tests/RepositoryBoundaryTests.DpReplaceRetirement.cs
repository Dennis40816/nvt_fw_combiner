namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>DP Replace slot identity is normalized by the shared Application authoring use case.</summary>
    [Fact]
    public void DpReplaceUsesTheSharedCompiledAuthoringPath()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "DpReplaceInputSlotProjection.cs")));
        Assert.DoesNotContain("DpReplaceInputSlotProjection", ReadProductionSources(), StringComparison.Ordinal);
    }

    /// <summary>AB Merge input requirements come from the shared compiled authoring contract.</summary>
    [Fact]
    public void AbMergeUsesTheSharedCompiledAuthoringPath()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchAbMergeInputProjection.cs")));
        Assert.DoesNotContain("WorkbenchAbMergeInputProjection", ReadProductionSources(), StringComparison.Ordinal);
    }

    /// <summary>CtrlRAM firmware-version authoring uses the Application draft contract directly.</summary>
    [Fact]
    public void CtrlRamVersionAuthoringHasOneTypedContract()
    {
        Assert.DoesNotContain(
            "WorkbenchCtrlRamFirmwareVersionEdit",
            ReadProductionSources(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WorkbenchCtrlRamAuthoringTransitionResult",
            ReadProductionSources(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WorkbenchSlotIds",
            ReadProductionSources(),
            StringComparison.Ordinal);
    }
}
