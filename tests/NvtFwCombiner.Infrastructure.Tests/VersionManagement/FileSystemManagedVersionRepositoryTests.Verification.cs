using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies release-manifest compatibility and launchability projections.</summary>
public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>The managed verifier consumes the same roles and payload families emitted by the production packager.</summary>
    [Fact]
    public async Task ProductionReleaseManifestRolesAreAdmittedByManagedVerifier()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            includeProductionContractPayload: true);

        ManagedPackageVerificationResult verified = await new FileSystemManagedVersionRepository()
            .VerifyPackageAsync(sourceRoot, package, TestContext.Current.CancellationToken);

        Assert.True(verified.IsVerified, verified.Issue.ToString());
    }

    /// <summary>Verification preserves the launchability distinction between historical and managed packages.</summary>
    [Fact]
    public async Task PackageVerificationReportsManagedLauncherAdmissionSeparately()
    {
        using var workspace = TempWorkspace.Create();
        string legacyRoot = workspace.PathFor("legacy-source");
        string managedRoot = workspace.PathFor("managed-source");
        var repository = new FileSystemManagedVersionRepository();

        ManagedPackageVerificationResult legacy = await repository.VerifyPackageAsync(
            legacyRoot,
            CreatePackage(legacyRoot, "1.0.4"),
            TestContext.Current.CancellationToken);
        ManagedPackageVerificationResult managed = await repository.VerifyPackageAsync(
            managedRoot,
            CreatePackage(managedRoot, "1.0.4", includeManagedLauncher: true),
            TestContext.Current.CancellationToken);

        Assert.True(legacy.IsVerified, legacy.Issue.ToString());
        Assert.False(legacy.HasSupportedManagedLauncher);
        Assert.True(managed.IsVerified, managed.Issue.ToString());
        Assert.True(managed.HasSupportedManagedLauncher);
    }

    /// <summary>A manual-only release is intentionally not a managed Version install candidate.</summary>
    [Fact]
    public async Task ManualOnlyReleaseManifestFailsClosedFromManagedVersionVerification()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("manual-only-source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "1.1.0",
            mutateManifest: static manifest =>
            {
                manifest["schemaVersion"] = "1.3";
                manifest["distributionMode"] = "manual-only";
            });

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository()
            .VerifyPackageAsync(sourceRoot, package, TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Issue);
        Assert.False(result.HasSupportedManagedLauncher);
    }

    /// <summary>JSON-null manifest collections fail closed as invalid payload.</summary>
    [Fact]
    public async Task NullManifestCollectionsFailAsInvalidPayload()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");

        ManagedVersionInstallResult result = await new FileSystemManagedVersionRepository().InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6", nullManifestCollections: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
    }
}
