namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ApplicationBoundaryTests
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
            "LegacyCombinerProcessorAdapter.Postbuild.cs"));
        string host = File.ReadAllText(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "ExternalTools",
            "StagedExternalProcessorHost.cs"));
        string adapter = File.ReadAllText(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "ExternalTools",
            "LegacyCombinerProcessorAdapter.cs"));

        Assert.DoesNotContain("LegacyCombinerPostbuildPlanCompiler.CreatePlan", processor, StringComparison.Ordinal);
        Assert.DoesNotContain("_profilesByProcessorId", processor, StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorProtocolPlan? plan = request.ProtocolPlan", adapter, StringComparison.Ordinal);
        Assert.Contains("LegacyCombinerPostbuildPlanCompiler.ProtocolId", adapter, StringComparison.Ordinal);
        Assert.Contains("adapter.ValidateRequest(request)", host, StringComparison.Ordinal);
        Assert.Contains("external-processor.protocol.unsupported", host, StringComparison.Ordinal);
        Assert.Contains("external-processor.protocol.unsupported", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyPostbuildProcessorIds", host + adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("request.ProcessorId", host + adapter, StringComparison.Ordinal);
    }
}
