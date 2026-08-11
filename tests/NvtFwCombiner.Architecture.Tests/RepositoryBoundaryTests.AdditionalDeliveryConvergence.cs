namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ApplicationBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Compiled profiles and Application Build are the only AB additional-delivery semantic owners.</summary>
    [Fact]
    public void AdditionalDeliveryUsesCompiledApplicationBuildPath()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "AbMergeAFlashCodeExportService.cs")));

        string production = ReadProductionSources();
        string compiler = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");
        string runService = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string abAdapter = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");

        Assert.DoesNotContain("AbMergeAFlashCodeExportService", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchAbAFlashCodeDeliveryPlan", production, StringComparison.Ordinal);
        Assert.Contains("new CompiledAdditionalDelivery(", compiler, StringComparison.Ordinal);
        Assert.Contains("dp-a-before-cmi", compiler, StringComparison.Ordinal);
        Assert.Contains("ICompositionDeliveryWriter", runService, StringComparison.Ordinal);
        Assert.Contains("SliceDeliveryBytes", runService, StringComparison.Ordinal);
        Assert.Contains("DeliveryArtifactSummary", runService, StringComparison.Ordinal);
        Assert.DoesNotContain("dp-a-before-cmi", abAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("a-cmi-dp-version", abAdapter, StringComparison.Ordinal);
    }
}
