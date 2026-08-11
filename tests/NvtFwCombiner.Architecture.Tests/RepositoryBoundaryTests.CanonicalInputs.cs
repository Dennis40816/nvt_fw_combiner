namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class CanonicalCompositionBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Input slots and length policies are constructed once as Domain-owned definitions.</summary>
    [Fact]
    public void NormalizedInputsUseDomainCanonicalDefinitions()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        Assert.DoesNotContain(
            "internal sealed partial class CompositionProfileInputSlot",
            profiles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal abstract record CompositionProfileLengthRule",
            profiles,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ExactBytesLengthRule", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("ExactResolvedMapCapacityLengthRule", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("BoundedLengthRule", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceViewCoverageLengthRule", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("MapInputLengthRequirement", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledTpMaximum256KInputLengthRequirement", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledDeclaredPrefixWithWarningInputLengthRequirement", domain, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal sealed class CompositionProfileInputSelectionGroup",
            profiles,
            StringComparison.Ordinal);
        Assert.Contains("CompositionInputSlotDefinition", domain, StringComparison.Ordinal);
        Assert.Contains("InputLengthRequirementDefinition", domain, StringComparison.Ordinal);
        Assert.Contains("CompiledSourceViewCoverageInputLengthRequirement", domain, StringComparison.Ordinal);
        Assert.Contains("InputSelectionGroupDefinition", domain, StringComparison.Ordinal);
    }
}
