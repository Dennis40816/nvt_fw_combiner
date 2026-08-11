namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private static void AssertBootstrapTestsDoNotMutateTheSharedCatalogPublication()
    {
        string directory = Path.Combine(
            Root.FullName,
            "tests",
            "NvtFwCombiner.Bootstrap.Tests");
        string sources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain(
            "[Collection(CanonicalCapabilityCatalogPublicationGroup.Name)]",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DisableParallelization",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Canonical.Catalog.Reload(",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Services.Catalog",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Services.WarmCanonicalCapabilities(",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Services.CreateSystemInformationService(",
            sources,
            StringComparison.Ordinal);
    }
}
