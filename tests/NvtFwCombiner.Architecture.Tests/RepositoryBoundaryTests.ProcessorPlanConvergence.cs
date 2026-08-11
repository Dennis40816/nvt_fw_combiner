namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The external-tool adapter executes the compiled processor plan and never selects a run plan again.</summary>
    [Fact]
    public void ExternalProcessorAdapterDoesNotReconstructCompiledPlans()
    {
        string processor = File.ReadAllText(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "ExternalTools",
            "LegacyCombinerPostbuildProcessor.cs"));
        string router = File.ReadAllText(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "ExternalTools",
            "ExternalProcessorRouter.cs"));

        Assert.DoesNotContain("LegacyCombinerPostbuildPlanCompiler.CreatePlan", processor, StringComparison.Ordinal);
        Assert.DoesNotContain("_profilesByProcessorId", processor, StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorProtocolPlan? plan = request.ProtocolPlan", router, StringComparison.Ordinal);
        Assert.Contains("external-processor.protocol.unsupported", router, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyPostbuildProcessorIds", router, StringComparison.Ordinal);
        Assert.DoesNotContain("request.ProcessorId", router, StringComparison.Ordinal);
    }
}
