using System.Text.Json;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>A reachable locator containing a sign-in page is not a verified Registry document.</summary>
    [Fact]
    public async Task EnvironmentSelfTestRejectsLoginHtmlAtExistingRegistryLocator()
    {
        using var workspace = TempWorkspace.Create();
        string registryPath = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await File.WriteAllTextAsync(
            registryPath,
            "<html><head><title>Sign in to your account</title></head><body>Microsoft 365</body></html>",
            TestContext.Current.CancellationToken);
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        string launcherStatePath = workspace.PathFor("state/launcher-bootstrap.v1.json");
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            workspace.PathFor("managed-root"),
            new JsonVersionManagerStateStore(statePath),
            new FileSystemUpdateCatalogSource(),
            new FileSystemManagedVersionRepository(),
            new JsonLauncherMutationFence(launcherStatePath),
            new FileSystemUpdateSourceRegistry(registryPath));

        VersionEnvironmentSelfTestResult result = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.RegistryIssue);
        Assert.Empty(result.Attempts);
        Assert.False(File.Exists(statePath));
    }

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
        string launcherStatePath = workspace.PathFor("state/launcher-bootstrap.v1.json");
        string stateDirectory = Path.GetDirectoryName(statePath)!;
        string writerLockPath = FileSystemVersionManagerWriteLease.GetLockPath(statePath);
        string managedRoot = workspace.PathFor("managed-root");
        UpdateCatalogVersionSnapshot package = CreatePackage(original, "0.10.6");
        await WriteCatalogAsync(original, [package]);
        CopyDirectory(original, relocated);
        string catalogSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Path.Combine(relocated, FileSystemUpdateCatalogSource.CatalogFileName)))).ToLowerInvariant();
        var registry = new UpdateSourceRegistryDocument(
            1,
            "nvt-fw-combiner-production",
            1,
            new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            new("0.10.6", 1, catalogSha256),
            [
                new("latest", Path.Combine(missingLatest, FileSystemUpdateCatalogSource.CatalogFileName)),
                new("available", Path.Combine(relocated, FileSystemUpdateCatalogSource.CatalogFileName)),
                new("deprecated", Path.Combine(original, FileSystemUpdateCatalogSource.CatalogFileName)),
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
            new JsonLauncherMutationFence(launcherStatePath),
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
