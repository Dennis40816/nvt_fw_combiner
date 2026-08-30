using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;
using System.Security.Cryptography;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Locks exact writer custody and fail-closed recovery execution.</summary>
public sealed partial class ManagedSetupRecoveryExecutionTests
{
    private sealed record ForeignToken : ManagedSetupRecoveryExecutionToken;

    private static string DirectoryProof(string root)
    {
        return string.Join('\n', Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => string.Concat(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                "|",
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))))
            .Order(StringComparer.Ordinal));
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class RecoveryEvidenceFixture : IDisposable
    {
        private readonly TempWorkspace _workspace;
        private readonly IManagedVersionRepository _repository;
        private readonly IInstalledLauncherRepository _launcherRepository;
        private readonly ILauncherBootstrapStateStore _launcherStateStore;

        private RecoveryEvidenceFixture(
            TempWorkspace workspace,
            string managedRoot,
            ManagedSetupRecoveryTransaction transaction,
            IManagedVersionRepository repository,
            IInstalledLauncherRepository launcherRepository,
            ILauncherBootstrapStateStore launcherStateStore,
            Action<int>? afterDelete = null)
        {
            _workspace = workspace;
            _repository = repository;
            _launcherRepository = launcherRepository;
            _launcherStateStore = launcherStateStore;
            ManagedRoot = managedRoot;
            Transaction = transaction;
            Executor = CreateExecutor(afterDelete);
        }

        internal string ManagedRoot { get; }
        internal ManagedSetupRecoveryTransaction Transaction { get; private set; }
        internal FileSystemManagedSetupRecoveryExecution Executor { get; }

        internal FileSystemManagedSetupRecoveryExecution CreateExecutor(
            Action<int>? afterDelete = null,
            Action<long>? afterHashedFile = null)
        {
            return new FileSystemManagedSetupRecoveryExecution(
                Transaction.StatePathIdentity,
                _repository,
                _launcherRepository,
                _launcherStateStore,
                afterDelete,
                afterHashedFile);
        }

        internal static async Task<RecoveryEvidenceFixture> CreateAsync(
            Action<int>? afterDelete = null,
            bool withLauncherState = false,
            bool readyLauncherState = false)
        {
            Assert.False(withLauncherState && readyLauncherState);
            TempWorkspace workspace = TempWorkspace.Create("nfc-recovery-evidence");
            try
            {
                string root = Path.Combine(workspace.Root, "managed");
                string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
                _ = Directory.CreateDirectory(root);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                ManagedAppVersion version = ManagedAppVersion.Parse("1.0.6");
                string sourceRoot = Path.Combine(workspace.Root, "source");
                UpdateCatalogVersionSnapshot package =
                    FileSystemManagedVersionRepositoryTests.CreatePackageForManagedSetup(
                        sourceRoot,
                        version.ToString());
                ManagedVersionInstallResult installed = await new FileSystemManagedVersionRepository()
                    .InstallAsync(
                        root,
                        sourceRoot,
                        package,
                        TestContext.Current.CancellationToken);
                Assert.True(installed.IsSuccess, installed.Issue.ToString());
                ManagedVersionAdmission admission = installed.Admission!;
                string repositoryStaging = Path.Combine(
                    root,
                    FileSystemManagedVersionRepository.StagingDirectoryName);
                if (Directory.Exists(repositoryStaging))
                {
                    Assert.Empty(Directory.EnumerateFileSystemEntries(repositoryStaging));
                    Directory.Delete(repositoryStaging);
                }
                string launcherText = "distribution-launcher";
                string bootstrapText = "immutable-bootstrap";
                await File.WriteAllTextAsync(
                    Path.Combine(
                        root,
                        FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
                    launcherText,
                    TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(
                    Path.Combine(
                        root,
                        FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName),
                    bootstrapText,
                    TestContext.Current.CancellationToken);
                await new JsonVersionManagerStateStore(
                    Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.SeedFileName),
                    allowUnboundSeedTemplate: true).SaveAsync(
                    ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
                    TestContext.Current.CancellationToken);
                await new JsonVersionManagerStateStore(statePath).SaveAsync(
                    ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission)
                        .BindToManagedRoot(root),
                    TestContext.Current.CancellationToken);
                var launcher = ManagedLauncherIdentity.Create(
                    version,
                    admission.AdmissionIdentity,
                    admission.ReleaseManifestSha256,
                    version,
                    ManagedLauncherIdentity.SupportedProtocolVersion,
                    ManagedLauncherIdentity.ExecutablePath,
                    7,
                    new string('c', 64));
                ILauncherBootstrapStateStore launcherStateStore;
                if (withLauncherState || readyLauncherState)
                {
                    var durableLauncherStore = new JsonLauncherBootstrapStateStore(statePath);
                    LauncherBootstrapStateSaveResult saved = await durableLauncherStore.TrySaveAsync(
                        readyLauncherState
                            ? LauncherBootstrapState.Create(
                                root,
                                active: launcher,
                                lastKnownGood: launcher,
                                pending: null,
                                failed: null)
                            : LauncherBootstrapState.Create(
                                root,
                                active: null,
                                lastKnownGood: null,
                                pending: null,
                                failed: launcher),
                        TestContext.Current.CancellationToken);
                    Assert.True(saved.IsSuccess, saved.Issue.ToString());
                    launcherStateStore = durableLauncherStore;
                }
                else
                {
                    launcherStateStore = new MissingLauncherStateStore();
                }
                var transaction = new ManagedSetupRecoveryTransaction(
                    "0123456789abcdef0123456789abcdef",
                    root,
                    statePath,
                    ManagedSetupRecoveryPhase.BootstrapLaunchRecorded,
                    ["managed", ".managed.setup-transaction.v1.json", ".managed.setup-staging/0123456789abcdef0123456789abcdef"],
                    new(
                        launcherText.Length,
                        Hash(launcherText),
                        10,
                        new string('d', 64),
                        FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                        bootstrapText.Length,
                        Hash(bootstrapText)),
                    new(
                        1,
                        new string('e', 64),
                        1,
                        version.ToString(),
                        new string('f', 64),
                        Path.Combine(sourceRoot, "update-catalog.v1.json"),
                        "registry",
                        sourceRoot,
                        "latest",
                        version.ToString(),
                        package.PackagePath.Value,
                        package.PackageSize,
                        package.PackageSha256,
                        package.ReleaseManifestSha256,
                        package.Identity));
                await File.WriteAllTextAsync(
                    FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
                    "exact-marker",
                    TestContext.Current.CancellationToken);
                return new(
                    workspace,
                    root,
                    transaction,
                    new HealthyRepository(admission),
                    new FixedLauncherRepository(launcher),
                    launcherStateStore,
                    afterDelete);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }

        internal static Task<RecoveryEvidenceFixture> CreateEarlyStagingAsync(
            Action<int>? afterDelete = null,
            long launcherSize = 10,
            long bootstrapSize = 10)
        {
            return CreateMissingStatePrefixAsync(
                ManagedSetupRecoveryPhase.Staging,
                createSkeleton: false,
                afterDelete,
                launcherSize,
                bootstrapSize);
        }

        internal static async Task<RecoveryEvidenceFixture> CreateFullStagingAsync(
            Action<int>? afterDelete = null)
        {
            RecoveryEvidenceFixture fixture = await CreateAsync(afterDelete);
            try
            {
                string container = FileSystemManagedInstallationRootProbe.GetStagingContainerPath(
                    fixture.ManagedRoot);
                string child = Path.Combine(container, fixture.Transaction.TransactionId);
                _ = Directory.CreateDirectory(container);
                Directory.Move(fixture.ManagedRoot, child);
                ManagedSetupRecoveryTransaction original = fixture.Transaction;
                fixture.Transaction = new ManagedSetupRecoveryTransaction(
                    original.TransactionId,
                    original.ManagedRootIdentity,
                    original.StatePathIdentity,
                    ManagedSetupRecoveryPhase.Staging,
                    original.OwnedPaths,
                    original.Payload,
                    original.Candidate);
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        internal static Task<RecoveryEvidenceFixture> CreateTerminalSkeletonAsync()
        {
            return CreateMissingStatePrefixAsync(
                ManagedSetupRecoveryPhase.RootPromoted,
                createSkeleton: true,
                afterDelete: null,
                launcherSize: 10,
                bootstrapSize: 10);
        }

        private static async Task<RecoveryEvidenceFixture> CreateMissingStatePrefixAsync(
            ManagedSetupRecoveryPhase phase,
            bool createSkeleton,
            Action<int>? afterDelete,
            long launcherSize,
            long bootstrapSize)
        {
            TempWorkspace workspace = TempWorkspace.Create("nfc-recovery-prefix");
            try
            {
                string root = Path.Combine(workspace.Root, "managed");
                string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
                _ = Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                ManagedAppVersion version = ManagedAppVersion.Parse("1.0.6");
                if (createSkeleton)
                {
                    _ = Directory.CreateDirectory(Path.Combine(root, "versions", version.ToString()));
                }
                var transaction = new ManagedSetupRecoveryTransaction(
                    "0123456789abcdef0123456789abcdef",
                    root,
                    statePath,
                    phase,
                    ["managed", ".managed.setup-transaction.v1.json", ".managed.setup-staging/0123456789abcdef0123456789abcdef"],
                    new(
                        launcherSize,
                        new string('a', 64),
                        10,
                        new string('b', 64),
                        FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                        bootstrapSize,
                        new string('c', 64)),
                    new(
                        1,
                        new string('d', 64),
                        1,
                        version.ToString(),
                        new string('e', 64),
                        Path.Combine(workspace.Root, "source", "update-catalog.v1.json"),
                        "registry",
                        Path.Combine(workspace.Root, "source"),
                        "latest",
                        version.ToString(),
                        "packages/app.zip",
                        1024,
                        new string('f', 64),
                        new string('1', 64),
                        $"{version}|packages/app.zip|1024|{new string('f', 64)}|{new string('1', 64)}"));
                await File.WriteAllTextAsync(
                    FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
                    "exact-marker",
                    TestContext.Current.CancellationToken);
                return new RecoveryEvidenceFixture(
                    workspace,
                    root,
                    transaction,
                    new ThrowingRepository(),
                    new ThrowingLauncherRepository(),
                    new MissingLauncherStateStore(),
                    afterDelete);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }

        private static string Hash(string text)
        {
            return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        }
    }

    private sealed class ThrowingRepository : IManagedVersionRepository
    {
        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "A marker-derived prefix must not require an installed repository.");
        }

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
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

    private sealed class ThrowingLauncherRepository : IInstalledLauncherRepository
    {
        public ValueTask<InstalledLauncherResult> VerifyAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "A Missing/Missing marker-derived prefix must not require Launcher identity.");
        }

        public ValueTask<InstalledLauncherLaunchResult> AcquireLaunchLeaseAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class HealthyRepository(ManagedVersionAdmission admission)
        : IManagedVersionRepository
    {
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

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
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

    private sealed class FixedLauncherRepository(ManagedLauncherIdentity launcher)
        : IInstalledLauncherRepository
    {
        public ValueTask<InstalledLauncherResult> VerifyAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new InstalledLauncherResult(
                launcher,
                InstalledLauncherIssue.None));
        }

        public ValueTask<InstalledLauncherLaunchResult> AcquireLaunchLeaseAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class MissingLauncherStateStore : ILauncherBootstrapStateStore
    {
        public ValueTask<LauncherBootstrapStateLoadResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LauncherBootstrapStateLoadResult(
                null,
                LauncherBootstrapStateLoadIssue.Missing));
        }

        public ValueTask<LauncherBootstrapStateSaveResult> TrySaveAsync(
            LauncherBootstrapState state,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
