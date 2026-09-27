namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>The headless session guard fails the process only when the session loop has died.</summary>
public sealed class HeadlessSessionLoopGuardTests
{
    /// <summary>A faulted loop reports its cause, the dispatcher owner and the running tests in one failure.</summary>
    [Fact]
    public async Task FaultedSessionLoopFailsWithItsCause()
    {
        var loop = new TaskCompletionSource();
        var failures = new List<(string Message, Exception? Cause)>();
        Task watch = HeadlessSessionLoopGuard.FailWhenFaulted(
            loop.Task,
            (message, cause) => failures.Add((message, cause)));
        var setupFailure = new InvalidOperationException(
            "The calling thread cannot access this object because a different thread owns it.");

        Assert.True(loop.TrySetException(setupFailure));
        await watch;

        (string message, Exception? cause) = Assert.Single(failures);
        Assert.Same(setupFailure, cause);
        Assert.Contains("headless session loop faulted", message, StringComparison.Ordinal);
        Assert.Contains("BUG-20260927-uismoke-headless-session-stall", message, StringComparison.Ordinal);
        Assert.Contains("Avalonia UI dispatcher thread: ", message, StringComparison.Ordinal);
        Assert.Contains(
            $"{TestContext.Current.Test!.TestDisplayName} on managed ",
            message,
            StringComparison.Ordinal);
        Assert.Contains(setupFailure.Message, message, StringComparison.Ordinal);
    }

    /// <summary>A loop that completes or is canceled never fails the process.</summary>
    /// <param name="ending">How the loop ends.</param>
    [Theory]
    [InlineData("completed")]
    [InlineData("canceled")]
    public async Task EndedSessionLoopDoesNotFail(string ending)
    {
        var loop = new TaskCompletionSource();
        int failures = 0;
        Task watch = HeadlessSessionLoopGuard.FailWhenFaulted(loop.Task, (_, _) => failures++);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.True(ending == "completed" ? loop.TrySetResult() : loop.TrySetCanceled(cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watch);

        Assert.Equal(0, failures);
    }
}
