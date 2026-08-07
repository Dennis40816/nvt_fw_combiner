namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Authoring and compiled artifacts share one IC-number mode vocabulary.</summary>
    [Fact]
    public void CompiledArtifactsReuseCanonicalIcNumberInputMode()
    {
        string domain = ReadDomainSources();
        string profiles = ReadProfileSources();
        string details = ReadText(
            "src/NvtFwCombiner.Domain/Composition/V2CompiledCompositionDetails.cs");

        Assert.DoesNotContain("enum CompiledIcNumberPolicy", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("class CompiledIcNumberPolicies", profiles, StringComparison.Ordinal);
        Assert.Contains("public IcNumberInputMode? IcNumberInputMode", details, StringComparison.Ordinal);
    }
}
