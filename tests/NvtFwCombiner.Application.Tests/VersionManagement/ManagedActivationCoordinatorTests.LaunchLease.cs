using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class ManagedActivationCoordinatorTests
{
    /// <summary>An executable lease failure starts nothing and does not create a durable launch tombstone.</summary>
    [Fact]
    public async Task ExecutableLeaseFailureOccursBeforeLaunchJournalWrite()
    {
        VersionManagerState original = State();
        var store = new FakeStateStore(original);
        var process = new FakeProcess(ManagedProcessStartOutcome.Ready);
        var repository = new HealthyRepository
        {
            LaunchLeaseIssue = ManagedExecutableLaunchIssue.Unavailable,
        };

        ManagedLauncherResult result = await new ManagedActivationCoordinator(
            "managed",
            store,
            repository,
            process).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.StateUnavailable, result.Outcome);
        Assert.Empty(process.Starts);
        Assert.Same(original, store.State);
    }
}
