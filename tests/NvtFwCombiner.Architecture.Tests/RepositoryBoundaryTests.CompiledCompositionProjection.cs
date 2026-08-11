namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Compiled consumers use the canonical V2 details graph without a parallel facade.</summary>
    [Fact]
    public void CompiledCompositionDoesNotRepeatV2DetailProjections()
    {
        string source = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");

        Assert.DoesNotContain("public string ProfileId =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public string ProfileVersion =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public string IcId =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public string ModeId =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public string ExperienceId =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public CompositionKind CompositionKind =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public string DefaultOutputFileName =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public IReadOnlyList<CompiledValidationRequirement> ValidationRequirements =>", source, StringComparison.Ordinal);
    }
}
