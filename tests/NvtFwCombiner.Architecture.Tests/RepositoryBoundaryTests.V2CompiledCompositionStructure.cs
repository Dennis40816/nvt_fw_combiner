namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies the retired pre-bundle compiler cannot remain as an alternate runtime authority.</summary>
    [Fact]
    public void LegacyCompilerRuntimeSurfaceIsRetired()
    {
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

        Assert.DoesNotContain("LegacyCompiledCompositionIdentity", productionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyProfileCompilationAuthority", productionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyRuntimeExecutable", productionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.CreateLegacy(", productionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("nfc.compiled-composition.legacy", productionSources, StringComparison.Ordinal);
    }

    /// <summary>Verifies production composition creation is owned only by trusted V2 compilers.</summary>
    [Fact]
    public void ProductionCompiledCompositionCreationStaysV2Owned()
    {
        string composition = ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string profileSources = ReadProfileSources();
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
        Assert.Contains("public V2CompiledCompositionDetails V2Details", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionCompilationAuthority", productionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileCompileResult", bootstrapSources, StringComparison.Ordinal);
    }
}
