using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Verifies the workflow-neutral selected-file inspection lifecycle.</summary>
public sealed class WorkflowInspectionLifecycleTests
{
    private static readonly ShellTextResources Text = ShellTextResources.For(ShellLanguage.English);

    /// <summary>Cancel targets the latest queued selection and a later request starts with a fresh generation.</summary>
    [Fact]
    public async Task NewSelectionCancelsAndRejectsTheOlderAttempt()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCleaned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<AuthoringInspectionProgress>? firstProgress = null;
        CancellationToken firstCancellation = default;

        Task<WorkflowInspectionAttemptState> first = lifecycle.StartAsync(
            Text,
            async (progress, _, cancellationToken) =>
            {
                firstProgress = progress;
                firstCancellation = cancellationToken;
                progress.Report(new(0, 2));
                firstStarted.SetResult();
                await releaseFirst.Task;
                return new(true);
            },
            TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task<WorkflowInspectionAttemptState> second = lifecycle.StartAsync(
            Text,
            async (_, _, cancellationToken) =>
            {
                secondEntered.SetResult();
                try
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                    return new(true);
                }
                finally
                {
                    secondCleaned.SetResult();
                }
            },
            TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(
            () => firstCancellation.IsCancellationRequested,
            TimeSpan.FromSeconds(5)));
        Assert.False(secondEntered.Task.IsCompleted);
        Task cancel = lifecycle.CancelAsync(TestContext.Current.CancellationToken);
        Assert.False(cancel.IsCompleted);
        _ = releaseFirst.TrySetResult();
        await Task.WhenAll(first, second, cancel);

        IProgress<AuthoringInspectionProgress> retained =
            Assert.IsType<IProgress<AuthoringInspectionProgress>>(firstProgress, exactMatch: false);
        _ = Assert.ThrowsAny<OperationCanceledException>(() => retained.Report(new(1, 2)));
        Assert.True(secondEntered.Task.IsCompletedSuccessfully);
        Assert.True(secondCleaned.Task.IsCompletedSuccessfully);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, await first);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, await second);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, lifecycle.State);
        Assert.False(lifecycle.Loading.IsVisible);

        WorkflowInspectionAttemptState completed = await lifecycle.StartAsync(
            Text,
            (progress, _, _) =>
            {
                progress.Report(new(1, 1));
                return Task.FromResult(new WorkflowInspectionOperationResult(true));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, lifecycle.State);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, completed);
        Assert.Equal(new AuthoringInspectionProgress(1, 1), lifecycle.Progress);
        Assert.False(lifecycle.Loading.IsVisible);

        WorkflowInspectionAttemptState stale = await lifecycle.StartAsync(
            Text,
            static (_, _, _) => Task.FromException<WorkflowInspectionOperationResult>(
                new OperationCanceledException("The selected inspection is stale.")),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, lifecycle.State);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, stale);
        Assert.False(lifecycle.Loading.IsVisible);
    }

    /// <summary>Retry drains a failed request and allocates a never-reused generation.</summary>
    [Fact]
    public async Task RetryDrainsTheFailureAndUsesANewGeneration()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        int invocation = 0;

        Task<WorkflowInspectionOperationResult> Execute(
            IProgress<AuthoringInspectionProgress> progress,
            Func<bool> isCurrent,
            CancellationToken cancellationToken)
        {
            invocation++;
            if (invocation is 1 or 3)
            {
                return Task.FromResult(new WorkflowInspectionOperationResult(
                    false,
                    "input.artifact.read-failed"));
            }
            progress.Report(new(1, 1));
            return Task.FromResult(new WorkflowInspectionOperationResult(true));
        }

        Assert.Equal(
            WorkflowInspectionAttemptState.Failed,
            await lifecycle.StartAsync(
                Text,
                Execute,
                TestContext.Current.CancellationToken));

        Assert.Equal(WorkflowInspectionAttemptState.Failed, lifecycle.State);
        Assert.True(lifecycle.Loading.CanRetry);
        await lifecycle.Loading.RetryCommand!.ExecuteAsync(null);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, lifecycle.State);
        Assert.Equal(2, invocation);

        Assert.Equal(
            WorkflowInspectionAttemptState.Failed,
            await lifecycle.StartAsync(
                Text,
                Execute,
                TestContext.Current.CancellationToken));
        lifecycle.Invalidate();
        lifecycle.ApplyText(ShellTextResources.For(ShellLanguage.ChineseTraditional));

        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, lifecycle.State);
        Assert.False(lifecycle.Loading.IsVisible);
        Assert.False(lifecycle.Loading.CanRetry);
        await lifecycle.Loading.RetryCommand!.ExecuteAsync(null);
        Assert.Equal(3, invocation);
    }

    /// <summary>Progress keeps exact monotonic work units through the terminal.</summary>
    [Fact]
    public async Task ProgressIsMonotonicAndRetainsExactWorkUnits()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        var observed = new List<(int? Completed, int? Total)>();
        IProgress<AuthoringInspectionProgress>? retainedProgress = null;

        Assert.Equal(
            WorkflowInspectionAttemptState.Succeeded,
            await lifecycle.StartAsync(
                Text,
                (progress, _, _) =>
                {
                    retainedProgress = progress;
                    for (int completed = 0; completed <= 3; completed++)
                    {
                        progress.Report(new(completed, 3));
                        observed.Add((lifecycle.Progress?.CompletedWork, lifecycle.Progress?.TotalWork));
                    }
                    return Task.FromResult(new WorkflowInspectionOperationResult(true));
                },
                TestContext.Current.CancellationToken));

        Assert.Equal([(0, 3), (1, 3), (2, 3), (3, 3)], observed);
        Assert.Equal(lifecycle.Progress?.TotalWork, lifecycle.Progress?.CompletedWork);
        Assert.Empty(lifecycle.Loading.ProgressPercentLabel);
        IProgress<AuthoringInspectionProgress> retained = Assert.IsType<IProgress<AuthoringInspectionProgress>>(
            retainedProgress,
            exactMatch: false);
        _ = Assert.Throws<InvalidOperationException>(() => retained.Report(new(3, 3)));
        Assert.Equal(new AuthoringInspectionProgress(3, 3), lifecycle.Progress);
    }

    /// <summary>Active progress relocalizes and a successor starts without the predecessor's percentage.</summary>
    [Fact]
    public async Task ActivePresentationRelocalizesWithoutLosingExactProgress()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<WorkflowInspectionAttemptState> active = lifecycle.StartAsync(
            Text,
            async (progress, _, cancellationToken) =>
            {
                progress.Report(new(1, 2));
                reported.SetResult();
                await release.Task.WaitAsync(cancellationToken);
                return new(true);
            },
            TestContext.Current.CancellationToken);
        await reported.Task.WaitAsync(TestContext.Current.CancellationToken);

        lifecycle.Loading.SetReducedMotion(true);
        lifecycle.ApplyText(ShellTextResources.For(ShellLanguage.ChineseTraditional));

        Assert.Equal("正在檢查所選檔案", lifecycle.Loading.Title);
        Assert.Equal("已檢查 1 / 2 個檔案", lifecycle.Loading.Detail);
        Assert.Equal("50%", lifecycle.Loading.ProgressPercentLabel);
        Assert.False(lifecycle.Loading.ShouldAnimate);
        var successorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSuccessor = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<WorkflowInspectionAttemptState> successor = lifecycle.StartAsync(
            Text,
            async (_, _, cancellationToken) =>
            {
                successorStarted.SetResult();
                await releaseSuccessor.Task.WaitAsync(cancellationToken);
                return new(true);
            },
            TestContext.Current.CancellationToken);
        await successorStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Null(lifecycle.Loading.Progress);
        Assert.Empty(lifecycle.Loading.ProgressPercentLabel);
        Assert.False(lifecycle.Loading.ShouldAnimate);
        await lifecycle.Loading.CancelCommand!.ExecuteAsync(null);

        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, lifecycle.State);
        Assert.False(lifecycle.Loading.IsVisible);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, await active);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, await successor);
    }

    /// <summary>Malformed progress cannot be presented as a successful inspection.</summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    public async Task InvalidProgressFailsClosed(int completed, int total)
    {
        var lifecycle = new WorkflowInspectionLifecycle();

        Assert.Equal(
            WorkflowInspectionAttemptState.Failed,
            await lifecycle.StartAsync(
                Text,
                (progress, _, _) =>
                {
                    progress.Report(new(completed, total));
                    return Task.FromResult(new WorkflowInspectionOperationResult(true));
                },
                TestContext.Current.CancellationToken));

        Assert.Equal(WorkflowInspectionAttemptState.Failed, lifecycle.State);
        Assert.True(lifecycle.Loading.CanRetry);
    }
}
