using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>Update-source mutation fails closed while launcher activation is pending.</summary>
    [Fact]
    public async Task LauncherPendingBlocksUpdateSourceMutation()
    {
        var stateStore = new MemoryStateStore(State([Admission("0.10.6")], "0.10.6", "0.10.6"));
        var fence = new RecordingLauncherFence(PendingProtection());
        using var experience = new VersionManagementExperience(
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
        using var experience = new VersionManagementExperience(
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
        using var experience = new VersionManagementExperience(
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

    private static LauncherMutationProtection PendingProtection()
    {
        return new(
            LauncherMutationFenceIssue.None,
            HasPendingActivation: true,
            ActiveOwner: Admission("0.10.5"),
            LastKnownGoodOwner: Admission("0.10.5"),
            PendingOwners: [Admission("0.10.6")]);
    }

    private sealed class RecordingLauncherFence(LauncherMutationProtection protection)
        : ILauncherMutationFence
    {
        public int LoadCount { get; private set; }
        public int RetireCount => Retired.Count;
        public List<ManagedVersionAdmission> Retired { get; } = [];

        public ValueTask<LauncherMutationProtection> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            return ValueTask.FromResult(protection);
        }

        public ValueTask<LauncherMutationFenceIssue> RetireLastKnownGoodOwnerAsync(
            ManagedVersionAdmission expectedOwner,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Retired.Add(expectedOwner);
            protection = protection with { LastKnownGoodOwner = protection.ActiveOwner };
            return ValueTask.FromResult(LauncherMutationFenceIssue.None);
        }
    }
}
