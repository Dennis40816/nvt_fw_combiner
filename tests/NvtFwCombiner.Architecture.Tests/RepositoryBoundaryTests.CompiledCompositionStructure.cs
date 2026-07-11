namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies only the profile compiler can mint executable legacy artifacts in production code.</summary>
    [Fact]
    public void ProductionCompiledCompositionCreationStaysProfileCompilerOwned()
    {
        string project = ReadText("src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj");
        string composition = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string compiler = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.cs");
        string profileSources = ReadProfileSources();
        string request = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunRequest.cs");
        string previewTokens = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.PreviewTokens.cs");
        string runner = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");
        string bootstrapSources = ReadBootstrapSources();

        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Profiles\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Application.Tests\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Domain.Tests\" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Application\" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Bootstrap\" />",
            project,
            StringComparison.Ordinal);
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
        Assert.Contains("CompiledComposition compiledComposition", request, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunProfile", request, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionPlan plan", request, StringComparison.Ordinal);
        Assert.Contains(
            "request.CompiledComposition.CompilationFingerprint",
            previewTokens,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AppendPlanFingerprint", previewTokens, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition compiledComposition", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition profile,", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("compile.Plan", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCompositionRunAdapter", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunProfile", bootstrapSources, StringComparison.Ordinal);
    }
}
