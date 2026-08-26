namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Registry transport, selection policy, and filesystem access remain layered.</summary>
    [Fact]
    public void VersionRegistryKeepsTransportPolicyAndFilesystemInTheirOwningLayers()
    {
        string contract = ReadText(
            "src/NvtFwCombiner.Contracts/VersionManagement/UpdateSourceRegistryDocument.cs");
        string application = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/UpdateSourceRegistry.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.cs");
        string infrastructure = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateSourceRegistry.cs");
        string bootstrap = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");

        Assert.Contains("UpdateSourceRegistryDocument", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", contract, StringComparison.Ordinal);
        Assert.Contains("IUpdateSourceRegistry", application, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryEntryStatus.Latest", application, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryEntryStatus.Available", application, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSourceRegistryEntryStatus.Deprecated =>", application, StringComparison.Ordinal);
        Assert.Contains("FileSystemUpdateSourceRegistry", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("IManagedVersionRepository", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("IUpdateCatalogSource", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionManagerState", infrastructure, StringComparison.Ordinal);
        Assert.Contains("new FileSystemUpdateSourceRegistry", bootstrap, StringComparison.Ordinal);
    }
}
