using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>Automatic recovery leaves the exact app mutation durable while launcher activation is pending.</summary>
    [Fact]
    public async Task LauncherPendingBlocksAutomaticDeleteRecovery()
    {
        ManagedVersionAdmission active = Admission("0.10.6");
        ManagedVersionAdmission deleting = Admission("0.10.5");
        VersionManagerState state = State([active, deleting], "0.10.6", "0.10.6")
            .WithPendingMutation(new(ManagedVersionMutationKind.Delete, deleting));
        var stateStore = new MemoryStateStore(state);
        var repository = new HealthyRepository();
        var fence = new RecordingLauncherFence(PendingProtection());
        using var experience = new VersionManagementExperience(
            active.Version,
            "managed-root",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.7")),
            repository,
            fence);

        VersionManagementSnapshot result = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, result.StateIssue);
        Assert.Empty(repository.Deleted);
        Assert.Equal(0, stateStore.SaveCount);
        Assert.Equal(state, stateStore.State);
    }
}
