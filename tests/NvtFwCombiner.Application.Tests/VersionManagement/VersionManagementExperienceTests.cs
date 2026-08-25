using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests the stateful version-management use case rather than only its pure policies.</summary>
public sealed partial class VersionManagementExperienceTests
{
    private const string Hash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>A newly installed fourth healthy version persists a soft reminder that Keep all can clear.</summary>
    [Fact]
    public async Task SuccessfulFourthInstallPersistsRetentionReviewUntilAcknowledged()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4"), Admission("0.10.3")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var repository = new HealthyRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        VersionInstallOperationResult installed = await experience.InstallAsync(
            ManagedAppVersion.Parse("0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.True(installed.Install.IsSuccess);
        Assert.Equal(4, installed.Snapshot.Inventory.HealthyCount);
        Assert.True(installed.Snapshot.State!.RetentionReviewDue);
        Assert.True(stateStore.State.RetentionReviewDue);

        VersionManagementSnapshot acknowledged = await experience.AcknowledgeRetentionReviewAsync(
            TestContext.Current.CancellationToken);

        Assert.False(acknowledged.State!.RetentionReviewDue);
        Assert.False(stateStore.State.RetentionReviewDue);
        Assert.Equal(4, acknowledged.Inventory.HealthyCount);
        Assert.Empty(repository.Deleted);
    }

    /// <summary>A failed retention save keeps the durable reminder and returns typed unavailable state.</summary>
    [Fact]
    public async Task RetentionAcknowledgementSaveFailurePreservesReminder()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5")
            .WithRetentionReviewDue(retentionReviewDue: true);
        var stateStore = new FailingStateStore(initial, failOnSave: 1);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new CountingCatalogSource(),
            new HealthyRepository());

        VersionManagementSnapshot result = await experience.AcknowledgeRetentionReviewAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, result.StateIssue);
        Assert.True(result.State!.RetentionReviewDue);
        Assert.True(stateStore.State.RetentionReviewDue);
        Assert.Equal(1, stateStore.SaveCount);
    }

    /// <summary>Deleting last-known-good revalidates and refuses mutation until the second warning is confirmed.</summary>
    [Fact]
    public async Task LastKnownGoodDeleteRequiresSeparateConfirmedRequest()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.4");
        var stateStore = new MemoryStateStore(initial);
        var repository = new HealthyRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);

        VersionDeleteOperationResult warning = await experience.DeleteAsync(
            ManagedAppVersion.Parse("0.10.4"),
            rollbackLossConfirmed: false,
            TestContext.Current.CancellationToken);

        Assert.True(warning.Decision.RequiresRollbackLossWarning);
        Assert.Equal(VersionDeleteOperationIssue.RollbackConfirmationRequired, warning.OperationIssue);
        Assert.Null(warning.RepositoryIssue);
        Assert.Empty(repository.Deleted);
        Assert.Equal(ManagedAppVersion.Parse("0.10.4"), stateStore.State.LastKnownGoodVersion);

        VersionDeleteOperationResult deleted = await experience.DeleteAsync(
            ManagedAppVersion.Parse("0.10.4"),
            rollbackLossConfirmed: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionDeleteOperationIssue.None, deleted.OperationIssue);
        Assert.Equal(ManagedVersionDeleteIssue.None, deleted.RepositoryIssue);
        Assert.Equal([ManagedAppVersion.Parse("0.10.4")], repository.Deleted);
        Assert.Null(deleted.Snapshot.State!.LastKnownGoodVersion);
        Assert.Null(stateStore.State.LastKnownGoodVersion);
    }

    /// <summary>Permission failure remains distinct from an absent or offline source.</summary>
    [Fact]
    public async Task PermissionDeniedSourcePublishesTypedVisibleStatus()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            new IssueCatalogSource(UpdateCatalogLoadIssue.PermissionDenied),
            new HealthyRepository());

        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        VersionManagementSnapshot checkedSnapshot = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.PermissionDenied, checkedSnapshot.SourceStatus);
        Assert.Equal(UpdateCatalogLoadIssue.PermissionDenied, checkedSnapshot.CatalogIssue);
        Assert.Null(checkedSnapshot.Catalog);
    }

    /// <summary>Every source failure maps to a stable visible status without throwing.</summary>
    [Theory]
    [InlineData((int)UpdateCatalogLoadIssue.SourceMissing, VersionSourceStatus.Offline)]
    [InlineData((int)UpdateCatalogLoadIssue.SourceUnavailable, VersionSourceStatus.Offline)]
    [InlineData((int)UpdateCatalogLoadIssue.PermissionDenied, VersionSourceStatus.PermissionDenied)]
    [InlineData((int)UpdateCatalogLoadIssue.UnsafeSource, VersionSourceStatus.Invalid)]
    [InlineData((int)UpdateCatalogLoadIssue.CatalogTooLarge, VersionSourceStatus.Invalid)]
    [InlineData((int)UpdateCatalogLoadIssue.InvalidManifest, VersionSourceStatus.Invalid)]
    [InlineData((int)UpdateCatalogLoadIssue.UnstableRead, VersionSourceStatus.Invalid)]
    [InlineData((int)UpdateCatalogLoadIssue.None, VersionSourceStatus.Invalid)]
    [InlineData(999, VersionSourceStatus.Invalid)]
    public async Task SourceIssuesMapToStableVisibleStatus(
        int issueCode,
        VersionSourceStatus expected)
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            new IssueCatalogSource((UpdateCatalogLoadIssue)issueCode),
            new HealthyRepository());

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, snapshot.SourceStatus);
        Assert.Equal((UpdateCatalogLoadIssue)issueCode, snapshot.CatalogIssue);
    }

    /// <summary>A newer catalog entry remains invalid until complete package verification succeeds.</summary>
    [Fact]
    public async Task UnverifiedNewerPackagePublishesInvalidStatus()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            new FixedCatalogSource(Catalog("0.10.6")),
            new HealthyRepository(verifyPackages: false));

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Invalid, snapshot.SourceStatus);
        Assert.Null(snapshot.VerifiedCandidate);
        Assert.False(snapshot.ShouldPromptForUpdate);
    }

    /// <summary>No configured source short-circuits discovery and remains explicitly NotConfigured.</summary>
    [Fact]
    public async Task MissingConfiguredSourceSkipsCatalogRead()
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.5");
        VersionManagerState initial = VersionManagerState.Create(
            null,
            version,
            version,
            [Admission("0.10.5")],
            null,
            null,
            false,
            managedRootIdentity: "managed-root");
        var source = new CountingCatalogSource();
        using var experience = new VersionManagementExperience(
            version,
            "managed-root",
            new MemoryStateStore(initial),
            source,
            new HealthyRepository());

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.NotConfigured, snapshot.SourceStatus);
        Assert.Equal(0, source.LoadCount);
    }

    /// <summary>Delete adapter failure preserves admission and returns a typed repository issue.</summary>
    [Fact]
    public async Task RepositoryDeleteFailurePreservesInstalledState()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var repository = new HealthyRepository(deleteIssue: ManagedVersionDeleteIssue.DeleteFailed);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionDeleteOperationResult result = await experience.DeleteAsync(
            ManagedAppVersion.Parse("0.10.4"),
            rollbackLossConfirmed: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionDeleteOperationIssue.RepositoryFailure, result.OperationIssue);
        Assert.Equal(ManagedVersionDeleteIssue.DeleteFailed, result.RepositoryIssue);
        Assert.Contains(result.Snapshot.State!.Admissions, item => item.Version == ManagedAppVersion.Parse("0.10.4"));
    }

    /// <summary>Disposal is idempotent and all later use is rejected.</summary>
    [Fact]
    public async Task DisposeIsIdempotentAndRejectsLaterUse()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            new FixedCatalogSource(Catalog("0.10.6")),
            new HealthyRepository());

        experience.Dispose();
        experience.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await experience.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Confirming a new source cancels an old check before it can republish the old path.</summary>
    [Fact]
    public async Task CommittingNewSourceSupersedesInFlightOldSourceCheck()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var source = new SupersedingCatalogSource(Catalog("0.10.7"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            source,
            new HealthyRepository());
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);

        Task<VersionManagementSnapshot> staleCheck = experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken).AsTask();
        _ = await source.FirstCheckStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        VersionManagementSnapshot committed = await experience.CommitUpdateSourceAsync(
            "new-source-root",
            TestContext.Current.CancellationToken);
        _ = await staleCheck;

        Assert.True(source.FirstCheckWasCancelled);
        Assert.Equal("new-source-root", committed.State!.UpdateSource);
        Assert.Equal(ManagedAppVersion.Parse("0.10.7"), Assert.Single(committed.Catalog!.Versions).Version);
        Assert.Equal(VersionSourceStatus.Connected, committed.SourceStatus);
    }

    /// <summary>A failed durable source commit keeps the prior source and never probes the candidate.</summary>
    [Fact]
    public async Task CommitSourceSaveFailurePreservesCommittedSourceAndSkipsCatalog()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 1);
        var source = new CountingCatalogSource();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            source,
            new HealthyRepository());

        VersionManagementSnapshot result = await experience.CommitUpdateSourceAsync(
            "candidate-source-root",
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, result.StateIssue);
        Assert.Equal("source-root", result.State!.UpdateSource);
        Assert.Equal("source-root", stateStore.State.UpdateSource);
        Assert.Equal(1, stateStore.SaveCount);
        Assert.Equal(0, source.LoadCount);
    }

    /// <summary>An interrupted refresh restores the last completed source result instead of remaining Checking.</summary>
    [Theory]
    [InlineData(VersionSourceStatus.Connected, true, (int)UpdateCatalogLoadIssue.None)]
    [InlineData(VersionSourceStatus.Invalid, false, (int)UpdateCatalogLoadIssue.None)]
    [InlineData(VersionSourceStatus.PermissionDenied, true, (int)UpdateCatalogLoadIssue.PermissionDenied)]
    [InlineData(VersionSourceStatus.Offline, true, (int)UpdateCatalogLoadIssue.SourceMissing)]
    [InlineData(VersionSourceStatus.Offline, true, (int)UpdateCatalogLoadIssue.SourceUnavailable)]
    [InlineData(VersionSourceStatus.Invalid, true, (int)UpdateCatalogLoadIssue.UnsafeSource)]
    [InlineData(VersionSourceStatus.Invalid, true, (int)UpdateCatalogLoadIssue.CatalogTooLarge)]
    [InlineData(VersionSourceStatus.Invalid, true, (int)UpdateCatalogLoadIssue.InvalidManifest)]
    [InlineData(VersionSourceStatus.Invalid, true, (int)UpdateCatalogLoadIssue.UnstableRead)]
    [InlineData(VersionSourceStatus.Invalid, true, 999)]
    public async Task MutationInterruptingRefreshRestoresPriorSourceStatus(
        VersionSourceStatus expected,
        bool verifyPackages,
        int issueCode)
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        UpdateCatalogLoadIssue issue = (UpdateCatalogLoadIssue)issueCode;
        UpdateCatalogLoadResult firstResult = issue == UpdateCatalogLoadIssue.None
            ? new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)
            : new(null, issue);
        var source = new CompletedThenBlockingCatalogSource(firstResult);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            source,
            new HealthyRepository(verifyPackages));

        VersionManagementSnapshot completed = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);
        Assert.Equal(expected, completed.SourceStatus);

        Task<VersionManagementSnapshot> interruptedCheck = experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken).AsTask();
        _ = await source.SecondCheckStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        VersionManagementSnapshot restored = await experience.AcknowledgeRetentionReviewAsync(
            TestContext.Current.CancellationToken);
        VersionManagementSnapshot interruptedResult = await interruptedCheck;

        Assert.Equal(expected, restored.SourceStatus);
        Assert.Equal(expected, interruptedResult.SourceStatus);
        Assert.True(source.SecondCheckWasCancelled);
    }

    private static VersionManagerState State(
        IReadOnlyList<ManagedVersionAdmission> admissions,
        string active,
        string lastKnownGood,
        string source = "source-root")
    {
        return VersionManagerState.Create(
            source,
            ManagedAppVersion.Parse(active),
            ManagedAppVersion.Parse(lastKnownGood),
            admissions,
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root");
    }

    private static ManagedVersionAdmission Admission(string version)
    {
        return new(ManagedAppVersion.Parse(version), $"identity-{version}", Hash);
    }

    private static UpdateCatalogSnapshot Catalog(string version)
    {
        var document = new UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                version,
                "2026-08-21T00:00:00Z",
                $"packages/NvtFwCombiner-v{version}-win-x64.zip",
                42,
                Hash,
                Hash,
                $"Release {version}")]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    private sealed class FixedCatalogSource(UpdateCatalogSnapshot snapshot) : IUpdateCatalogSource
    {
        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new UpdateCatalogLoadResult(snapshot, UpdateCatalogLoadIssue.None));
        }
    }

    private sealed class MutableCatalogSource(UpdateCatalogSnapshot snapshot) : IUpdateCatalogSource
    {
        internal UpdateCatalogSnapshot Snapshot { get; set; } = snapshot;

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new UpdateCatalogLoadResult(Snapshot, UpdateCatalogLoadIssue.None));
        }
    }

    private sealed class IssueCatalogSource(UpdateCatalogLoadIssue issue) : IUpdateCatalogSource
    {
        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new UpdateCatalogLoadResult(null, issue));
        }
    }

    private sealed class CountingCatalogSource : IUpdateCatalogSource
    {
        internal int LoadCount { get; private set; }

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            throw new InvalidOperationException("A source-free check must not read the catalog.");
        }
    }

    private sealed class SupersedingCatalogSource(UpdateCatalogSnapshot newSourceSnapshot) : IUpdateCatalogSource
    {
        private int _firstCheckWasCancelled;

        internal TaskCompletionSource<bool> FirstCheckStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool FirstCheckWasCancelled => Volatile.Read(ref _firstCheckWasCancelled) != 0;

        public async ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            if (string.Equals(sourceRoot, "source-root", StringComparison.Ordinal))
            {
                _ = FirstCheckStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _ = Interlocked.Exchange(ref _firstCheckWasCancelled, 1);
                    throw;
                }
            }
            return new(newSourceSnapshot, UpdateCatalogLoadIssue.None);
        }
    }

    private sealed class CompletedThenBlockingCatalogSource(UpdateCatalogLoadResult firstResult)
        : IUpdateCatalogSource
    {
        private int _loadCount;
        private int _secondCheckWasCancelled;

        internal TaskCompletionSource<bool> SecondCheckStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool SecondCheckWasCancelled => Volatile.Read(ref _secondCheckWasCancelled) != 0;

        public async ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _loadCount) == 1)
            {
                return firstResult;
            }

            _ = SecondCheckStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _ = Interlocked.Exchange(ref _secondCheckWasCancelled, 1);
                throw;
            }

            throw new InvalidOperationException("The blocking catalog check must be cancelled by the mutation.");
        }
    }

    private sealed class MemoryStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; } = state;

        internal void ReplaceState(VersionManagerState replacement)
        {
            State = replacement;
        }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(State, VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HealthyRepository(
        bool verifyPackages = true,
        ManagedVersionDeleteIssue deleteIssue = ManagedVersionDeleteIssue.None,
        ManagedPackageVerificationResult? verificationResult = null) : IManagedVersionRepository
    {
        internal List<ManagedAppVersion> Deleted { get; } = [];

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(verificationResult ?? (verifyPackages
                ? new ManagedPackageVerificationResult(
                    new(package.Version, package.Identity, package.ReleaseNotes),
                    ManagedVersionInstallIssue.None)
                : new ManagedPackageVerificationResult(
                    Candidate: null,
                    ManagedVersionInstallIssue.PackageMismatch)));
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ManagedVersionInstallResult(
                new(package.Version, package.Identity, package.ReleaseManifestSha256),
                ManagedVersionInstallIssue.None,
                WasAlreadyInstalled: false));
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ManagedVersionInventoryReadResult.Success(
                ManagedVersionInventory.Create(admissions.Select(admission =>
                    new InstalledVersionSnapshot(
                        admission.Version,
                        admission.AdmissionIdentity,
                        failedActivationVersion == admission.Version
                            ? ManagedVersionIntegrity.Damaged
                            : ManagedVersionIntegrity.Healthy,
                        failedActivationVersion == admission.Version
                            ? ManagedVersionDamageReason.FailedActivation
                            : null,
                        activeVersion == admission.Version,
                        lastKnownGoodVersion == admission.Version)))));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            if (deleteIssue == ManagedVersionDeleteIssue.None)
            {
                Deleted.Add(admission.Version);
            }
            return ValueTask.FromResult(deleteIssue);
        }
    }
}
