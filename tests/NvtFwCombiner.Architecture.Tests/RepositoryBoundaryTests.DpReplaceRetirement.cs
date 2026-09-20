namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The obsolete DP slot adapter remains absent after retirement.</summary>
    [Fact]
    public void DpReplaceLegacySlotAdapterRemainsRetired()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "DpReplaceInputSlotProjection.cs")));
        Assert.DoesNotContain("DpReplaceInputSlotProjection", ReadProductionSources(), StringComparison.Ordinal);
    }

    /// <summary>Retired DP execution has no typed port, session, registration or CLI handler.</summary>
    [Fact]
    public void RetiredDpExecutionHasNoParallelRuntimeAuthority()
    {
        string production = ReadProductionSources();
        foreach (string retired in new[]
        {
            "IDpReplaceAuthoring", "DpReplaceAuthoringExperience", "AcceptedDpExecutionPlan",
            "TryCompileDpReplace", "CompileDynamicDefinition", "RunDpReplaceAsync", "_dpReplaceSession",
            "GetDpReferenceCapacities", "DpReplaceAddressSpaceId", "DpReplaceByIc",
        })
        {
            Assert.DoesNotContain(retired, production, StringComparison.Ordinal);
        }
        Assert.Equal(1, CountOccurrences(production, "profile.v2.plan.retired-experience"));
        string compiler = ReadText("src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");
        Assert.Contains("ExperienceIds.DpReplace", compiler, StringComparison.Ordinal);
        Assert.Contains("profile.v2.plan.retired-experience", compiler, StringComparison.Ordinal);
        Assert.Contains("CompositionEngine", ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.ExternalProcessors.cs"), StringComparison.Ordinal);
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
