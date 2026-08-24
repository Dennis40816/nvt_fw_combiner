using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
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
