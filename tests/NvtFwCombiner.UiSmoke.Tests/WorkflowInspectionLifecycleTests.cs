using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Verifies the workflow-neutral selected-file inspection lifecycle.</summary>
public sealed class WorkflowInspectionLifecycleTests
{
    private static readonly ShellTextResources Text = ShellTextResources.For(ShellLanguage.English);

    /// <summary>New selection cancels and permanently rejects the prior generation.</summary>
    [Fact]
    public async Task NewSelectionCancelsAndRejectsTheOlderAttempt()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<AuthoringInspectionProgress>? firstProgress = null;
        CancellationToken firstCancellation = default;

        Task first = lifecycle.StartAsync(
            Text,
            async (progress, _, cancellationToken) =>
            {
                firstProgress = progress;
                firstCancellation = cancellationToken;
                progress.Report(new(0, 2));
                firstStarted.SetResult();
                await releaseFirst.Task;
                cancellationToken.ThrowIfCancellationRequested();
            },
            TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task second = lifecycle.StartAsync(
            Text,
            (progress, _, _) =>
            {
                secondStarted.SetResult();
                progress.Report(new(0, 1));
                progress.Report(new(1, 1));
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(
            () => firstCancellation.IsCancellationRequested,
            TimeSpan.FromSeconds(5)));
        Assert.False(secondStarted.Task.IsCompleted);
        _ = releaseFirst.TrySetResult();
        await Task.WhenAll(first, second);

        IProgress<AuthoringInspectionProgress> retained =
            Assert.IsType<IProgress<AuthoringInspectionProgress>>(firstProgress, exactMatch: false);
        _ = Assert.ThrowsAny<OperationCanceledException>(() => retained.Report(new(1, 2)));
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, lifecycle.State);
        Assert.Equal(2, lifecycle.Generation);
        Assert.Equal(1, lifecycle.CompletedWork);
        Assert.Equal(1, lifecycle.TotalWork);
        Assert.False(lifecycle.Loading.IsVisible);
    }

    /// <summary>Retry drains a failed request and allocates a never-reused generation.</summary>
    [Fact]
    public async Task RetryDrainsTheFailureAndUsesANewGeneration()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        int invocation = 0;

        Task Execute(
            IProgress<AuthoringInspectionProgress> progress,
            Func<bool> isCurrent,
            CancellationToken cancellationToken)
        {
            invocation++;
            if (invocation == 1)
            {
                return Task.FromException(new IOException("controlled inspection failure"));
            }
            progress.Report(new(1, 1));
            return Task.CompletedTask;
        }

        await lifecycle.StartAsync(
            Text,
            Execute,
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowInspectionAttemptState.Failed, lifecycle.State);
        Assert.Equal(1, lifecycle.Generation);
        Assert.True(lifecycle.Loading.CanRetry);
        await lifecycle.Loading.RetryCommand!.ExecuteAsync(null);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, lifecycle.State);
        Assert.Equal(2, lifecycle.Generation);
        Assert.Equal(2, invocation);
    }

    /// <summary>Progress keeps exact monotonic work units through the terminal.</summary>
    [Fact]
    public async Task ProgressIsMonotonicAndRetainsExactWorkUnits()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        var observed = new List<(int? Completed, int? Total)>();

        await lifecycle.StartAsync(
            Text,
            (progress, _, _) =>
            {
                for (int completed = 0; completed <= 3; completed++)
                {
                    progress.Report(new(completed, 3));
                    observed.Add((lifecycle.CompletedWork, lifecycle.TotalWork));
                }
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal([(0, 3), (1, 3), (2, 3), (3, 3)], observed);
        Assert.Equal(lifecycle.TotalWork, lifecycle.CompletedWork);
        Assert.Empty(lifecycle.Loading.ProgressPercentLabel);
    }

    /// <summary>Active progress relocalizes and reduced motion keeps the exact static value.</summary>
    [Fact]
    public async Task ActivePresentationRelocalizesWithoutLosingExactProgress()
    {
        var lifecycle = new WorkflowInspectionLifecycle();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task active = lifecycle.StartAsync(
            Text,
            async (progress, _, cancellationToken) =>
            {
                progress.Report(new(1, 2));
                reported.SetResult();
                await release.Task.WaitAsync(cancellationToken);
            },
            TestContext.Current.CancellationToken);
        await reported.Task.WaitAsync(TestContext.Current.CancellationToken);

        lifecycle.Loading.SetReducedMotion(true);
        lifecycle.ApplyText(ShellTextResources.For(ShellLanguage.ChineseTraditional));

        Assert.Equal("正在檢查所選檔案", lifecycle.Loading.Title);
        Assert.Equal("已檢查 1 / 2 個檔案", lifecycle.Loading.Detail);
        Assert.Equal("50%", lifecycle.Loading.ProgressPercentLabel);
        Assert.False(lifecycle.Loading.ShouldAnimate);
        await lifecycle.Loading.CancelCommand!.ExecuteAsync(null);

        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, lifecycle.State);
        Assert.False(lifecycle.Loading.IsVisible);
        Assert.True(active.IsCompletedSuccessfully);
    }

    /// <summary>Malformed progress cannot be presented as a successful inspection.</summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    public async Task InvalidProgressFailsClosed(int completed, int total)
    {
        var lifecycle = new WorkflowInspectionLifecycle();

        await lifecycle.StartAsync(
            Text,
            (progress, _, _) =>
            {
                progress.Report(new(completed, total));
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowInspectionAttemptState.Failed, lifecycle.State);
        Assert.True(lifecycle.Loading.CanRetry);
    }
}
