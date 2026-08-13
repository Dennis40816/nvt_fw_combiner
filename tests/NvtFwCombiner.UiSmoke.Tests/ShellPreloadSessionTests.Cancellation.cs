using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellPreloadSessionTests
{
    /// <summary>Optional cancellation drains only remaining preload work and permits an explicit later retry.</summary>
    [Fact]
    public async Task OptionalCancellationDrainsRunningStagesWithoutCancellingCatalog()
    {
        using ShellPreloadSession session = CreateSession();
        session.AdoptReadyCatalog();
        TaskCompletionSource diagnosticsStarted = NewSignal();
        TaskCompletionSource viewsStarted = NewSignal();
        int historyRuns = 0;

        static async Task BlockAsync(TaskCompletionSource started, CancellationToken cancellationToken)
        {
            _ = started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        Task preload = session.RunOptionalStagesAsync(
            new(
                static () => { },
                _ => ++historyRuns == 1
                    ? Task.FromException(new InvalidOperationException("history failed"))
                    : Task.CompletedTask,
                null,
                token => BlockAsync(diagnosticsStarted, token),
                (_, isCurrent, token) =>
                {
                    Assert.True(isCurrent());
                    return BlockAsync(viewsStarted, token);
                }),
            TestContext.Current.CancellationToken);
        await Task.WhenAll(diagnosticsStarted.Task, viewsStarted.Task).WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await session.CancelOptionalsAndDrainAsync();
        await preload;

        Assert.Equal(ShellPreloadStageState.Succeeded, session.CatalogStage.State);
        Assert.Equal(ShellPreloadStageState.Failed, Stage(session, ShellPreloadSession.HistoryStageId).State);
        Assert.Equal(
            ShellPreloadStageState.Succeeded,
            Stage(session, ShellPreloadSession.ExternalEnvironmentStageId).State);
        Assert.All(session.Stages.Where(stage => !stage.IsRequired &&
                stage.Id is not ShellPreloadSession.HistoryStageId and
                    not ShellPreloadSession.ExternalEnvironmentStageId),
            stage => Assert.Equal(ShellPreloadStageState.Cancelled, stage.State));

        Assert.True(await session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken));
        Assert.Equal(ShellPreloadStageState.Succeeded, Stage(session, ShellPreloadSession.HistoryStageId).State);
        Assert.Equal(2, historyRuns);
    }

    /// <summary>An optional worker that ignores cancellation cannot publish after its bounded drain.</summary>
    [Fact]
    public async Task OptionalDrainTimeoutInvalidatesLateProgress()
    {
        int reports = 0;
        int historyRuns = 0;
        using var session = new ShellPreloadSession(
            _ => reports++,
            Text,
            drainTimeout: TimeSpan.FromMilliseconds(20));
        session.AdoptReadyCatalog();
        TaskCompletionSource started = NewSignal();
        TaskCompletionSource release = NewSignal();
        Action<int, int>? delayedProgress = null;
        Task preload = session.RunOptionalStagesAsync(
            new(
                static () => { },
                _ => ++historyRuns == 1
                    ? Task.FromException(new InvalidOperationException("history failed"))
                    : Task.CompletedTask,
                null,
                static _ => Task.CompletedTask,
                async (progress, isCurrent, cancellationToken) =>
                {
                    delayedProgress = progress;
                    _ = started.TrySetResult();
                    await release.Task;
                    Assert.False(isCurrent());
                }),
            TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Task cancel = session.CancelOptionalsAndDrainAsync();
        Assert.Equal(ShellPreloadStageState.Cancelled,
            Stage(session, ShellPreloadSession.ViewsStageId).State);
        await cancel;
        _ = Assert.Throws<InvalidOperationException>(() => delayedProgress!(1, 1));
        Assert.False(await session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken));
        _ = release.TrySetResult();
        await preload;
        Assert.True(await session.TryRetryOptionalAsync(
            ShellPreloadSession.HistoryStageId,
            TestContext.Current.CancellationToken));
        int reportsAfterRetry = reports;

        Assert.Equal(reportsAfterRetry, reports);
        Assert.Equal(2, historyRuns);
    }
}
