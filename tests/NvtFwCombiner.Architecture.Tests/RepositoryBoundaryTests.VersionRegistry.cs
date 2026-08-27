namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Registry transports share one parser while selection policy stays in Application.</summary>
    [Fact]
    public void VersionRegistryKeepsTransportPolicyAndFilesystemInTheirOwningLayers()
    {
        string contract = ReadText(
            "src/NvtFwCombiner.Contracts/VersionManagement/UpdateSourceRegistryDocument.cs");
        string application = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/UpdateSourceRegistry.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.cs");
        string infrastructure = string.Concat(
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateSourceRegistry.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/HttpUpdateSourceRegistry.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/UpdateSourceRegistryDocumentParser.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/UpdateSourceRegistryAdapterFactory.cs"));
        string bootstrap = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");

        Assert.Contains("UpdateSourceRegistryDocument", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", contract, StringComparison.Ordinal);
        Assert.Contains("IUpdateSourceRegistry", application, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryEntryStatus.Latest", application, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryEntryStatus.Available", application, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSourceRegistryEntryStatus.Deprecated =>", application, StringComparison.Ordinal);
        Assert.Contains("FileSystemUpdateSourceRegistry", infrastructure, StringComparison.Ordinal);
        Assert.Contains("HttpUpdateSourceRegistry", infrastructure, StringComparison.Ordinal);
        Assert.Equal(
            2,
            CountOccurrences(infrastructure, "UpdateSourceRegistryDocumentParser.Parse(bytes)"));
        Assert.DoesNotContain("IManagedVersionRepository", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("IUpdateCatalogSource", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionManagerState", infrastructure, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryAdapterFactory.Create", bootstrap, StringComparison.Ordinal);
    }

    /// <summary>Filesystem Registry and Catalog adapters consume one path-admission owner.</summary>
    [Fact]
    public void VersionRegistryAndCatalogReuseManagedPathSafety()
    {
        string owner = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedPathSafety.cs");
        string[] consumers =
        [
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateSourceRegistry.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/UpdateSourceRegistryDocumentParser.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateCatalogSource.cs"),
        ];

        Assert.Contains("TryNormalizeExactAbsolutePath", owner, StringComparison.Ordinal);
        Assert.Contains("HasReparseComponent", owner, StringComparison.Ordinal);
        Assert.Contains("PathComparer", owner, StringComparison.Ordinal);
        foreach (string consumer in consumers)
        {
            Assert.Contains("ManagedPathSafety.", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain("IsDeviceExtendedOrAlternateStream", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain("private static bool HasReparseComponent", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain("private static StringComparer PathComparer", consumer, StringComparison.Ordinal);
        }
    }
}
