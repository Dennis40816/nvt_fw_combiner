using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    private static async Task<(ManagedFirstInstallationMaterializationResult Result, string Marker)>
        MaterializePromotedInstallationAsync(string parent)
    {
        string root = Path.Combine(parent, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(parent);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission));
        using var payload = new TestPayloadCapture(PayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(parent, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Issue.ToString());
        return (result, FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root));
    }

    private static ManagedDistributionPayloadIdentity PayloadIdentity(
        string bootstrapFileName = "NvtFwCombiner.Bootstrap.exe")
    {
        return new(
            ManagedAppVersion.Parse("1.0.4"),
            new string('c', 40),
            "distribution-launcher"u8.Length,
            Hash("distribution-launcher"u8),
            512,
            HashA,
            new ManagedImmutableBootstrapIdentity(
                bootstrapFileName,
                "immutable-bootstrap"u8.Length,
                Hash("immutable-bootstrap"u8)));
    }

    private static byte[] PayloadDescriptor(byte[] bootstrap)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "1.0",
            product = "NVT FW Combiner",
            payloadKind = "distribution-launcher-bootstrap",
            launcherSetupProtocolVersion = 1,
            launcherVersion = "1.0.4",
            runtimeIdentifier = "win-x64",
            sourceCommit = new string('c', 40),
            bootstrap = new
            {
                resourceName = "NvtFwCombiner.DistributionLauncher.Payload.NvtFwCombiner.Bootstrap.exe",
                installedFileName = "NvtFwCombiner.Bootstrap.exe",
                size = bootstrap.LongLength,
                sha256 = Hash(bootstrap),
                versionManagementProtocolVersion = 1,
                sourceCommit = new string('c', 40),
            },
        });
    }

    private static FreshInstallationCandidate Candidate(string parent)
    {
        string sourceRoot = Path.Combine(parent, "source");
        _ = Directory.CreateDirectory(sourceRoot);
        var document = new UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                "1.0.4",
                "2026-08-29T00:00:00Z",
                "packages/NvtFwCombiner-v1.0.4-win-x64.zip",
                4096,
                HashA,
                HashB,
                "Release 1.0.4")]);
        UpdateCatalogVersionSnapshot package = Assert.IsType<UpdateCatalogSnapshot>(
            UpdateCatalogValidator.Validate(document).Snapshot).Versions[0];
        var identity = new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            1,
            HashA,
            1,
            package.Version,
            HashB,
            Path.Combine(sourceRoot, "update-catalog.v1.json"),
            sourceRoot,
            UpdateSourceRegistryEntryStatus.Latest,
            package.PackagePath.Value,
            package.PackageSize,
            package.PackageSha256,
            package.ReleaseManifestSha256);
        return new(
            identity,
            package,
            new VerifiedUpdateCandidate(package.Version, package.Identity, package.ReleaseNotes));
    }

    private static FreshInstallationCandidate RealPackageCandidate(string parent)
    {
        string sourceRoot = Path.Combine(parent, "source-real");
        _ = Directory.CreateDirectory(sourceRoot);
        UpdateCatalogVersionSnapshot package = FileSystemManagedVersionRepositoryTests.CreatePackageForManagedSetup(
            sourceRoot,
            "1.0.4",
            includeManagedLauncher: true);
        var identity = new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            1,
            HashA,
            1,
            package.Version,
            HashB,
            Path.Combine(sourceRoot, "update-catalog.v1.json"),
            sourceRoot,
            UpdateSourceRegistryEntryStatus.Latest,
            package.PackagePath.Value,
            package.PackageSize,
            package.PackageSha256,
            package.ReleaseManifestSha256);
        return new(
            identity,
            package,
            new VerifiedUpdateCandidate(package.Version, package.Identity, package.ReleaseNotes));
    }

    private static ManagedVersionAdmission Admission(FreshInstallationCandidate candidate)
    {
        return new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed class TestPayloadCapture(
        ManagedDistributionPayloadIdentity identity,
        string launcherContent = "distribution-launcher")
        : IManagedDistributionPayloadCapture, IManagedDistributionPayloadContent
    {
        public ManagedDistributionPayloadIdentity Identity => identity;
        internal int CopyCount { get; private set; }

        public ValueTask CopyBootstrapAsync(string destination, CancellationToken cancellationToken)
        {
            CopyCount++;
            return new(File.WriteAllTextAsync(destination, "immutable-bootstrap", cancellationToken));
        }

        public ValueTask CopyDistributionLauncherAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            CopyCount++;
            return new(File.WriteAllTextAsync(destination, launcherContent, cancellationToken));
        }

        public void Dispose()
        {
        }
    }

    private sealed class OpaquePayloadCapture(ManagedDistributionPayloadIdentity identity)
        : IManagedDistributionPayloadCapture
    {
        public ManagedDistributionPayloadIdentity Identity => identity;

        public void Dispose()
        {
        }
    }

    private sealed class MaterializingRepository(
        ManagedVersionAdmission admission,
        Action<int, string>? inventoryObserved = null,
        bool createRepositoryStaging = false)
        : IManagedVersionRepository, IWindowsCustodiedManagedVersionRepository
    {
        internal int InstallCount { get; private set; }
        internal int InventoryCount { get; private set; }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstallCount++;
            if (createRepositoryStaging)
            {
                _ = Directory.CreateDirectory(Path.Combine(
                    managedRoot,
                    FileSystemManagedVersionRepository.StagingDirectoryName));
            }
            string versionRoot = Path.Combine(managedRoot, "versions", package.Version.ToString());
            _ = Directory.CreateDirectory(versionRoot);
            File.WriteAllText(Path.Combine(versionRoot, "payload.txt"), "installed");
            return ValueTask.FromResult(new ManagedVersionInstallResult(
                admission,
                ManagedVersionInstallIssue.None,
                WasAlreadyInstalled: false));
        }

        async ValueTask<ManagedVersionPayloadMaterializationResult>
            IWindowsCustodiedManagedVersionRepository.MaterializeVerifiedPayloadWithinHeldRootAsync(
                WindowsStableRelativeWriteRoot writeRoot,
                string sourceRoot,
                UpdateCatalogVersionSnapshot package,
                Action<string>? afterPackageDirectoryCreated,
                CancellationToken cancellationToken)
        {
            ManagedVersionInstallResult result = await InstallAsync(
                writeRoot.RootPath,
                sourceRoot,
                package,
                cancellationToken);
            return new(result.Admission, result.Issue, result.WasAlreadyInstalled);
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InventoryCount++;
            inventoryObserved?.Invoke(InventoryCount, managedRoot);
            ManagedVersionInventory inventory = ManagedVersionInventory.Create(
                [new(
                    admission.Version,
                    admission.AdmissionIdentity,
                    ManagedVersionIntegrity.Healthy,
                    DamageReason: null,
                    IsActive: true,
                    IsLastKnownGood: true,
                    ManagedVersionAdmissionState.Admitted,
                    admission)]);
            return ValueTask.FromResult(ManagedVersionInventoryReadResult.Success(inventory));
        }

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission target,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TemporaryRoot : IAsyncDisposable
    {
        internal TemporaryRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nfc-distribution-runtime-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public ValueTask DisposeAsync()
        {
            Directory.Delete(Path, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

}
