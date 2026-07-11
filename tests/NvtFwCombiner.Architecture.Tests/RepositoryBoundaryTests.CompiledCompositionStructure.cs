namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies only the profile compiler assembly can mint executable legacy artifacts.</summary>
    [Fact]
    public void LegacyCompiledCompositionCreationStaysProfileCompilerOwned()
    {
        string project = ReadText("src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj");
        string composition = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string compiler = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.cs");
        string profileSources = ReadProfileSources();

        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Profiles\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Domain.Tests\" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", project, StringComparison.Ordinal);
        Assert.Contains(
            "internal static CompiledComposition CreateLegacy",
            composition,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public static CompiledComposition Create",
            composition,
            StringComparison.Ordinal);
        Assert.Contains("CompiledComposition.CreateLegacy(", compiler, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(profileSources, "CompiledComposition.CreateLegacy("));
    }
}
