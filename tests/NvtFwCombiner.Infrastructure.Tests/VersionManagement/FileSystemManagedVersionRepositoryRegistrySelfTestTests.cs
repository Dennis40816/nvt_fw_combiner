using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>Real adapters admit a relocated complete root through ordered fallback without state mutation.</summary>
    [Fact]
    public async Task EnvironmentSelfTestUsesRelocatedCompleteRootAndSkipsDeprecated()
    {
        using var workspace = TempWorkspace.Create();
        string original = workspace.PathFor("original-source");
        string relocated = workspace.PathFor("relocated-source");
        string missingLatest = workspace.PathFor("missing-latest");
        string registryPath = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        string stateDirectory = Path.GetDirectoryName(statePath)!;
        string writerLockPath = FileSystemVersionManagerWriteLease.GetLockPath(statePath);
        string managedRoot = workspace.PathFor("managed-root");
        UpdateCatalogVersionSnapshot package = CreatePackage(original, "0.10.6");
        await WriteCatalogAsync(original, [package]);
        CopyDirectory(original, relocated);
        var registry = new UpdateSourceRegistryDocument(
            1,
            1,
            [
                new("latest", missingLatest),
                new("available", relocated),
                new("deprecated", original),
            ]);
        await File.WriteAllBytesAsync(
            registryPath,
            JsonSerializer.SerializeToUtf8Bytes(
                registry,
                CatalogJsonOptions),
            TestContext.Current.CancellationToken);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            managedRoot,
            new JsonVersionManagerStateStore(statePath),
            new FileSystemUpdateCatalogSource(),
            new FileSystemManagedVersionRepository(),
            new FileSystemUpdateSourceRegistry(registryPath));

        VersionEnvironmentSelfTestResult result = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Attempts.Count);
        Assert.Equal(missingLatest, result.Attempts[0].SourceRoot);
        Assert.Equal(UpdateCatalogLoadIssue.SourceMissing, result.Attempts[0].CatalogIssue);
        Assert.False(result.Attempts[0].IsVerified);
        Assert.Equal(relocated, result.Attempts[1].SourceRoot);
        Assert.Equal(ManagedAppVersion.Parse("0.10.6"), result.Attempts[1].NewestVersion);
        Assert.True(result.Attempts[1].IsVerified);
        Assert.DoesNotContain(result.Attempts, attempt =>
            string.Equals(attempt.SourceRoot, original, StringComparison.Ordinal));
        Assert.False(Directory.Exists(stateDirectory));
        Assert.False(File.Exists(statePath));
        Assert.False(File.Exists(writerLockPath));
        Assert.False(Directory.Exists(managedRoot));
    }
}
