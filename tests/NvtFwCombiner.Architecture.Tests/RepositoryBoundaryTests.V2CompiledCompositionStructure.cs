namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies production composition creation is owned only by trusted V2 compilers.</summary>
    [Fact]
    public void ProductionCompiledCompositionCreationStaysV2Owned()
    {
        string composition = ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string profileSources = ReadProfileSources();
        string request = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunRequest.cs");
        string bootstrapSources = ReadBootstrapSources();
        string sourceRoot = Path.Combine(Root.FullName, "src");
        string productionSources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("CompositionProfileCompiler", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.CreateLegacy(", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.CreateLegacy(", productionSources, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition.CreateV2(", profileSources, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition.CreateV2RuntimeExecutable(", profileSources, StringComparison.Ordinal);
        Assert.Contains("internal static CompiledComposition CreateV2", composition, StringComparison.Ordinal);
        Assert.Contains("internal static CompiledComposition CreateV2RuntimeExecutable", composition, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleV2CompilationAuthority", request, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileCompileResult", bootstrapSources, StringComparison.Ordinal);
    }
}
