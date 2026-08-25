using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>A source check cannot erase an independently unavailable inventory status.</summary>
    [Fact]
    public async Task SourceCheckPreservesUnavailableInventoryIssue()
    {
        ManagedVersionAdmission active = Admission("0.10.5");
        var repository = new TransactionRepository([active])
        {
            InventoryResultOverride = ManagedVersionInventoryReadResult.Unavailable(),
        };
        using var experience = new VersionManagementExperience(
            active.Version,
            "managed-root",
            new MemoryStateStore(State(
                [active],
                active: active.Version.ToString(),
                lastKnownGood: active.Version.ToString())),
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, snapshot.InventoryIssue);
        Assert.Empty(snapshot.Inventory.Versions);
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
        using var experience = new VersionManagementExperience(
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
}
