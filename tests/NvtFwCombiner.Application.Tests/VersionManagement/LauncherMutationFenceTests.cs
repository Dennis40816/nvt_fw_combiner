using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests launcher mutation fences.</summary>
[Collection(nameof(VersionManagementExperienceSerialGroup))]
public sealed partial class LauncherMutationFenceTests
{
    /// <summary>Update-source mutation fails closed while launcher activation is pending.</summary>
    [Fact]
    public async Task LauncherPendingBlocksUpdateSourceMutation()
    {
        var stateStore = new MemoryStateStore(State([Admission("0.10.6")], "0.10.6", "0.10.6"));
        var fence = new RecordingLauncherFence(PendingProtection());
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.7")),
            new HealthyRepository(),
            fence);

        VersionManagementSnapshot result = await experience.CommitUpdateSourceAsync(
            "new-source",
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, result.StateIssue);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Equal(1, fence.LoadCount);
    }

    /// <summary>An exact active launcher owner admission cannot be deleted.</summary>
    [Fact]
    public async Task ActiveLauncherOwnerIsDeleteProtected()
    {
        ManagedVersionAdmission activeApp = Admission("0.10.6");
        ManagedVersionAdmission launcherOwner = Admission("0.10.5");
        var stateStore = new MemoryStateStore(State(
            [activeApp, launcherOwner],
            "0.10.6",
            "0.10.6"));
        var repository = new HealthyRepository();
        var fence = new RecordingLauncherFence(new(
            LauncherMutationFenceIssue.None,
            HasPendingActivation: false,
            launcherOwner,
            launcherOwner,
            PendingOwners: []));
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            activeApp.Version,
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.7")),
            repository,
            fence);

        VersionDeleteOperationResult result = await experience.DeleteAsync(
            launcherOwner.Version,
            rollbackLossConfirmed: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionDeleteBlock.LauncherOwner, result.Decision.Block);
        Assert.Equal(VersionDeleteOperationIssue.PolicyBlocked, result.OperationIssue);
        Assert.Empty(repository.Deleted);
        Assert.Equal(0, fence.RetireCount);
    }

    /// <summary>LKG-only launcher ownership is retired durably after confirmation and before app deletion.</summary>
    [Fact]
    public async Task LastKnownGoodOnlyOwnerRetiresBeforeDelete()
    {
        ManagedVersionAdmission activeApp = Admission("0.10.6");
        ManagedVersionAdmission rollbackOwner = Admission("0.10.5");
        var stateStore = new MemoryStateStore(State(
            [activeApp, rollbackOwner],
            "0.10.6",
            "0.10.6"));
        var repository = new HealthyRepository();
        var fence = new RecordingLauncherFence(new(
            LauncherMutationFenceIssue.None,
            HasPendingActivation: false,
            activeApp,
            rollbackOwner,
            PendingOwners: []));
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            activeApp.Version,
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.7")),
            repository,
            fence);

        VersionDeleteOperationResult warning = await experience.DeleteAsync(
            rollbackOwner.Version,
            rollbackLossConfirmed: false,
            TestContext.Current.CancellationToken);
        VersionDeleteOperationResult deleted = await experience.DeleteAsync(
            rollbackOwner.Version,
            rollbackLossConfirmed: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionDeleteOperationIssue.RollbackConfirmationRequired, warning.OperationIssue);
        Assert.Equal(VersionDeleteOperationIssue.None, deleted.OperationIssue);
        Assert.Equal([rollbackOwner.Version], repository.Deleted);
        Assert.Equal([rollbackOwner], fence.Retired);
    }
}
