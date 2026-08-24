using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>A promoted install with a failed commit save is admitted on the next process start.</summary>
    [Fact]
    public async Task InstallCommitSaveFailureConvergesFromDurableJournalAfterRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 2);
        var repository = new TransactionRepository(initial.Admissions);
        using (var first = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository))
        {
            _ = await first.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
            VersionInstallOperationResult interrupted = await first.InstallAsync(
                ManagedAppVersion.Parse("0.10.6"),
                TestContext.Current.CancellationToken);
            Assert.Equal(ManagedVersionInstallIssue.StateUnavailable, interrupted.Install.Issue);
            Assert.Equal(ManagedVersionMutationKind.Install, stateStore.State.PendingMutation?.Kind);
        }

        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.Contains(recovered.State.Admissions, admission =>
            admission.Version == ManagedAppVersion.Parse("0.10.6"));
        Assert.Equal(2, recovered.Inventory.HealthyCount);
    }

    /// <summary>A completed delete with a failed commit save removes its admission after restart.</summary>
    [Fact]
    public async Task DeleteCommitSaveFailureConvergesFromDurableJournalAfterRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 2);
        var repository = new TransactionRepository(initial.Admissions);
        using (var first = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository))
        {
            VersionDeleteOperationResult interrupted = await first.DeleteAsync(
                ManagedAppVersion.Parse("0.10.4"),
                rollbackLossConfirmed: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionDeleteOperationIssue.StateUnavailable, interrupted.OperationIssue);
            Assert.Equal(ManagedVersionMutationKind.Delete, stateStore.State.PendingMutation?.Kind);
        }

        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, admission =>
            admission.Version == ManagedAppVersion.Parse("0.10.4"));
        Assert.Null(recovered.Inventory.Find(ManagedAppVersion.Parse("0.10.4")));
    }

    /// <summary>An unadmitted SemVer directory has a typed recovery block and never reaches admission lookup.</summary>
    [Fact]
    public async Task UnadmittedDirectoryDeleteReturnsRecoveryBlockWithoutCrashing()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var repository = new TransactionRepository(initial.Admissions, unadmittedVersion: "0.10.4");
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

        Assert.Equal(VersionDeleteOperationIssue.PolicyBlocked, result.OperationIssue);
        Assert.Equal(ManagedVersionDeleteBlock.RecoveryRequired, result.Decision.Block);
        Assert.Equal(1, result.Snapshot.Inventory.UnadmittedCount);
    }

    /// <summary>A valid self-admission without the exact pending install remains unadmitted.</summary>
    [Fact]
    public async Task ValidSelfAdmissionWithoutPendingInstallRemainsUnadmitted()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var repository = new TransactionRepository(
            [.. initial.Admissions, Admission("0.10.6")]);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(initial),
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot snapshot = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        InstalledVersionSnapshot observed = Assert.IsType<InstalledVersionSnapshot>(
            snapshot.Inventory.Find(ManagedAppVersion.Parse("0.10.6")));
        Assert.Equal(ManagedVersionAdmissionState.Unadmitted, observed.AdmissionState);
        Assert.Equal(Admission("0.10.6"), observed.ObservedAdmission);
        Assert.Null(snapshot.State!.PendingMutation);
    }

    /// <summary>A failed install-prepare save prevents the repository mutation from starting.</summary>
    [Fact]
    public async Task InstallPrepareSaveFailureStartsNoRepositoryMutation()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 1);
        var repository = new TransactionRepository(initial.Admissions);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        VersionInstallOperationResult result = await experience.InstallAsync(
            ManagedAppVersion.Parse("0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.StateUnavailable, result.Install.Issue);
        Assert.Equal(0, repository.InstallCalls);
        Assert.Null(stateStore.State.PendingMutation);
    }

    /// <summary>A confirmed install from an obsolete source generation fails typed before any mutation.</summary>
    [Fact]
    public async Task SourceChangedAfterConfirmationReturnsPackageUnavailableWithoutMutation()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var repository = new TransactionRepository(initial.Admissions);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        stateStore.ReplaceState(State(
            initial.Admissions,
            active: "0.10.5",
            lastKnownGood: "0.10.5",
            source: "other-source"));

        VersionInstallOperationResult result = await experience.InstallAsync(
            ManagedAppVersion.Parse("0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.PackageUnavailable, result.Install.Issue);
        Assert.Equal("other-source", result.Snapshot.State!.UpdateSource);
        Assert.Equal(0, repository.InstallCalls);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Null(stateStore.State.PendingMutation);
    }

    /// <summary>A newer catalog generation that removes the requested version fails before mutation.</summary>
    [Fact]
    public async Task CurrentCatalogWithoutRequestedVersionReturnsPackageUnavailableWithoutMutation()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var repository = new TransactionRepository(initial.Admissions);
        var source = new MutableCatalogSource(Catalog("0.10.6"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            source,
            repository);
        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        source.Snapshot = Catalog("0.10.7");
        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);

        VersionInstallOperationResult result = await experience.InstallAsync(
            ManagedAppVersion.Parse("0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.PackageUnavailable, result.Install.Issue);
        Assert.Equal(0, repository.InstallCalls);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Null(stateStore.State.PendingMutation);
    }

    /// <summary>An exact admitted retry is idempotent and never opens a second filesystem transaction.</summary>
    [Fact]
    public async Task ExactAlreadyAdmittedInstallReturnsSuccessWithoutMutation()
    {
        UpdateCatalogSnapshot catalog = Catalog("0.10.5");
        UpdateCatalogVersionSnapshot package = Assert.Single(catalog.Versions);
        var admitted = new ManagedVersionAdmission(
            package.Version,
            package.Identity,
            package.ReleaseManifestSha256);
        VersionManagerState initial = State(
            [admitted],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var repository = new TransactionRepository(initial.Admissions);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(catalog),
            repository);

        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        VersionInstallOperationResult result = await experience.InstallAsync(
            package.Version,
            TestContext.Current.CancellationToken);

        Assert.True(result.Install.IsSuccess);
        Assert.True(result.Install.WasAlreadyInstalled);
        Assert.Equal(0, repository.InstallCalls);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Null(result.Snapshot.State!.PendingMutation);
    }

    /// <summary>An admitted version with another catalog identity fails closed without a journal.</summary>
    [Fact]
    public async Task ConflictingAlreadyAdmittedInstallReturnsIdentityConflictWithoutMutation()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var repository = new TransactionRepository(initial.Admissions);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.5")),
            repository);

        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        VersionInstallOperationResult result = await experience.InstallAsync(
            ManagedAppVersion.Parse("0.10.5"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.IdentityConflict, result.Install.Issue);
        Assert.Equal(0, repository.InstallCalls);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Null(result.Snapshot.State!.PendingMutation);
    }

    /// <summary>A restart after install prepare but before filesystem mutation clears an absent target.</summary>
    [Fact]
    public async Task PreparedInstallWithoutPayloadClearsOnRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        ManagedVersionAdmission pending = Admission("0.10.6");
        VersionManagerState prepared = initial.WithPendingMutation(
            new(ManagedVersionMutationKind.Install, pending));
        var stateStore = new MemoryStateStore(prepared);
        var repository = new TransactionRepository(initial.Admissions);
        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, item => item.Version == pending.Version);
        Assert.Equal(1, stateStore.SaveCount);
        Assert.Equal(0, repository.InstallCalls);
    }

    /// <summary>A repository failure plus failed journal clear is safely cleared by the next restart.</summary>
    [Fact]
    public async Task FailedInstallClearSaveFailureConvergesAfterRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 2);
        var repository = new TransactionRepository(initial.Admissions)
        {
            InstallIssue = ManagedVersionInstallIssue.PromotionFailed,
        };
        using (var first = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository))
        {
            _ = await first.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
            VersionInstallOperationResult interrupted = await first.InstallAsync(
                ManagedAppVersion.Parse("0.10.6"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ManagedVersionInstallIssue.StateUnavailable, interrupted.Install.Issue);
            Assert.Equal(ManagedVersionMutationKind.Install, stateStore.State.PendingMutation?.Kind);
            Assert.Equal(1, repository.InstallCalls);
        }

        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, item =>
            item.Version == ManagedAppVersion.Parse("0.10.6"));
    }

    /// <summary>A failed recovery commit remains journaled and the following restart admits the exact payload.</summary>
    [Fact]
    public async Task InstallRecoveryCommitSaveFailureRemainsJournaledUntilNextRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        ManagedVersionAdmission pending = Admission("0.10.6");
        VersionManagerState prepared = initial.WithPendingMutation(
            new(ManagedVersionMutationKind.Install, pending));
        var stateStore = new FailingStateStore(prepared, failOnSave: 1);
        var repository = new TransactionRepository([.. initial.Admissions, pending]);
        using (var firstRestart = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository))
        {
            VersionManagementSnapshot stillPrepared = await firstRestart.InitializeAsync(
                TestContext.Current.CancellationToken);
            Assert.NotNull(stillPrepared.State!.PendingMutation);
            Assert.DoesNotContain(stillPrepared.State.Admissions, item => item.Version == pending.Version);
        }

        using var secondRestart = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await secondRestart.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.Contains(recovered.State.Admissions, item => item == pending);
    }

    /// <summary>A promoted payload with another identity never self-admits from an install journal.</summary>
    [Fact]
    public async Task InstallRecoveryIdentityMismatchStaysBlocked()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        ManagedVersionAdmission pending = Admission("0.10.6");
        var observed = new ManagedVersionAdmission(
            pending.Version,
            "unexpected-identity",
            new string('b', 64));
        VersionManagerState prepared = initial.WithPendingMutation(
            new(ManagedVersionMutationKind.Install, pending));
        var stateStore = new MemoryStateStore(prepared);
        var repository = new TransactionRepository([.. initial.Admissions, observed]);
        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot blocked = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(pending, blocked.State!.PendingMutation?.Admission);
        Assert.DoesNotContain(blocked.State.Admissions, item => item.Version == pending.Version);
        Assert.Equal(ManagedVersionAdmissionState.Unadmitted, blocked.Inventory.Find(pending.Version)?.AdmissionState);
        Assert.Equal(0, stateStore.SaveCount);
    }

    /// <summary>A failed delete-prepare save prevents the repository mutation from starting.</summary>
    [Fact]
    public async Task DeletePrepareSaveFailureStartsNoRepositoryMutation()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 1);
        var repository = new TransactionRepository(initial.Admissions);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionDeleteOperationResult result = await experience.DeleteAsync(
            ManagedAppVersion.Parse("0.10.4"),
            rollbackLossConfirmed: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionDeleteOperationIssue.StateUnavailable, result.OperationIssue);
        Assert.Equal(0, repository.DeleteCalls);
        Assert.Null(stateStore.State.PendingMutation);
    }

    /// <summary>An initial delete treats an already absent exact target as committed.</summary>
    [Fact]
    public async Task InitialJournaledDeleteCommitsWhenTargetAlreadyDisappeared()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var repository = new TransactionRepository(initial.Admissions)
        {
            DeleteIssue = ManagedVersionDeleteIssue.NotInstalled,
        };
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionDeleteOperationResult result = await experience.DeleteAsync(
            ManagedAppVersion.Parse("0.10.4"),
            rollbackLossConfirmed: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionDeleteOperationIssue.None, result.OperationIssue);
        Assert.Equal(ManagedVersionDeleteIssue.NotInstalled, result.RepositoryIssue);
        Assert.DoesNotContain(result.Snapshot.State!.Admissions, admission =>
            admission.Version == ManagedAppVersion.Parse("0.10.4"));
        Assert.Null(result.Snapshot.State.PendingMutation);
        Assert.Equal(2, stateStore.SaveCount);
    }

    /// <summary>An absent delete target converges after its commit save fails and the app restarts.</summary>
    [Fact]
    public async Task AlreadyAbsentDeleteCommitSaveFailureConvergesOnRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 2);
        var repository = new TransactionRepository(initial.Admissions)
        {
            DeleteIssue = ManagedVersionDeleteIssue.NotInstalled,
        };
        using (var first = new VersionManagementExperience(
                   ManagedAppVersion.Parse("0.10.5"),
                   "managed-root",
                   stateStore,
                   new FixedCatalogSource(Catalog("0.10.6")),
                   repository))
        {
            VersionDeleteOperationResult interrupted = await first.DeleteAsync(
                ManagedAppVersion.Parse("0.10.4"),
                rollbackLossConfirmed: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionDeleteOperationIssue.StateUnavailable, interrupted.OperationIssue);
            Assert.Equal(ManagedVersionMutationKind.Delete, stateStore.State.PendingMutation?.Kind);
        }

        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, admission =>
            admission.Version == ManagedAppVersion.Parse("0.10.4"));
        Assert.Equal(2, repository.DeleteCalls);
    }

    /// <summary>A restart after delete prepare but before mutation performs the exact guarded delete.</summary>
    [Fact]
    public async Task PreparedDeleteExecutesAndCommitsOnRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        ManagedVersionAdmission pending = initial.Admissions.Single(item =>
            item.Version == ManagedAppVersion.Parse("0.10.4"));
        VersionManagerState prepared = initial.WithPendingMutation(
            new(ManagedVersionMutationKind.Delete, pending));
        var stateStore = new MemoryStateStore(prepared);
        var repository = new TransactionRepository(initial.Admissions);
        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, item => item.Version == pending.Version);
        Assert.Equal(1, repository.DeleteCalls);
        Assert.Equal(1, stateStore.SaveCount);
    }

    /// <summary>A delete failure plus failed journal clear retries the exact delete after restart.</summary>
    [Fact]
    public async Task FailedDeleteClearSaveFailureRetriesAndConvergesAfterRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 2);
        var repository = new TransactionRepository(initial.Admissions)
        {
            DeleteIssue = ManagedVersionDeleteIssue.DeleteFailed,
        };
        using (var first = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository))
        {
            VersionDeleteOperationResult interrupted = await first.DeleteAsync(
                ManagedAppVersion.Parse("0.10.4"),
                rollbackLossConfirmed: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionDeleteOperationIssue.StateUnavailable, interrupted.OperationIssue);
            Assert.Equal(ManagedVersionMutationKind.Delete, stateStore.State.PendingMutation?.Kind);
            Assert.Equal(1, repository.DeleteCalls);
        }

        repository.DeleteIssue = ManagedVersionDeleteIssue.None;
        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, item =>
            item.Version == ManagedAppVersion.Parse("0.10.4"));
        Assert.Equal(2, repository.DeleteCalls);
    }

    /// <summary>A failed delete recovery commit stays journaled and converges after another restart.</summary>
    [Fact]
    public async Task DeleteRecoveryCommitSaveFailureRemainsJournaledUntilNextRestart()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        ManagedVersionAdmission pending = initial.Admissions.Single(item =>
            item.Version == ManagedAppVersion.Parse("0.10.4"));
        VersionManagerState prepared = initial.WithPendingMutation(
            new(ManagedVersionMutationKind.Delete, pending));
        var stateStore = new FailingStateStore(prepared, failOnSave: 1);
        var repository = new TransactionRepository(
            initial.Admissions.Where(item => item.Version != pending.Version));
        using (var firstRestart = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository))
        {
            VersionManagementSnapshot stillPrepared = await firstRestart.InitializeAsync(
                TestContext.Current.CancellationToken);
            Assert.NotNull(stillPrepared.State!.PendingMutation);
        }

        using var secondRestart = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        VersionManagementSnapshot recovered = await secondRestart.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(recovered.State!.PendingMutation);
        Assert.DoesNotContain(recovered.State.Admissions, item => item.Version == pending.Version);
        Assert.Equal(2, repository.DeleteCalls);
    }

    /// <summary>A transient recovery delete failure preserves the journal for a later retry.</summary>
    [Fact]
    public async Task DeleteRecoveryFailurePreservesPreparedJournal()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5"), Admission("0.10.4")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        ManagedVersionAdmission pending = initial.Admissions.Single(item =>
            item.Version == ManagedAppVersion.Parse("0.10.4"));
        VersionManagerState prepared = initial.WithPendingMutation(
            new(ManagedVersionMutationKind.Delete, pending));
        var stateStore = new MemoryStateStore(prepared);
        var repository = new TransactionRepository(initial.Admissions)
        {
            DeleteIssue = ManagedVersionDeleteIssue.DeleteFailed,
        };
        using var restarted = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot blocked = await restarted.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(pending, blocked.State!.PendingMutation?.Admission);
        Assert.Equal(1, repository.DeleteCalls);
        Assert.Equal(0, stateStore.SaveCount);
    }
}
