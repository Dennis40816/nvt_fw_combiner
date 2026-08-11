namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class CanonicalCompositionBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Normalized validations reuse Domain definitions and retain only unresolved source-view state.</summary>
    [Fact]
    public void NormalizedValidationsUseDomainCanonicalDefinitions()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2",
            "CompositionProfileValidation.cs")));
        Assert.DoesNotContain("abstract record CompositionProfileValidation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataValueProfileValidation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewByteAssertionProfileValidation", profiles, StringComparison.Ordinal);
        Assert.Contains("ValidationRequirementDefinition", domain, StringComparison.Ordinal);
        Assert.Contains("SourceViewNonUniformValidationDefinition", domain, StringComparison.Ordinal);
    }
}
