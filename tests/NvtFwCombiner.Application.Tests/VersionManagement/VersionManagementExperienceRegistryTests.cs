using NvtFwCombiner.Application.VersionManagement;

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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
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

}
