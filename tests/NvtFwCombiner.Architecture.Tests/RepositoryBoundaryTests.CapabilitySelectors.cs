namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Application transports the typed workflow choice and never reverse-parses route axes.</summary>
    [Fact]
    public void WorkflowNumberChoiceHasOneTypedCanonicalProducer()
    {
        string producer = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs");
        string transport = string.Concat(
            ReadText("src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs"),
            ReadText("src/NvtFwCombiner.Application/Capabilities/CanonicalDynamicCapabilityModels.cs"),
            ReadText("src/NvtFwCombiner.Application/Capabilities/CapabilitySelectorPublication.cs"));
        string validator = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/WorkflowIcNumberChoiceProjection.cs");

        Assert.Contains("ProjectGeneralReplaceNumberChoice", producer, StringComparison.Ordinal);
        Assert.Contains("CapabilityNumberChoice? NumberChoice", transport, StringComparison.Ordinal);
        Assert.Contains("route.NumberChoice", transport, StringComparison.Ordinal);
        Assert.DoesNotContain("IcCountVariant", validator, StringComparison.Ordinal);
        Assert.DoesNotContain("\"1-ic\"", validator, StringComparison.Ordinal);
        Assert.DoesNotContain("\"2-8-ic\"", validator, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseExactCount", validator, StringComparison.Ordinal);
    }
}
