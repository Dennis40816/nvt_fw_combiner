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
        string runAdapter = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompiledCompositionRunAdapter.cs");
        string runner = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");
        string bootstrapWithoutAdapter = ReadBootstrapSources()
            .Replace(runAdapter, string.Empty, StringComparison.Ordinal);

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
        Assert.Contains("CompiledComposition compiledComposition", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition profile,", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("compile.Plan", bootstrapWithoutAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompositionRunProfile(", bootstrapWithoutAdapter, StringComparison.Ordinal);
    }
}
