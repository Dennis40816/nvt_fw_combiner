namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Catalog client disclosure is Application-owned and Bootstrap only registers it.</summary>
    [Fact]
    public void LarCatalogProjectionIsRetiredFromBootstrap()
    {
        string bootstrapDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        string host = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string applicationExperience = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityExperience.cs");

        Assert.Empty(Directory.EnumerateFiles(
            bootstrapDirectory,
            "CanonicalCapabilityProjection*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.Contains(
            "new CanonicalCapabilityExperience(",
            host,
            StringComparison.Ordinal);
        Assert.Contains(
            "sealed class CanonicalCapabilityExperience : ICompositionCapabilityExperience",
            applicationExperience,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NvtFwCombiner.Profiles",
            applicationExperience,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryCompile",
            applicationExperience,
            StringComparison.Ordinal);
    }
}
