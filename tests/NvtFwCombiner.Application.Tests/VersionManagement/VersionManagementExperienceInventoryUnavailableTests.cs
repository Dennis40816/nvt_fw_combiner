using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>Only a truly missing state may bootstrap an observed empty inventory.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Invalid)]
    [InlineData(VersionManagerStateLoadIssue.Unavailable)]
    public async Task UnusableStatePublishesUnavailableInventoryInsteadOfObservedEmpty(
        VersionManagerStateLoadIssue stateIssue)
    {
        var repository = new TransactionRepository([]);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new LoadResultStateStore(new(null, stateIssue)),
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot snapshot = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(stateIssue, snapshot.StateIssue);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, snapshot.InventoryIssue);
        Assert.Empty(snapshot.Inventory.Versions);
        Assert.Equal(0, repository.InventoryCalls);
    }

    /// <summary>A validated state bound to another root cannot publish an observed empty inventory.</summary>
    [Fact]
    public async Task ManagedRootMismatchPublishesUnavailableInventoryInsteadOfObservedEmpty()
    {
        VersionManagerState foreign = VersionManagerState.Create(
            updateSource: null,
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "foreign-root");
        var repository = new TransactionRepository([]);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new LoadResultStateStore(new(foreign, VersionManagerStateLoadIssue.None)),
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot snapshot = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.ManagedRootMismatch, snapshot.StateIssue);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, snapshot.InventoryIssue);
        Assert.Empty(snapshot.Inventory.Versions);
        Assert.Equal(0, repository.InventoryCalls);
    }

    /// <summary>An unusable reload cannot retain a prior catalog, candidate, or prompt.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Invalid)]
    [InlineData(VersionManagerStateLoadIssue.Unavailable)]
    public async Task UnusableReloadClearsPriorSourceFactsAndPrompt(
        VersionManagerStateLoadIssue stateIssue)
    {
        VersionManagerState initial = State(
            [Admission("0.10.5")],
            active: "0.10.5",
            lastKnownGood: "0.10.5");
        var stateStore = new LoadResultStateStore(new(
            initial,
            VersionManagerStateLoadIssue.None));
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            new HealthyRepository());

        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        VersionManagementSnapshot prior = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);
        Assert.NotNull(prior.Catalog);
        Assert.NotNull(prior.VerifiedCandidate);
        Assert.True(prior.ShouldPromptForUpdate);

        stateStore.Result = new(null, stateIssue);
        VersionManagementSnapshot unavailable = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(stateIssue, unavailable.StateIssue);
        Assert.Null(unavailable.Catalog);
        Assert.Null(unavailable.VerifiedCandidate);
        Assert.False(unavailable.ShouldPromptForUpdate);
        Assert.Equal(VersionSourceStatus.Offline, unavailable.SourceStatus);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, unavailable.InventoryIssue);
    }

    /// <summary>A contended writer lease clears prior source facts and blocks later checks.</summary>
    [Fact]
    public async Task WriterLeaseUnavailableClearsPriorPromptAndBlocksCatalogProbe()
    {
        ManagedVersionAdmission active = Admission("0.10.5");
        var stateStore = new LoadResultStateStore(new(
            State([active], active.Version.ToString(), active.Version.ToString()),
            VersionManagerStateLoadIssue.None));
        var catalogSource = new RecordingCatalogSource(Catalog("0.10.6"));
        var repository = new TransactionRepository([active]);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            active.Version,
            "managed-root",
            stateStore,
            catalogSource,
            repository);

        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        VersionManagementSnapshot prior = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);
        Assert.True(prior.ShouldPromptForUpdate);
        Assert.Equal(1, catalogSource.LoadCalls);
        Assert.Equal(1, repository.VerifyPackageCalls);

        stateStore.LeaseAvailable = false;
        VersionManagementSnapshot unavailable = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);
        VersionManagementSnapshot checkedAgain = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, unavailable.StateIssue);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, unavailable.InventoryIssue);
        Assert.Empty(unavailable.Inventory.Versions);
        Assert.Null(unavailable.Catalog);
        Assert.Null(unavailable.VerifiedCandidate);
        Assert.False(unavailable.ShouldPromptForUpdate);
        Assert.Equal(VersionSourceStatus.Offline, unavailable.SourceStatus);
        Assert.Equal(unavailable.StateIssue, checkedAgain.StateIssue);
        Assert.Equal(unavailable.InventoryIssue, checkedAgain.InventoryIssue);
        Assert.Null(checkedAgain.Catalog);
        Assert.Null(checkedAgain.VerifiedCandidate);
        Assert.False(checkedAgain.ShouldPromptForUpdate);
        Assert.Equal(VersionSourceStatus.Offline, checkedAgain.SourceStatus);
        Assert.Equal(1, catalogSource.LoadCalls);
        Assert.Equal(1, repository.VerifyPackageCalls);
    }

    /// <summary>A recovered same-source catalog never republishes a prompt derived from stale durable state.</summary>
    [Fact]
    public async Task WriterContentionRecoveryClearsPromptAfterDurableStateChanges()
    {
        ManagedVersionAdmission priorActive = Admission("0.10.5");
        ManagedVersionAdmission newActive = Admission("0.10.6");
        var stateStore = new LoadResultStateStore(new(
            State(
                [priorActive],
                active: priorActive.Version.ToString(),
                lastKnownGood: priorActive.Version.ToString()),
            VersionManagerStateLoadIssue.None));
        var catalogSource = new RecordingCatalogSource(Catalog("0.10.6"));
        var repository = new TransactionRepository([priorActive, newActive]);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            priorActive.Version,
            "managed-root",
            stateStore,
            catalogSource,
            repository);

        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        VersionManagementSnapshot checkedSnapshot = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);
        Assert.True(checkedSnapshot.ShouldPromptForUpdate);

        stateStore.LeaseAvailable = false;
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        stateStore.Result = new(
            State(
                [priorActive, newActive],
                active: newActive.Version.ToString(),
                lastKnownGood: priorActive.Version.ToString()),
            VersionManagerStateLoadIssue.None);
        stateStore.LeaseAvailable = true;

        VersionManagementSnapshot recovered = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(newActive.Version, recovered.State!.ActiveVersion);
        Assert.NotNull(recovered.Catalog);
        Assert.Null(recovered.VerifiedCandidate);
        Assert.Equal(VersionSourceStatus.Offline, recovered.SourceStatus);
        Assert.Equal(0, recovered.Generation);
        Assert.False(recovered.ShouldPromptForUpdate);
        Assert.Equal(1, catalogSource.LoadCalls);
        Assert.Equal(1, repository.VerifyPackageCalls);
    }

    /// <summary>A missing state retains the intentional first-run empty-state bootstrap.</summary>
    [Fact]
    public async Task MissingStateBootstrapsObservedEmptyInventory()
    {
        var repository = new TransactionRepository([]);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new LoadResultStateStore(new(null, VersionManagerStateLoadIssue.Missing)),
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot snapshot = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.None, snapshot.StateIssue);
        Assert.Equal(ManagedVersionInventoryReadIssue.None, snapshot.InventoryIssue);
        Assert.NotNull(snapshot.State);
        Assert.Empty(snapshot.Inventory.Versions);
        Assert.Equal(1, repository.InventoryCalls);
    }

    /// <summary>A source check cannot erase an independently unavailable inventory status.</summary>
    [Fact]
    public async Task SourceCheckPreservesUnavailableInventoryIssue()
    {
        ManagedVersionAdmission active = Admission("0.10.5");
        var repository = new TransactionRepository([active])
        {
            InventoryResultOverride = ManagedVersionInventoryReadResult.Unavailable(),
        };
        var catalogSource = new RecordingCatalogSource(Catalog("0.10.6"));
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            active.Version,
            "managed-root",
            new MemoryStateStore(State(
                [active],
                active: active.Version.ToString(),
                lastKnownGood: active.Version.ToString())),
            catalogSource,
            repository);

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, snapshot.InventoryIssue);
        Assert.Empty(snapshot.Inventory.Versions);
        Assert.Null(snapshot.Catalog);
        Assert.Null(snapshot.VerifiedCandidate);
        Assert.False(snapshot.ShouldPromptForUpdate);
        Assert.Equal(VersionSourceStatus.Offline, snapshot.SourceStatus);
        Assert.Equal(0, catalogSource.LoadCalls);
        Assert.Equal(0, repository.VerifyPackageCalls);
    }

    /// <summary>An unavailable inventory preserves either prepared filesystem journal without mutation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableInventoryRetainsPreparedMutationJournal(bool isDelete)
    {
        ManagedVersionAdmission active = Admission("0.10.5");
        ManagedVersionAdmission target = Admission("0.10.6");
        ManagedVersionMutationKind kind = isDelete
            ? ManagedVersionMutationKind.Delete
            : ManagedVersionMutationKind.Install;
        IReadOnlyList<ManagedVersionAdmission> admissions = isDelete
            ? [active, target]
            : [active];
        VersionManagerState prepared = State(
                admissions,
                active: active.Version.ToString(),
                lastKnownGood: active.Version.ToString())
            .WithPendingMutation(new(kind, target));
        var stateStore = new MemoryStateStore(prepared);
        var repository = new TransactionRepository(admissions)
        {
            InventoryResultOverride = ManagedVersionInventoryReadResult.Unavailable(),
        };
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            active.Version,
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.7")),
            repository);

        VersionManagementSnapshot snapshot = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, snapshot.InventoryIssue);
        Assert.Empty(snapshot.Inventory.Versions);
        Assert.Equal(kind, snapshot.State!.PendingMutation?.Kind);
        Assert.Equal(target, snapshot.State.PendingMutation?.Admission);
        Assert.Equal(kind, stateStore.State.PendingMutation?.Kind);
        Assert.Equal(target, stateStore.State.PendingMutation?.Admission);
        Assert.True(repository.InventoryCalls > 0);
        Assert.Equal(0, repository.InstallCalls);
        Assert.Equal(0, repository.DeleteCalls);
        Assert.Equal(0, stateStore.SaveCount);
    }

    /// <summary>An unavailable inventory cannot acknowledge or persist a retention review.</summary>
    [Fact]
    public async Task UnavailableInventoryBlocksRetentionReviewAcknowledgement()
    {
        ManagedVersionAdmission active = Admission("0.10.5");
        VersionManagerState initial = State(
                [active],
                active: active.Version.ToString(),
                lastKnownGood: active.Version.ToString())
            .WithRetentionReviewDue(retentionReviewDue: true);
        var stateStore = new MemoryStateStore(initial);
        var repository = new TransactionRepository([active])
        {
            InventoryResultOverride = ManagedVersionInventoryReadResult.Unavailable(),
        };
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            active.Version,
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot snapshot = await experience.AcknowledgeRetentionReviewAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, snapshot.InventoryIssue);
        Assert.True(snapshot.State!.RetentionReviewDue);
        Assert.True(stateStore.State.RetentionReviewDue);
        Assert.Equal(0, stateStore.SaveCount);
    }

    private sealed class LoadResultStateStore(VersionManagerStateLoadResult result)
        : IVersionManagerStateStore
    {
        internal VersionManagerStateLoadResult Result { get; set; } = result;

        internal bool LeaseAvailable { get; set; } = true;

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(LeaseAvailable
                ? VersionManagerWriteLeaseTestSupport.Acquired()
                : VersionManagerWriteLeaseTestSupport.Busy());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result);
        }

        public ValueTask SaveAsync(VersionManagerState state, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The load-only state store must not be mutated.");
        }
    }

    private sealed class RecordingCatalogSource(UpdateCatalogSnapshot snapshot)
        : IRootCatalogSourceTestDouble
    {
        internal int LoadCalls { get; private set; }

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            LoadCalls++;
            return ValueTask.FromResult(new UpdateCatalogLoadResult(
                snapshot,
                UpdateCatalogLoadIssue.None));
        }
    }
}
