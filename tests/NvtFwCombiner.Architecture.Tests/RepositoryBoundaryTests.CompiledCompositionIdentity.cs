namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The compiled V2 details object is the sole retained compiled identity and policy form.</summary>
    [Fact]
    public void CompiledCompositionDoesNotCopyItsV2IdentityOrValidationState()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Domain",
            "Composition",
            "V2CompiledCompositionIdentity.cs")));
        Assert.DoesNotContain(
            "class V2CompiledCompositionIdentity",
            ReadText("src/NvtFwCombiner.Domain/Composition/V2CompiledCompositionDetails.cs"),
            StringComparison.Ordinal);

        string composition = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        Assert.DoesNotContain("ValidationRequirements =>", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "V2Details.Provenance.ValidationRequirements;",
            composition,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CopyValidationRequirements", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileId = details", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileId = source", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileVersion = details", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileVersion = source", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ExperienceId = details", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ExperienceId = source", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultOutputFileName = details", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultOutputFileName = source", composition, StringComparison.Ordinal);
    }
}
