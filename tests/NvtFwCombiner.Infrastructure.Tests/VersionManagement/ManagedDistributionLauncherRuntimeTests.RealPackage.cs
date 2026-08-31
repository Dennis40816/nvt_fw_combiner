using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies real-package Setup admission and root-custody boundaries.</summary>
public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    /// <summary>Promoted Setup custody still permits the immutable installed Launcher read lease.</summary>
    [Fact]
    public async Task PromotedRealPackageCustodyAllowsInstalledLauncherLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(
            temporary.Path,
            useStandaloneLauncherProbe: true);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository());
        using var payload = new TestPayloadCapture(PayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Issue.ToString());
        IManagedPromotedFirstInstallation installation = result.Installation!;
        try
        {
            string versionRoot = Path.Combine(
                root,
                FileSystemManagedVersionRepository.VersionsDirectoryName,
                admission.Version.ToString());
            WindowsStableCustodyResult tree = WindowsStablePathCustody.TryAcquireImmutableTree(
                versionRoot,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(tree.IsAcquired, tree.Issue.ToString());
            tree.Custody!.Dispose();

            InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
                .AcquireLaunchLeaseAsync(
                    root,
                    admission,
                    TestContext.Current.CancellationToken);
            Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
            acquired.Lease!.Dispose();

            _ = Assert.Throws<IOException>(() => Directory.Move(root, root + ".replacement"));
        }
        finally
        {
            installation.Dispose();
        }

        string displaced = root + ".replacement";
        Directory.Move(root, displaced);
        Directory.Move(displaced, root);
    }

    /// <summary>A real schema-1.2 ZIP is the independent source of every promoted version byte.</summary>
    [Fact]
    public async Task MaterializerPromotesRealPackageAndMatchesEverySourceArchiveMember()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission expectedAdmission = Admission(candidate);
        var repository = new FileSystemManagedVersionRepository();
        ManagedPackageVerificationResult verified = await repository.VerifyPackageAsync(
            candidate.Identity.SourceRoot,
            candidate.Package,
            TestContext.Current.CancellationToken);
        Assert.True(verified.IsVerified, verified.Issue.ToString());
        Assert.True(verified.HasSupportedManagedLauncher);
        string ordinaryRoot = Path.Combine(temporary.Path, "ordinary-managed");
        _ = Directory.CreateDirectory(ordinaryRoot);
        ManagedVersionInstallResult ordinary = await repository.InstallAsync(
            ordinaryRoot,
            candidate.Identity.SourceRoot,
            candidate.Package,
            TestContext.Current.CancellationToken);
        Assert.True(ordinary.IsSuccess, ordinary.Issue.ToString());
        Assert.Equal(expectedAdmission, ordinary.Admission);
        int cleanupAttempts = 0;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            repository,
            repositoryStagingCleanupAttemptObserved: (_, _) => cleanupAttempts++);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(expectedAdmission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.Equal(1, cleanupAttempts);
        IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(expectedAdmission, installation.Admission);
        string installedVersion = Path.Combine(root, "versions", candidate.Package.Version.ToString());
        string packagePath = Path.Combine(
            candidate.Identity.SourceRoot,
            candidate.Package.PackagePath.Value.Replace('/', Path.DirectorySeparatorChar));
        Dictionary<string, (long Length, string Sha256)> expectedMembers =
            await ReadPackageMembersAsync(packagePath, candidate.Package.Version.ToString());
        Dictionary<string, (long Length, string Sha256)> actualMembers =
            await ReadInstalledMembersAsync(installedVersion);
        Dictionary<string, (long Length, string Sha256)> ordinaryMembers =
            await ReadInstalledMembersAsync(Path.Combine(
                ordinaryRoot,
                FileSystemManagedVersionRepository.VersionsDirectoryName,
                candidate.Package.Version.ToString()));
        Assert.True(actualMembers.Remove(FileSystemManagedVersionRepository.AdmissionFileName));
        Assert.True(ordinaryMembers.Remove(FileSystemManagedVersionRepository.AdmissionFileName));
        Assert.Equal(
            expectedMembers.Keys.Order(StringComparer.Ordinal),
            actualMembers.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedMembers.Keys.Order(StringComparer.Ordinal),
            ordinaryMembers.Keys.Order(StringComparer.Ordinal));
        foreach ((string path, (long length, string sha256)) in expectedMembers)
        {
            Assert.Equal((length, sha256), actualMembers[path]);
            Assert.Equal((length, sha256), ordinaryMembers[path]);
        }
        Assert.Equal(
            [
                FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName,
                FileSystemManagedFirstInstallationRootMaterializer.SeedFileName,
                FileSystemManagedVersionRepository.VersionsDirectoryName,
            ],
            Directory.EnumerateFileSystemEntries(root)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        Assert.True(File.Exists(marker));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>A transient Windows staging-cleanup conflict is retried without weakening real ZIP admission.</summary>
    [Fact]
    public async Task RealPackageFirstInstallRetriesOneTransientRepositoryStagingCleanupAndCompletes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        Microsoft.Win32.SafeHandles.SafeFileHandle? contentionHandle = null;
        int successfulDeleteOpenCount = 0;
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingDelete: _ => successfulDeleteOpenCount++,
            beforeRepositoryStagingCleanup: repositoryStaging =>
            {
                contentionHandle = OpenRepositoryStagingHandle(
                    repositoryStaging,
                    deleteOnClose: false);
            },
            repositoryStagingCleanupAttemptObserved: (attempt, state) =>
            {
                attemptStates.Add(state);
                if (attempt == 1 && state == ManagedSetupStagingCleanupState.OwnedDeletionPending)
                {
                    contentionHandle!.Dispose();
                }
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        contentionHandle?.Dispose();

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.NotNull(contentionHandle);
        Assert.Equal(1, successfulDeleteOpenCount);
        Assert.Equal(
            [
                ManagedSetupStagingCleanupState.OwnedDeletionPending,
                ManagedSetupStagingCleanupState.Absent,
            ],
            attemptStates);
        Assert.False(Directory.Exists(Path.Combine(
            root,
            FileSystemManagedVersionRepository.StagingDirectoryName)));
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
        installation.Dispose();
        Assert.False(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Native sharing contention after identity observation is retried once.</summary>
    [Fact]
    public async Task TransientRepositoryStagingSharingContentionRetriesAndCompletes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        int deleteOpenCount = 0;
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            repositoryStagingCleanupAttemptObserved: (_, state) => attemptStates.Add(state),
            repositoryStagingCleanupDelay: (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
            repositoryStagingDeleteOpenStatusOverride: actual =>
                ++deleteOpenCount == 1
                    ? WindowsStablePathCustody.NativeMethods.StatusSharingViolation
                    : actual);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.Equal(
            [ManagedSetupStagingCleanupState.RetryableContention, ManagedSetupStagingCleanupState.Deleted],
            attemptStates);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Persistent native sharing contention exhausts exactly one bounded budget.</summary>
    [Fact]
    public async Task PersistentRepositoryStagingSharingContentionExhaustsExactBudget()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var delays = new List<TimeSpan>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            repositoryStagingCleanupAttemptObserved: (_, state) => attemptStates.Add(state),
            repositoryStagingCleanupDelay: (delay, token) =>
            {
                token.ThrowIfCancellationRequested();
                delays.Add(delay);
                return ValueTask.CompletedTask;
            },
            repositoryStagingDeleteOpenStatusOverride: _ =>
                WindowsStablePathCustody.NativeMethods.StatusSharingViolation);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal(20, attemptStates.Count);
        Assert.All(attemptStates, state =>
            Assert.Equal(ManagedSetupStagingCleanupState.RetryableContention, state));
        Assert.Equal(19, delays.Count);
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromMilliseconds(250), delay));
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Access denial and unclassified native failures are terminal, never retryable.</summary>
    [Theory]
    [InlineData(WindowsStablePathCustody.NativeMethods.StatusAccessDenied)]
    [InlineData(WindowsStablePathCustody.NativeMethods.StatusObjectNameNotFound)]
    [InlineData(WindowsStablePathCustody.NativeMethods.StatusObjectPathNotFound)]
    [InlineData(WindowsStablePathCustody.NativeMethods.StatusDeletePending)]
    [InlineData(unchecked((int)0xC0000001))]
    public async Task TerminalRepositoryStagingNativeStatusFailsWithoutRetry(int nativeStatus)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            repositoryStagingCleanupAttemptObserved: (_, state) => attemptStates.Add(state),
            repositoryStagingDeleteOpenStatusOverride: _ => nativeStatus);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal([ManagedSetupStagingCleanupState.ChangedOrUnsafe], attemptStates);
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Path absence alone cannot prove owned deletion when the native observer cannot.</summary>
    [Fact]
    public async Task RepositoryStagingDeletionRequiresNativeAbsenceProof()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            repositoryStagingCleanupAttemptObserved: (_, state) => attemptStates.Add(state),
            repositoryOwnedDeletionObservationStatusOverride: _ =>
                WindowsStablePathCustody.NativeMethods.StatusAccessDenied);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal([ManagedSetupStagingCleanupState.ChangedOrUnsafe], attemptStates);
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Either native not-found result proves the exact owned deletion completed.</summary>
    [Theory]
    [InlineData(WindowsStablePathCustody.NativeMethods.StatusObjectNameNotFound)]
    [InlineData(WindowsStablePathCustody.NativeMethods.StatusObjectPathNotFound)]
    public async Task OwnedRepositoryStagingNativeAbsenceCompletesInstallation(int nativeStatus)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            repositoryStagingCleanupAttemptObserved: (_, state) => attemptStates.Add(state),
            repositoryOwnedDeletionObservationStatusOverride: _ => nativeStatus);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.Equal([ManagedSetupStagingCleanupState.Deleted], attemptStates);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Owned delete-pending cleanup is bounded and preserves recovery evidence.</summary>
    [Fact]
    public async Task PersistentRepositoryStagingHolderExhaustsExactBudgetWithoutPromotion()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        Microsoft.Win32.SafeHandles.SafeFileHandle? held = null;
        var attemptStates = new List<ManagedSetupStagingCleanupState>();
        var delays = new List<TimeSpan>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
                held = OpenRepositoryStagingHandle(repositoryStaging, deleteOnClose: false),
            repositoryStagingCleanupAttemptObserved: (_, state) => attemptStates.Add(state),
            repositoryStagingCleanupDelay: (delay, token) =>
            {
                token.ThrowIfCancellationRequested();
                delays.Add(delay);
                return ValueTask.CompletedTask;
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        held?.Dispose();

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal(20, attemptStates.Count);
        Assert.All(attemptStates, state =>
            Assert.Equal(ManagedSetupStagingCleanupState.OwnedDeletionPending, state));
        Assert.Equal(19, delays.Count);
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromMilliseconds(250), delay));
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Content in repository staging is terminal and remains untouched.</summary>
    [Fact]
    public async Task NonemptyRepositoryStagingFailsBeforeAnyDeleteAttempt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        string? sentinel = null;
        int attempts = 0;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
            {
                sentinel = Path.Combine(repositoryStaging, "unexpected.bin");
                File.WriteAllText(sentinel, "keep");
            },
            repositoryStagingCleanupAttemptObserved: (_, _) => attempts++);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal(0, attempts);
        Assert.NotNull(sentinel);
        Assert.True(File.Exists(sentinel));
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>A file substituted for repository staging is terminal and remains untouched.</summary>
    [Fact]
    public async Task RepositoryStagingFileSubstitutionFailsBeforeAnyDeleteAttempt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        string? substitutedPath = null;
        int attempts = 0;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
            {
                Directory.Delete(repositoryStaging);
                File.WriteAllText(repositoryStaging, "replacement");
                substitutedPath = repositoryStaging;
            },
            repositoryStagingCleanupAttemptObserved: (_, _) => attempts++);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal(0, attempts);
        Assert.NotNull(substitutedPath);
        Assert.True(File.Exists(substitutedPath));
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>A direct-child reparse substitution is terminal and its target remains untouched.</summary>
    [Fact]
    public async Task RepositoryStagingReparseSubstitutionFailsBeforeAnyDeleteAttempt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        string outside = Path.Combine(temporary.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        string outsideSentinel = Path.Combine(outside, "keep.txt");
        File.WriteAllText(outsideSentinel, "keep");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        string? link = null;
        int attempts = 0;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
            {
                Directory.Delete(repositoryStaging);
                _ = Directory.CreateSymbolicLink(repositoryStaging, outside);
                link = repositoryStaging;
            },
            repositoryStagingCleanupAttemptObserved: (_, _) => attempts++);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal(0, attempts);
        Assert.NotNull(link);
        Assert.True(Directory.Exists(link));
        Assert.True(File.Exists(outsideSentinel));
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>A replacement created after our owned deletion is never adopted or deleted.</summary>
    [Fact]
    public async Task RepositoryStagingReplacementAfterOwnedDeleteFailsAndSurvives()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        Microsoft.Win32.SafeHandles.SafeFileHandle? held = null;
        string? staging = null;
        string? sentinel = null;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
            {
                staging = repositoryStaging;
                held = OpenRepositoryStagingHandle(repositoryStaging, deleteOnClose: false);
            },
            repositoryStagingCleanupAttemptObserved: (attempt, state) =>
            {
                if (attempt != 1 || state != ManagedSetupStagingCleanupState.OwnedDeletionPending)
                {
                    return;
                }
                held!.Dispose();
                _ = Directory.CreateDirectory(staging!);
                sentinel = Path.Combine(staging!, "replacement.txt");
                File.WriteAllText(sentinel, "replacement");
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.NotNull(sentinel);
        Assert.True(File.Exists(sentinel));
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Cancellation during owned delete-pending wait stops before promotion.</summary>
    [Fact]
    public async Task RepositoryStagingCleanupCancellationPreservesRecoveryEvidence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        using var cancellation = new CancellationTokenSource();
        Microsoft.Win32.SafeHandles.SafeFileHandle? held = null;
        int attempts = 0;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
                held = OpenRepositoryStagingHandle(repositoryStaging, deleteOnClose: false),
            repositoryStagingCleanupAttemptObserved: (attempt, state) =>
            {
                attempts = attempt;
            },
            repositoryStagingCleanupDelay: (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await materializer.MaterializeAsync(
                root,
                Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
                payload,
                candidate,
                ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
                cancellation.Token));
        held?.Dispose();

        Assert.Equal(1, attempts);
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>A pre-existing delete-pending child is never adopted as our cleanup.</summary>
    [Fact]
    public async Task PreexistingRepositoryStagingDeletePendingIsTerminal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        Microsoft.Win32.SafeHandles.SafeFileHandle? held = null;
        int attempts = 0;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            beforeRepositoryStagingCleanup: repositoryStaging =>
            {
                held = OpenRepositoryStagingHandle(repositoryStaging, deleteOnClose: true);
                Assert.True(WindowsStablePathCustody.MarkDeleteOnClose(held));
            },
            repositoryStagingCleanupAttemptObserved: (_, _) => attempts++);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        held?.Dispose();

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.Equal(0, attempts);
        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    private static Microsoft.Win32.SafeHandles.SafeFileHandle OpenRepositoryStagingHandle(
        string repositoryStaging,
        bool deleteOnClose)
    {
        uint access = WindowsStablePathCustody.NativeMethods.ReadAttributes |
            WindowsStablePathCustody.NativeMethods.Synchronize |
            (deleteOnClose ? WindowsStablePathCustody.NativeMethods.Delete : 0);
        uint share = WindowsStablePathCustody.NativeMethods.ShareRead |
            WindowsStablePathCustody.NativeMethods.ShareWrite |
            (deleteOnClose ? WindowsStablePathCustody.NativeMethods.ShareDelete : 0);
        uint flags = WindowsStablePathCustody.NativeMethods.BackupSemantics |
            WindowsStablePathCustody.NativeMethods.OpenReparsePoint |
            (deleteOnClose ? WindowsStablePathCustody.NativeMethods.DeleteOnClose : 0);
        Microsoft.Win32.SafeHandles.SafeFileHandle handle =
            WindowsStablePathCustody.NativeMethods.CreateFile(
                repositoryStaging,
                access,
                share,
                0,
                WindowsStablePathCustody.NativeMethods.OpenExisting,
                flags,
                0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new IOException("Could not create the repository staging test handle.");
        }
        return handle;
    }

    /// <summary>Setup reserves only its three files, two directories, and exact payload bytes.</summary>
    [Fact]
    public async Task SetupTreeLimitsAdmitExactOverheadAndRejectEveryOneOverDimension()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "setup-root");
        _ = Directory.CreateDirectory(root);
        byte[] seed = new byte[37];
        await File.WriteAllBytesAsync(
            Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.SeedFileName),
            seed,
            TestContext.Current.CancellationToken);
        ManagedDistributionPayloadIdentity payload = PayloadIdentity();

        WindowsStableTreeLimits limits =
            FileSystemManagedFirstInstallationRootMaterializer.CreateSetupTreeLimits(root, payload);
        long expectedBytes = checked(
            FileSystemManagedVersionRepository.MaximumInstalledBytes +
            payload.LauncherSize +
            payload.Bootstrap.Length +
            seed.LongLength);
        Assert.Equal(FileSystemManagedVersionRepository.MaximumInstalledFiles + 3, limits.MaximumFiles);
        Assert.Equal(
            FileSystemManagedVersionRepository.MaximumInstalledDirectories + 2,
            limits.MaximumDirectories);
        Assert.Equal(expectedBytes, limits.MaximumBytes);
        Assert.True(new WindowsStableTreeReservation(
            limits.MaximumFiles,
            limits.MaximumDirectories,
            limits.MaximumBytes,
            limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(
            limits.MaximumFiles + 1,
            limits.MaximumDirectories,
            limits.MaximumBytes,
            limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(
            limits.MaximumFiles,
            limits.MaximumDirectories + 1,
            limits.MaximumBytes,
            limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(
            limits.MaximumFiles,
            limits.MaximumDirectories,
            limits.MaximumBytes + 1,
            limits).IsWithinLimits);
    }

    /// <summary>The exact renamed root stays non-replaceable until closed custody owns the same identity.</summary>
    [Fact]
    public async Task WholeRootPromotionTransfersCustodyWithoutReplacementWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool replacementBlocked = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            afterRootPromotion: promotedRoot =>
            {
                try
                {
                    Directory.Move(promotedRoot, promotedRoot + ".replacement");
                }
                catch (IOException)
                {
                    replacementBlocked = true;
                }
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.True(replacementBlocked);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Cancellation during post-promotion capture retains a staging-phase recovery marker.</summary>
    [Fact]
    public async Task PostPromotionCaptureCancellationRetainsStagingPhaseRecoveryEvidence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        using var cancellation = new CancellationTokenSource();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            afterRootPromotion: _ => cancellation.Cancel());
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            cancellation.Token);

        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        JsonObject marker = JsonNode.Parse(await File.ReadAllTextAsync(
            markerPath,
            TestContext.Current.CancellationToken))!.AsObject();
        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.True(Directory.Exists(root));
        Assert.Equal("staging", marker["phase"]!.GetValue<string>());
    }
}
