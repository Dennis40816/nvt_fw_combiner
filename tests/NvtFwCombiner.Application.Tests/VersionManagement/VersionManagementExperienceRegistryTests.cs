using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    private const string FirstRegistryDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string SecondRegistryDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";

    /// <summary>Latest then ordered available is the only automatic candidate order.</summary>
    [Fact]
    public async Task RegistryUsesLatestThenAvailableAndNeverProbesDeprecated()
    {
        string latest = SourcePath("latest");
        string available = SourcePath("available-b");
        string deprecated = SourcePath("deprecated");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var catalogSource = new PathCatalogSource(
            (latest, new(null, UpdateCatalogLoadIssue.SourceUnavailable)),
            (available, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)));
        var registry = new SequenceRegistrySource(Registry(
            revision: 4,
            FirstRegistryDigest,
            (latest, UpdateSourceRegistryEntryStatus.Latest),
            (available, UpdateSourceRegistryEntryStatus.Available),
            (deprecated, UpdateSourceRegistryEntryStatus.Deprecated)));
        var repository = new CountingRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogSource,
            repository,
            registry);

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal([latest, available, available], catalogSource.LoadedRoots);
        Assert.Equal([available, available], repository.VerifiedRoots);
        Assert.DoesNotContain(deprecated, catalogSource.LoadedRoots);
        Assert.Equal(available, stateStore.State.UpdateSource);
        Assert.Equal(new VersionSourceRegistryState(4, FirstRegistryDigest, isManualPin: false),
            stateStore.State.SourceRegistryState);
        Assert.Equal(VersionRegistryStatus.FallbackSelected, result.RegistryStatus);
        Assert.Equal(UpdateSourceRegistryIssue.None, result.RegistryIssue);
    }

    /// <summary>Candidate exhaustion preserves every durable state value.</summary>
    [Fact]
    public async Task AllRegistryCandidatesFailWithoutDurableMutation()
    {
        string latest = SourcePath("latest");
        string available = SourcePath("available");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var registry = new SequenceRegistrySource(Registry(
            revision: 7,
            FirstRegistryDigest,
            (latest, UpdateSourceRegistryEntryStatus.Latest),
            (available, UpdateSourceRegistryEntryStatus.Available)));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource(),
            new HealthyRepository(),
            registry);

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(VersionRegistryStatus.Exhausted, result.RegistryStatus);
        Assert.Equal(UpdateSourceRegistryIssue.CandidatesExhausted, result.RegistryIssue);
    }

    /// <summary>Lower revisions and same-revision byte conflicts fail closed.</summary>
    [Theory]
    [InlineData(3, FirstRegistryDigest, UpdateSourceRegistryIssue.RevisionRollback)]
    [InlineData(5, SecondRegistryDigest, UpdateSourceRegistryIssue.RevisionConflict)]
    public async Task RegistryAntiRollbackRejectsStaleOrConflictingAuthorityWithoutMutation(
        long observedRevision,
        string observedDigest,
        UpdateSourceRegistryIssue expectedIssue)
    {
        string priorSource = SourcePath("prior-source");
        string latest = SourcePath("latest");
        VersionManagerState initial = VersionManagerState.Create(
            priorSource,
            ManagedAppVersion.Parse("0.10.5"),
            ManagedAppVersion.Parse("0.10.5"),
            [Admission("0.10.5")],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root",
            sourceRegistryState: new(5, FirstRegistryDigest, isManualPin: false));
        var stateStore = new MemoryStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            new SequenceRegistrySource(Registry(
                observedRevision,
                observedDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(VersionRegistryStatus.Rejected, result.RegistryStatus);
        Assert.Equal(expectedIssue, result.RegistryIssue);
    }

    /// <summary>Manual source confirmation pins until an explicit successful resume.</summary>
    [Fact]
    public async Task ManualSourceIsDurablyPinnedUntilExplicitResume()
    {
        string manual = SourcePath("manual");
        string registryLatest = SourcePath("registry-latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var registry = new SequenceRegistrySource(Registry(
            revision: 8,
            FirstRegistryDigest,
            (registryLatest, UpdateSourceRegistryEntryStatus.Latest)));
        var catalogs = new PathCatalogSource(
            (manual, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)),
            (registryLatest, new(Catalog("0.10.7"), UpdateCatalogLoadIssue.None)));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            new HealthyRepository(),
            registry);

        VersionManagementSnapshot pinned = await experience.CommitUpdateSourceAsync(
            manual,
            TestContext.Current.CancellationToken);
        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);

        Assert.True(stateStore.State.SourceRegistryState!.IsManualPin);
        Assert.Equal(0, stateStore.State.SourceRegistryState.AcceptedRevision);
        Assert.Null(stateStore.State.SourceRegistryState.AcceptedDigest);
        Assert.Equal(manual, stateStore.State.UpdateSource);
        Assert.Equal(0, registry.LoadCount);
        Assert.Equal(VersionRegistryStatus.ManualPin, pinned.RegistryStatus);

        VersionManagementSnapshot resumed = await experience.ResumeRegistryAsync(
            TestContext.Current.CancellationToken);

        Assert.False(stateStore.State.SourceRegistryState!.IsManualPin);
        Assert.Equal(registryLatest, stateStore.State.UpdateSource);
        Assert.Equal(2, registry.LoadCount);
        Assert.Equal(VersionRegistryStatus.LatestSelected, resumed.RegistryStatus);
    }

    /// <summary>A failed Resume cannot clear its durable manual pin.</summary>
    [Fact]
    public async Task FailedResumePreservesManualPinAndSource()
    {
        string manual = SourcePath("manual");
        string latest = SourcePath("latest");
        VersionManagerState initial = VersionManagerState.Create(
            manual,
            ManagedAppVersion.Parse("0.10.5"),
            ManagedAppVersion.Parse("0.10.5"),
            [Admission("0.10.5")],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root",
            sourceRegistryState: new(0, null, isManualPin: true));
        var stateStore = new MemoryStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource(),
            new HealthyRepository(),
            new SequenceRegistrySource(Registry(
                1,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.ResumeRegistryAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.True(stateStore.State.SourceRegistryState!.IsManualPin);
        Assert.Equal(manual, stateStore.State.UpdateSource);
        Assert.Equal(UpdateSourceRegistryIssue.CandidatesExhausted, result.RegistryIssue);
    }

    /// <summary>Identical accepted registry authority is idempotent.</summary>
    [Fact]
    public async Task SameRevisionAndDigestAreIdempotentWithoutAStateRewrite()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = VersionManagerState.Create(
            latest,
            ManagedAppVersion.Parse("0.10.5"),
            ManagedAppVersion.Parse("0.10.5"),
            [Admission("0.10.5")],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root",
            sourceRegistryState: new(5, FirstRegistryDigest, isManualPin: false));
        var stateStore = new MemoryStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            new SequenceRegistrySource(
                Registry(5, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)),
                Registry(5, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(VersionRegistryStatus.LatestSelected, result.RegistryStatus);
    }

    /// <summary>Deprecated prior source is retained for recovery but never auto-contacted.</summary>
    [Fact]
    public async Task DeprecatedPriorSourceIsRetainedButNeverContactedWhenCandidatesFail()
    {
        string deprecated = SourcePath("deprecated");
        string latest = SourcePath("latest");
        VersionManagerState initial = VersionManagerState.Create(
            deprecated,
            ManagedAppVersion.Parse("0.10.5"),
            ManagedAppVersion.Parse("0.10.5"),
            [Admission("0.10.5")],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root");
        var stateStore = new MemoryStateStore(initial);
        var catalogs = new PathCatalogSource();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            new HealthyRepository(),
            new SequenceRegistrySource(Registry(
                1,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest),
                (deprecated, UpdateSourceRegistryEntryStatus.Deprecated))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal([latest], catalogs.LoadedRoots);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Equal(deprecated, stateStore.State.UpdateSource);
        Assert.Equal(VersionRegistryStatus.DeprecatedRetained, result.RegistryStatus);
        Assert.Equal(UpdateSourceRegistryIssue.CurrentSourceDeprecated, result.RegistryIssue);
    }

    /// <summary>Atomic state-save failure leaves the prior authority durable.</summary>
    [Fact]
    public async Task StateSaveFailureAfterReadmissionLeavesDurableAuthorityUnchanged()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new FailingStateStore(initial, failOnSave: 1);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            new SequenceRegistrySource(
                Registry(1, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)),
                Registry(1, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Same(initial, stateStore.State);
        Assert.Equal(1, stateStore.SaveCount);
        Assert.Equal(UpdateSourceRegistryIssue.StateUnavailable, result.RegistryIssue);
    }

    /// <summary>Any non-newest catalog publication change rejects the commit.</summary>
    [Fact]
    public async Task NonNewestCatalogChangeDuringLeaseReadmissionRejectsCommit()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var catalogs = new SequencePathCatalogSource(
            latest,
            CatalogWithOlderReleaseNote("first"),
            CatalogWithOlderReleaseNote("changed"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            new HealthyRepository(),
            new SequenceRegistrySource(
                Registry(1, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)),
                Registry(1, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(UpdateSourceRegistryIssue.RegistryChanged, result.RegistryIssue);
    }

    /// <summary>ADR 0056 pending launcher state fences registry commits.</summary>
    [Fact]
    public async Task PendingLauncherMutationFenceBlocksRegistryCommitEvenWhenLeaseIsFree()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            new SequenceRegistrySource(
                Registry(1, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)),
                Registry(1, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest))),
            new DenyingMutationFence());

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(UpdateSourceRegistryIssue.StateUnavailable, result.RegistryIssue);
    }

    /// <summary>A changed registry during re-admission cannot commit.</summary>
    [Fact]
    public async Task RegistryChangingDuringCommitIsRejectedWithoutStateMutation()
    {
        string latest = SourcePath("latest");
        string other = SourcePath("other");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        var registry = new SequenceRegistrySource(
            Registry(9, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)),
            Registry(10, SecondRegistryDigest, (other, UpdateSourceRegistryEntryStatus.Latest)));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            registry);

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(UpdateSourceRegistryIssue.RegistryChanged, result.RegistryIssue);
    }

    /// <summary>Registry read failure is typed and leaves the prior source intact.</summary>
    [Fact]
    public async Task RegistryReadFailureIsTypedAndPreservesPriorSource()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource(),
            new HealthyRepository(),
            new SequenceRegistrySource(new UpdateSourceRegistryLoadResult(
                Snapshot: null,
                UpdateSourceRegistryLoadIssue.PermissionDenied)));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(VersionRegistryStatus.Unavailable, result.RegistryStatus);
        Assert.Equal(UpdateSourceRegistryIssue.PermissionDenied, result.RegistryIssue);
    }

    /// <summary>Writer contention after read-only admission performs no mutation.</summary>
    [Fact]
    public async Task BusyWriterLeaseAfterCandidateAdmissionCausesZeroMutation()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new SequencedLeaseStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            new SequenceRegistrySource(Registry(
                9,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
        Assert.Equal(UpdateSourceRegistryIssue.StateUnavailable, result.RegistryIssue);
        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, result.StateIssue);
    }

    /// <summary>A durable state change while waiting for the lease rejects the registry commit.</summary>
    [Fact]
    public async Task DurableStateChangeDuringReadmissionCausesZeroMutation()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        VersionManagerState changed = initial.WithRetentionReviewDue(retentionReviewDue: true);
        var stateStore = new ReloadChangedStateStore(initial, changed);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(),
            new SequenceRegistrySource(Registry(
                9,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(changed, stateStore.State);
        Assert.Equal(UpdateSourceRegistryIssue.StateUnavailable, result.RegistryIssue);
    }

    /// <summary>Self-test inspects every automatic source in order without consuming state or prompt generation.</summary>
    [Fact]
    public async Task EnvironmentSelfTestIsReadOnlyOrderedAndSkipsDeprecated()
    {
        string latest = SourcePath("latest");
        string available = SourcePath("available");
        string deprecated = SourcePath("deprecated");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LeaseCountingStateStore(initial);
        var catalogs = new PathCatalogSource(
            (latest, new(null, UpdateCatalogLoadIssue.SourceUnavailable)),
            (available, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)));
        var repository = new CountingRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            repository,
            new SequenceRegistrySource(Registry(
                12,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest),
                (deprecated, UpdateSourceRegistryEntryStatus.Deprecated),
                (available, UpdateSourceRegistryEntryStatus.Available))));
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        int leaseRequestsBeforeSelfTest = stateStore.LeaseRequestCount;

        VersionEnvironmentSelfTestResult result = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([latest, available], result.Attempts.Select(attempt => attempt.SourceRoot));
        Assert.Equal(
            [UpdateSourceRegistryEntryStatus.Latest, UpdateSourceRegistryEntryStatus.Available],
            result.Attempts.Select(attempt => attempt.Status));
        Assert.False(result.Attempts[0].IsVerified);
        Assert.True(result.Attempts[1].IsVerified);
        Assert.Equal([available], repository.VerifiedRoots);
        Assert.DoesNotContain(deprecated, catalogs.LoadedRoots);
        Assert.Equal(leaseRequestsBeforeSelfTest, stateStore.LeaseRequestCount);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);

        VersionManagementSnapshot checkedResult = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, checkedResult.Generation);
    }

    /// <summary>Self-test reuses normal newest-package admission and never scans historical packages.</summary>
    [Fact]
    public async Task EnvironmentSelfTestReusesNewestPackageAdmission()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LeaseCountingStateStore(initial);
        var repository = new CountingRepository(ManagedAppVersion.Parse("0.10.5"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(
                CatalogWithOlderReleaseNote("Release 0.10.5"),
                UpdateCatalogLoadIssue.None))),
            repository,
            new SequenceRegistrySource(Registry(
                12,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionEnvironmentSelfTestResult result = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);

        VersionEnvironmentSelfTestAttempt attempt = Assert.Single(result.Attempts);
        Assert.True(result.IsSuccess);
        Assert.True(attempt.IsVerified);
        Assert.Equal(ManagedAppVersion.Parse("0.10.6"), attempt.NewestVersion);
        Assert.Equal(ManagedVersionInstallIssue.None, attempt.PackageIssue);
        Assert.Equal([ManagedAppVersion.Parse("0.10.6")], repository.VerifiedVersions);
        Assert.Equal(0, stateStore.LeaseRequestCount);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    /// <summary>A maximum-size valid catalog still admits only its canonical newest package.</summary>
    [Fact]
    public async Task EnvironmentSelfTestAcceptsMaximumCatalogWithoutHistoricalPackageScan()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LeaseCountingStateStore(initial);
        var repository = new CountingRepository(ManagedAppVersion.Parse("0.10.1"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(
                CatalogWithVersionCount(UpdateCatalogValidator.MaximumVersionCount),
                UpdateCatalogLoadIssue.None))),
            repository,
            new SequenceRegistrySource(Registry(
                12,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        VersionEnvironmentSelfTestResult result = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);

        VersionEnvironmentSelfTestAttempt attempt = Assert.Single(result.Attempts);
        Assert.True(result.IsSuccess);
        Assert.Equal(ManagedAppVersion.Parse("0.10.128"), attempt.NewestVersion);
        Assert.Equal([ManagedAppVersion.Parse("0.10.128")], repository.VerifiedVersions);
        Assert.Equal(0, stateStore.LeaseRequestCount);
        Assert.Equal(0, stateStore.SaveCount);
    }

    /// <summary>Self-test and normal resolution reject the same mismatched typed candidate.</summary>
    [Fact]
    public async Task EnvironmentSelfTestAndCheckShareCandidateMismatchRejection()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LeaseCountingStateStore(initial);
        var repository = new HealthyRepository(verificationResult: new(
            new(
                ManagedAppVersion.Parse("0.10.7"),
                "identity-0.10.7",
                "Release 0.10.7"),
            ManagedVersionInstallIssue.None));
        UpdateSourceRegistryLoadResult registry = Registry(
            12,
            FirstRegistryDigest,
            (latest, UpdateSourceRegistryEntryStatus.Latest));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            repository,
            new SequenceRegistrySource(registry, registry));

        VersionEnvironmentSelfTestResult selfTest = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);
        VersionManagementSnapshot check = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        VersionEnvironmentSelfTestAttempt attempt = Assert.Single(selfTest.Attempts);
        Assert.False(selfTest.IsSuccess);
        Assert.False(attempt.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, attempt.PackageIssue);
        Assert.Equal(UpdateSourceRegistryIssue.CandidatesExhausted, check.RegistryIssue);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    /// <summary>Cancellation during an actual source read propagates without touching durable state.</summary>
    [Fact]
    public async Task EnvironmentSelfTestMidReadCancellationPerformsZeroMutation()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LeaseCountingStateStore(initial);
        var catalogs = new CancelFirstCatalogSource(Catalog("0.10.6"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            new HealthyRepository(),
            new SequenceRegistrySource(Registry(
                12,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));
        using var cancellation = new CancellationTokenSource();

        Task<VersionEnvironmentSelfTestResult> checking = experience.RunEnvironmentSelfTestAsync(
            cancellation.Token).AsTask();
        await catalogs.FirstLoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checking);
        Assert.Equal(0, stateStore.LeaseRequestCount);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    /// <summary>Self-test observes caller cancellation without performing any read or mutation.</summary>
    [Fact]
    public async Task EnvironmentSelfTestCancellationIsPropagatedWithoutMutation()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LeaseCountingStateStore(initial);
        var registry = new SequenceRegistrySource(Registry(
            1,
            FirstRegistryDigest,
            (SourcePath("latest"), UpdateSourceRegistryEntryStatus.Latest)));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource(),
            new HealthyRepository(),
            registry);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await experience.RunEnvironmentSelfTestAsync(cancellation.Token));

        Assert.Equal(0, registry.LoadCount);
        Assert.Equal(0, stateStore.LeaseRequestCount);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    /// <summary>Caller cancellation restores the prior snapshot and releases the owned check.</summary>
    [Fact]
    public async Task RegistryCheckCallerCancellationAllowsCleanRetry()
    {
        string latest = SourcePath("latest");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5",
            source: latest);
        var stateStore = new MemoryStateStore(initial);
        var catalogs = new CancelFirstCatalogSource(Catalog("0.10.6"));
        UpdateSourceRegistryLoadResult registry = Registry(
            1,
            FirstRegistryDigest,
            (latest, UpdateSourceRegistryEntryStatus.Latest));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            new HealthyRepository(),
            new SequenceRegistrySource(registry, registry, registry));
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        Task<VersionManagementSnapshot> cancelled = experience.CheckAsync(
            isAutomatic: false,
            cancellation.Token).AsTask();
        await catalogs.FirstLoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        VersionManagementSnapshot retried = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Connected, retried.SourceStatus);
        Assert.Equal(VersionRegistryStatus.LatestSelected, retried.RegistryStatus);
        Assert.Equal(1, stateStore.SaveCount);
    }

    /// <summary>The legacy/manual source path uses the same caller-cancellation cleanup owner.</summary>
    [Fact]
    public async Task DirectSourceCheckCallerCancellationAllowsCleanRetry()
    {
        string source = SourcePath("manual");
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5",
            source: source);
        var stateStore = new MemoryStateStore(initial);
        var catalogs = new CancelFirstCatalogSource(Catalog("0.10.6"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            new HealthyRepository());
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        Task<VersionManagementSnapshot> cancelled = experience.CheckAsync(
            isAutomatic: false,
            cancellation.Token).AsTask();
        await catalogs.FirstLoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        VersionManagementSnapshot retried = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Connected, retried.SourceStatus);
        Assert.Equal(0, stateStore.SaveCount);
    }

    /// <summary>Until the immutable locator is injected, self-test reports typed NotConfigured.</summary>
    [Fact]
    public async Task EnvironmentSelfTestWithoutInjectedRegistryIsTypedNotConfigured()
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new MemoryStateStore(initial);
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new PathCatalogSource(),
            new HealthyRepository());

        VersionEnvironmentSelfTestResult result = await experience.RunEnvironmentSelfTestAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateSourceRegistryLoadIssue.NotConfigured, result.RegistryIssue);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    /// <summary>A retained automatic source cannot bypass its missing fixed Registry authority.</summary>
    [Fact]
    public async Task AutomaticRegistryStateWithoutInjectedLocatorFailsClosedWithoutSourceContact()
    {
        string automatic = SourcePath("automatic");
        VersionManagerState initial = VersionManagerState.Create(
            automatic,
            ManagedAppVersion.Parse("0.10.5"),
            ManagedAppVersion.Parse("0.10.5"),
            [Admission("0.10.5")],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root",
            sourceRegistryState: new(4, FirstRegistryDigest, isManualPin: false));
        var stateStore = new MemoryStateStore(initial);
        var catalogs = new PathCatalogSource(
            (automatic, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)));
        var repository = new CountingRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            repository);
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.NotConfigured, result.SourceStatus);
        Assert.Equal(VersionRegistryStatus.NotConfigured, result.RegistryStatus);
        Assert.Equal(UpdateSourceRegistryIssue.NotConfigured, result.RegistryIssue);
        Assert.Null(result.Catalog);
        Assert.Null(result.VerifiedCandidate);
        Assert.False(result.ShouldPromptForUpdate);
        Assert.Empty(catalogs.LoadedRoots);
        Assert.Empty(repository.VerifiedRoots);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    /// <summary>An explicit manual pin remains usable without the automatic Registry locator.</summary>
    [Fact]
    public async Task ManualPinWithoutInjectedLocatorChecksOnlyItsCommittedSource()
    {
        string manual = SourcePath("manual");
        VersionManagerState initial = VersionManagerState.Create(
            manual,
            ManagedAppVersion.Parse("0.10.5"),
            ManagedAppVersion.Parse("0.10.5"),
            [Admission("0.10.5")],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root",
            sourceRegistryState: new(0, null, isManualPin: true));
        var stateStore = new MemoryStateStore(initial);
        var catalogs = new PathCatalogSource(
            (manual, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)));
        var repository = new CountingRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            catalogs,
            repository);
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);

        VersionManagementSnapshot result = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Connected, result.SourceStatus);
        Assert.Equal(VersionRegistryStatus.ManualPin, result.RegistryStatus);
        Assert.Equal(UpdateSourceRegistryIssue.None, result.RegistryIssue);
        Assert.Equal([manual], catalogs.LoadedRoots);
        Assert.Equal([manual], repository.VerifiedRoots);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Same(initial, stateStore.State);
    }

    private static UpdateSourceRegistryLoadResult Registry(
        long revision,
        string digest,
        params (string Path, UpdateSourceRegistryEntryStatus Status)[] entries)
    {
        return new(
            new UpdateSourceRegistrySnapshot(
                revision,
                digest,
                [.. entries.Select(entry => new UpdateSourceRegistryEntry(entry.Path, entry.Status))]),
            UpdateSourceRegistryLoadIssue.None);
    }

    private static string SourcePath(string name)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine("registry-sources", name)));
    }

    private static UpdateCatalogSnapshot CatalogWithOlderReleaseNote(string olderNote)
    {
        var document = new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [
                new(
                    "0.10.6",
                    "2026-08-21T00:00:00Z",
                    "packages/NvtFwCombiner-v0.10.6-win-x64.zip",
                    42,
                    Hash,
                    Hash,
                    "Release 0.10.6"),
                new(
                    "0.10.5",
                    "2026-08-20T00:00:00Z",
                    "packages/NvtFwCombiner-v0.10.5-win-x64.zip",
                    41,
                    Hash,
                    Hash,
                    olderNote),
            ]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    private static UpdateCatalogSnapshot CatalogWithVersionCount(int count)
    {
        var document = new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [.. Enumerable.Range(1, count)
                .Select(index => new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogVersionDocument(
                    $"0.10.{index}",
                    "2026-08-21T00:00:00Z",
                    $"packages/NvtFwCombiner-v0.10.{index}-win-x64.zip",
                    42,
                    Hash,
                    Hash,
                    $"Release 0.10.{index}"))]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    private sealed class SequenceRegistrySource(params UpdateSourceRegistryLoadResult[] results)
        : IUpdateSourceRegistry
    {
        private int _next;

        internal int LoadCount => _next;

        public ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            int index = Math.Min(Interlocked.Increment(ref _next) - 1, results.Length - 1);
            return ValueTask.FromResult(results[index]);
        }
    }

    private sealed class PathCatalogSource(
        params (string Path, UpdateCatalogLoadResult Result)[] results) : IUpdateCatalogSource
    {
        private readonly Dictionary<string, UpdateCatalogLoadResult> _results =
            results.ToDictionary(entry => entry.Path, entry => entry.Result, StringComparer.Ordinal);

        internal List<string> LoadedRoots { get; } = [];

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            LoadedRoots.Add(sourceRoot);
            return ValueTask.FromResult(_results.TryGetValue(sourceRoot, out UpdateCatalogLoadResult? result)
                ? result
                : new UpdateCatalogLoadResult(null, UpdateCatalogLoadIssue.SourceMissing));
        }
    }

    private sealed class SequencePathCatalogSource(
        string path,
        params UpdateCatalogSnapshot[] snapshots) : IUpdateCatalogSource
    {
        private int _next;

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            Assert.Equal(path, sourceRoot);
            int index = Math.Min(Interlocked.Increment(ref _next) - 1, snapshots.Length - 1);
            return ValueTask.FromResult(new UpdateCatalogLoadResult(
                snapshots[index],
                UpdateCatalogLoadIssue.None));
        }
    }

    private sealed class CancelFirstCatalogSource(UpdateCatalogSnapshot snapshot)
        : IUpdateCatalogSource
    {
        private int _loadCount;

        internal TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _loadCount) == 1)
            {
                _ = FirstLoadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return new(snapshot, UpdateCatalogLoadIssue.None);
        }
    }

    private sealed class CountingRepository(ManagedAppVersion? mismatchedVersion = null)
        : IManagedVersionRepository
    {
        private readonly HealthyRepository _inner = new();

        internal List<string> VerifiedRoots { get; } = [];

        internal List<ManagedAppVersion> VerifiedVersions { get; } = [];

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            VerifiedRoots.Add(sourceRoot);
            VerifiedVersions.Add(package.Version);
            return package.Version == mismatchedVersion
                ? ValueTask.FromResult(new ManagedPackageVerificationResult(
                    Candidate: null,
                    ManagedVersionInstallIssue.PackageMismatch))
                : _inner.VerifyPackageAsync(sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return _inner.InstallAsync(managedRoot, sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            return _inner.InventoryAsync(
                managedRoot,
                admissions,
                activeVersion,
                lastKnownGoodVersion,
                failedActivationVersion,
                cancellationToken);
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            return _inner.DeleteAsync(managedRoot, admission, activeVersion, cancellationToken);
        }
    }

    private sealed class LeaseCountingStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        internal int LeaseRequestCount { get; private set; }

        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; } = state;

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            LeaseRequestCount++;
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                State,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SequencedLeaseStateStore(VersionManagerState state)
        : IVersionManagerStateStore
    {
        private int _leaseCount;

        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; } = state;

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Interlocked.Increment(ref _leaseCount) == 1
                ? VersionManagerWriteLeaseTestSupport.Acquired()
                : VersionManagerWriteLeaseTestSupport.Busy());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                State,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DenyingMutationFence : IVersionManagementMutationFence
    {
        public ValueTask<bool> CanMutateAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class ReloadChangedStateStore : IVersionManagerStateStore
    {
        private readonly VersionManagerState _initial;
        private readonly VersionManagerState _changed;
        private int _loadCount;

        internal ReloadChangedStateStore(
            VersionManagerState initial,
            VersionManagerState changed)
        {
            _initial = initial;
            _changed = changed;
            State = initial;
        }

        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            State = Interlocked.Increment(ref _loadCount) == 1 ? _initial : _changed;
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                State,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }
}
