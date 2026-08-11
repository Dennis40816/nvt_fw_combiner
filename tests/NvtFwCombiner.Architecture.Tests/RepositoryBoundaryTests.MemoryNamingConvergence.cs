namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RetirementBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Memory and naming stay owned by their Application projections without Bootstrap recomputation.</summary>
    [Fact]
    public void MemoryAndNamingHaveOneApplicationOwnedProjectionPath()
    {
        string bootstrap = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        string[] retiredFiles =
        [
            Path.Combine(bootstrap, "CompositionOutputNaming.cs"),
            Path.Combine(bootstrap, "CompositionOutputNaming.AbMerge.cs"),
            Path.Combine(bootstrap, "CompositionMemoryProjection.Replace.cs"),
            Path.Combine(bootstrap, "CompositionMemoryProjection.Replace.Coverage.cs"),
            Path.Combine(bootstrap, "WorkbenchMemoryDisplayProjection.cs"),
        ];

        Assert.DoesNotContain(retiredFiles, File.Exists);
        AssertNoProductionText("WorkbenchMemoryDisplay");
        AssertNoProductionText("WorkbenchMemoryMapRow");
        AssertNoProductionText("WorkbenchMemoryCoverageSegment");
        AssertNoProductionText("OutputNamePathCandidate");
        AssertNoProductionText("OutputNameInspectionCandidate");
        AssertNoProductionText("OutputFileNameSuggestion");
        AssertNoProductionText("CompositionOutputNaming.Create");
        string resolver = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedSessionOutputNameResolver.cs");
        string compiled = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompiledOutputNameResolver.cs");
        string useCase = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionOutputNamingExperience.cs");
        string ports = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        Assert.Contains("CompiledOutputNameResolver.Resolve", resolver, StringComparison.Ordinal);
        Assert.Contains("accepted-ctrlram-version-draft", compiled, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionOutputNameResolver.Resolve", useCase, StringComparison.Ordinal);
        Assert.Contains(
            "CapabilityPublicationCoherence.GetAcceptedAbMergeTopologySelection",
            useCase,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCapabilityCompilerAdapter", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveAbMergeTopologySelection", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("abMergeTopologyToken", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("abMergeTopologyToken", ports, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            bootstrap,
            "CompositionExperienceAdapters.cs")));
    }
}
