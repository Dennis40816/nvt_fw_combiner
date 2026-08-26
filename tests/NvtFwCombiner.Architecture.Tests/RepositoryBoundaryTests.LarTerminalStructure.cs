namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Bootstrap remains an explicit composition root with no workflow-semantic source files.</summary>
    [Fact]
    public void LegacyRetirementLeavesBootstrapWithOnlyWiringAndLifetimeFiles()
    {
        string bootstrapRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        string[] actual =
        [
            .. Directory.EnumerateFiles(bootstrapRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(static path => Path.GetFileName(path))
                .Order(StringComparer.Ordinal),
        ];
        string[] expected =
        [
            "ApplicationCompositionGlobalUsings.cs",
            "CompositionHostServices.cs",
            "UpdateSourceRegistryLocator.cs",
        ];

        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            actual);
    }

    /// <summary>Focused capability disclosure does not retain test-only workflow aliases.</summary>
    [Fact]
    public void LegacyRetirementDeletesTestOnlyCapabilityAliases()
    {
        AssertNoProductionText("HasBuiltInV2DpReplace");
    }
}
