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
                "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.FreshInstallation.cs");
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

    /// <summary>Fresh setup promotion and recovery never regress to path-based tree mutation.</summary>
    [Fact]
    public void ManagedSetupUsesHandleCustodyInsteadOfPathMoveOrRecursiveDelete()
    {
        string implementation = string.Concat(
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.Helpers.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.Transaction.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.Native.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsManagedSetupPathCustody.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsManagedSetupPathCustody.Native.cs"));

        Assert.DoesNotContain("Directory.Move(", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain("recursive: true", implementation, StringComparison.Ordinal);
        Assert.Contains("NtCreateFile", implementation, StringComparison.Ordinal);
        Assert.Contains("NtSetInformationFile", implementation, StringComparison.Ordinal);
        Assert.Contains("RevalidateClosedTree", implementation, StringComparison.Ordinal);

        string setupCustody = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsManagedSetupPathCustody.cs");
        Assert.Contains(
            "TryCaptureImmutableTreeFromHeldDirectory",
            setupCustody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryAcquireImmutableTree(",
            setupCustody,
            StringComparison.Ordinal);
    }

    /// <summary>Setup and ordinary install retain one package semantic owner.</summary>
    [Fact]
    public void ManagedSetupReusesTheSingleManagedPackageVerifier()
    {
        string sourceRoot = Path.Combine(Root.FullName, "src");
        string[] declarations =
        [
            .. Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "internal static class ManagedPackageVerifier",
                    StringComparison.Ordinal)),
        ];
        string repository = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.Installation.cs");
        string materializer = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.cs");

        _ = Assert.Single(declarations);
        Assert.Contains("ManagedPackageVerifier.CreatePlanAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ManagedPackageVerifier.ExtractAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ManagedPackageVerifier.VerifyInstalledAsync", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagedPackageVerifier", materializer, StringComparison.Ordinal);
    }
}
