using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class ManagedSetupRecoveryProbeTests
{
    private static ManagedSetupTransactionDocument CreateMarker(
        string root,
        string? state = null)
    {
        const string transaction = "0123456789abcdef0123456789abcdef";
        string normalizedRoot = Path.GetFullPath(root);
        string normalizedState = Path.GetFullPath(state ?? @"C:\state\version-manager-state.json");
        string rootName = Path.GetFileName(normalizedRoot);
        string markerName = Path.GetFileName(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(normalizedRoot));
        string stagingName = Path.GetFileName(
            FileSystemManagedInstallationRootProbe.GetStagingContainerPath(normalizedRoot));
        return new(
            "1.0",
            "NVT FW Combiner",
            1,
            transaction,
            normalizedRoot,
            normalizedState,
            new(101, new string('a', 64)),
            new(202, new string('b', 64), "NvtFwCombiner.Bootstrap.exe", 303, new string('c', 64)),
            new(4, new string('d', 64), 1, "1.0.6", new string('e', 64), @"G:\AUTO\catalog.json", "registry", @"G:\AUTO", "latest", "1.0.6", "packages/app.zip", 404, new string('f', 64), new string('1', 64), "entry"),
            [rootName, markerName, $"{stagingName}/{transaction}"],
            "staging");
    }

    private static byte[] CanonicalPreExtractionMarkerBytes(string root, string state)
    {
        const string transaction = "0123456789abcdef0123456789abcdef";
        string rootName = Path.GetFileName(root);
        string markerName = Path.GetFileName(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root));
        string stagingName = Path.GetFileName(
            FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root));
        static string Json(string value)
        {
            return JsonSerializer.Serialize(value);
        }
        string document = $$"""
            {
              "schemaVersion": "1.0",
              "product": "NVT FW Combiner",
              "launcherSetupProtocolVersion": 1,
              "transactionId": "{{transaction}}",
              "managedRootIdentity": {{Json(root)}},
              "statePathIdentity": {{Json(state)}},
              "distributionLauncherExecutable": {
                "size": 101,
                "sha256": "{{new string('a', 64)}}"
              },
              "payloadAdmission": {
                "descriptorSize": 202,
                "descriptorSha256": "{{new string('b', 64)}}",
                "bootstrapInstalledFileName": "NvtFwCombiner.Bootstrap.exe",
                "bootstrapSize": 303,
                "bootstrapSha256": "{{new string('c', 64)}}"
              },
              "candidate": {
                "registryRevision": 4,
                "registryDigest": "{{new string('d', 64)}}",
                "catalogSchemaVersion": 1,
                "catalogLatestVersion": "1.0.6",
                "catalogDigest": "{{new string('e', 64)}}",
                "catalogPath": "G:\\AUTO\\catalog.json",
                "registryId": "registry",
                "sourceRoot": "G:\\AUTO",
                "sourceStatus": "latest",
                "version": "1.0.6",
                "packagePath": "packages/app.zip",
                "packageSize": 404,
                "packageSha256": "{{new string('f', 64)}}",
                "releaseManifestSha256": "{{new string('1', 64)}}",
                "entryIdentity": "entry"
              },
              "ownedPaths": [
                {{Json(rootName)}},
                {{Json(markerName)}},
                {{Json($"{stagingName}/{transaction}")}}
              ],
              "phase": "staging"
            }
            """;
        return Encoding.UTF8.GetBytes(document);
    }

    private static SortedDictionary<string, string> CaptureTree(string root)
    {
        var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFileSystemEntries(
            root,
            "*",
            SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path);
            snapshot[relative] = Directory.Exists(path)
                ? "directory"
                : Convert.ToBase64String(File.ReadAllBytes(path));
        }
        return snapshot;
    }

    private static async Task<byte[]> CaptureMaterializerStagingMarkerAsync()
    {
        using var temporary = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        string state = Path.Combine(temporary.Path, "state", "version-manager.v1.json");
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        FreshInstallationCandidate candidate = CreateCandidate(temporary.Path);
        ManagedVersionAdmission admission = new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new RecoveryMaterializingRepository(admission),
            stagingCustodyAcquired: (_, cancellationToken) =>
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            });
        using var payload = new RecoveryPayloadCapture(CreatePayloadIdentity());

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await materializer.MaterializeAsync(
                root,
                state,
                payload,
                candidate,
                ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
                cancellation.Token));

        return File.ReadAllBytes(marker);
    }

    private static async Task<byte[]> CaptureMaterializerPromotedMarkerAsync(
        bool recordBootstrapLaunch)
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        string state = Path.Combine(temporary.Path, "state", "version-manager.v1.json");
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        FreshInstallationCandidate candidate = CreateCandidate(temporary.Path);
        ManagedVersionAdmission admission = new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new RecoveryMaterializingRepository(admission));
        using var payload = new RecoveryPayloadCapture(CreatePayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            state,
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Issue.ToString());
        IManagedPromotedFirstInstallation installation = Assert.IsType<
            IManagedPromotedFirstInstallation>(result.Installation, exactMatch: false);
        if (recordBootstrapLaunch)
        {
            ManagedFirstInstallationTransactionIssue advanced = await installation
                .RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken);
            Assert.Equal(ManagedFirstInstallationTransactionIssue.None, advanced);
        }
        installation.Dispose();
        return File.ReadAllBytes(marker);
    }

    private static ManagedDistributionPayloadIdentity CreatePayloadIdentity()
    {
        ReadOnlySpan<byte> launcher = "distribution-launcher"u8;
        ReadOnlySpan<byte> bootstrap = "immutable-bootstrap"u8;
        return new(
            ManagedAppVersion.Parse("1.0.4"),
            new string('c', 40),
            launcher.Length,
            Hash(launcher),
            512,
            new string('a', 64),
            new ManagedImmutableBootstrapIdentity(
                FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                bootstrap.Length,
                Hash(bootstrap)));
    }

    private static FreshInstallationCandidate CreateCandidate(string parent)
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
                new string('a', 64),
                new string('b', 64),
                "Release 1.0.4")]);
        UpdateCatalogVersionSnapshot package = Assert.IsType<UpdateCatalogSnapshot>(
            UpdateCatalogValidator.Validate(document).Snapshot).Versions[0];
        var identity = new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            1,
            new string('a', 64),
            1,
            package.Version,
            new string('b', 64),
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

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string LifetimeSuffix(ManagedProcessLifetimeKind kind)
    {
        return kind switch
        {
            ManagedProcessLifetimeKind.Bootstrap => ManagedProcessLifetimeLease.BootstrapSuffix,
            ManagedProcessLifetimeKind.Application => ManagedProcessLifetimeLease.ApplicationSuffix,
            ManagedProcessLifetimeKind.Launcher => ManagedProcessLifetimeLease.LauncherSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "OpenJobObjectW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint OpenJobObjectForTest(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        string name);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "AssignProcessToJobObject",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObjectForTest(nint job, nint process);

    private sealed class RecoveryPayloadCapture(ManagedDistributionPayloadIdentity identity)
        : IManagedDistributionPayloadCapture, IManagedDistributionPayloadContent
    {
        public ManagedDistributionPayloadIdentity Identity => identity;

        public ValueTask CopyBootstrapAsync(string destination, CancellationToken cancellationToken)
        {
            return new(File.WriteAllTextAsync(destination, "immutable-bootstrap", cancellationToken));
        }

        public ValueTask CopyDistributionLauncherAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            return new(File.WriteAllTextAsync(
                destination,
                "distribution-launcher",
                cancellationToken));
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecoveryMaterializingRepository(ManagedVersionAdmission admission)
        : IManagedVersionRepository, IWindowsCustodiedManagedVersionRepository
    {
        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly TempWorkspace _workspace;

        internal TemporaryDirectory()
        {
            _workspace = TempWorkspace.Create("nfc-recovery");
            Path = _workspace.Root;
        }

        internal string Path { get; }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }
}
