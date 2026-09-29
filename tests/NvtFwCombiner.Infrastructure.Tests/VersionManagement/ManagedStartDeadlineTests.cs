using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the shared terminal-result boundary used by both managed process adapters.</summary>
public sealed class ManagedStartDeadlineTests
{
    /// <summary>Cancellation cannot hide an accepted child from its caller.</summary>
    [Fact]
    public async Task CallerCancellationDoesNotDiscardAlreadyAcceptedReady()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = new ManagedStartDeadline(TimeSpan.FromSeconds(5), caller.Token);
        Task<ManagedProcessStartResult> start = deadline.RunAsync<ManagedProcessStartResult>(async () =>
        {
            Assert.True(deadline.TryBeginCreation());
            entered.SetResult();
            await release.Task;
            return new(ManagedProcessStartOutcome.Ready, null);
        },
        static () => new(ManagedProcessStartOutcome.ReadyTimeout, null),
        static () => new(ManagedProcessStartOutcome.TerminationUnconfirmed, null),
        caller.Token);
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            caller.Cancel();
            release.SetResult();

            ManagedProcessStartResult result = await start.WaitAsync(
                TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(ManagedProcessStartOutcome.Ready, result.Outcome);
        }
        finally
        {
            _ = release.TrySetResult();
            _ = await start.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A cancelled worker still propagates cancellation after it owns cleanup.</summary>
    [Fact]
    public async Task CallerCancellationPreservesWorkerCancellationAfterCreation()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = new ManagedStartDeadline(TimeSpan.FromSeconds(5), caller.Token);
        Task<ManagedProcessStartResult> start = deadline.RunAsync<ManagedProcessStartResult>(async () =>
        {
            Assert.True(deadline.TryBeginCreation());
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, caller.Token);
            return new(ManagedProcessStartOutcome.Ready, null);
        },
        static () => new(ManagedProcessStartOutcome.ReadyTimeout, null),
        static () => new(ManagedProcessStartOutcome.TerminationUnconfirmed, null),
        caller.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        caller.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await start.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
    }

    /// <summary>An unfinished native creation yields an existing fail-closed outcome.</summary>
    [Fact]
    public async Task CallerCancellationCannotWaitForeverForUnfinishedCreation()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = new ManagedStartDeadline(TimeSpan.FromSeconds(10), caller.Token);
        Task<ManagedProcessStartResult> start = deadline.RunAsync<ManagedProcessStartResult>(async () =>
        {
            try
            {
                Assert.True(deadline.TryBeginCreation());
                entered.SetResult();
                await release.Task;
                return new(ManagedProcessStartOutcome.ReadyTimeout, null);
            }
            finally
            {
                workerExited.SetResult();
            }
        },
        static () => new(ManagedProcessStartOutcome.ReadyTimeout, null),
        static () => new(ManagedProcessStartOutcome.TerminationUnconfirmed, null),
        caller.Token);
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            caller.Cancel();

            ManagedProcessStartResult result = await start.WaitAsync(
                ManagedProcessTermination.DefaultWaitTimeout + TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Equal(ManagedProcessStartOutcome.TerminationUnconfirmed, result.Outcome);
        }
        finally
        {
            _ = release.TrySetResult();
            await workerExited.Task.WaitAsync(
                TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }
}
