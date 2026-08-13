using System.Runtime.CompilerServices;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Behavioral coverage for reusable foreground operation state.</summary>
public sealed class ForegroundLoadingStateTests
{
    /// <summary>Unknown progress remains indeterminate and reduced motion suppresses animation without hiding status.</summary>
    [Fact]
    public void LoadingStateDistinguishesUnknownProgressAndReducedMotion()
    {
        var state = new ForegroundLoadingState();

        state.Begin(
            "Loading capabilities",
            "Preparing the canonical catalog.",
            cancelLabel: "Cancel startup");

        Assert.True(state.IsVisible);
        Assert.True(state.IsRunning);
        Assert.True(state.IsIndeterminate);
        Assert.True(state.ShouldAnimate);
        Assert.True(state.CanCancel);
        Assert.Equal("Cancel startup", state.CancelLabel);
        Assert.False(state.HasDeterminateProgress);
        Assert.Empty(state.ProgressPercentLabel);
        Assert.Equal("Loading capabilities — Preparing the canonical catalog.", state.AccessibleStatus);

        state.SetReducedMotion(true);

        Assert.True(state.IsVisible);
        Assert.True(state.IsIndeterminate);
        Assert.False(state.ShouldAnimate);
    }

    /// <summary>Known progress is bounded text while continuous activity remains visible.</summary>
    [Fact]
    public void LoadingStateAcceptsOnlyBoundedDeterminateProgress()
    {
        var state = new ForegroundLoadingState();
        state.Begin("Exporting", "Preparing output.");

        state.ReportProgress(0.42, "Writing output.");

        Assert.True(state.IsIndeterminate);
        Assert.True(state.ShouldAnimate);
        Assert.True(state.HasDeterminateProgress);
        Assert.Equal(0.42, state.Progress);
        Assert.Equal("42%", state.ProgressPercentLabel);
        Assert.Equal("Writing output.", state.Detail);
        Assert.Equal("Exporting 42% — Writing output.", state.AccessibleStatus);

        state.ReportProgress(0.43, state.Detail);

        Assert.Equal("43%", state.ProgressPercentLabel);

        state.ReportProgress(0.51, state.Detail);

        Assert.Equal("51%", state.ProgressPercentLabel);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => state.ReportProgress(-0.01, state.Detail));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => state.ReportProgress(1.01, state.Detail));

        state.ReportProgress(1, state.Detail);

        Assert.Equal("100%", state.ProgressPercentLabel);
    }

    /// <summary>A delayed callback from the prior attempt cannot overwrite the active retry surface.</summary>
    [Fact]
    public async Task PreloadProjectionAcceptsOnlyTheCurrentAttempt()
    {
        var failure = new CapabilityCatalogReloadResult(
            Succeeded: false,
            RetainedLastKnownGood: false,
            Snapshot: null,
            [new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.SourceInvalid, "Invalid catalog.")]);
        using var session = new ShellPreloadSession(
            static _ => { },
            ShellTextResources.For(ShellLanguage.English));
        _ = await session.RunCatalogAsync(
            new OneUpdateLoader(new CanonicalCapabilityCatalogLoadUpdate(Progress: null, failure)),
            static _ => ValueTask.CompletedTask,
            retry: false,
            TestContext.Current.CancellationToken);
        ShellPreloadStageSnapshot staleStage = session.CatalogStage;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task retry = session.RunCatalogAsync(
            new BlockingLoader(entered),
            static _ => ValueTask.CompletedTask,
            retry: true,
            TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var state = new ForegroundLoadingState();
        var text = ShellTextResources.For(ShellLanguage.English);
        MainWindow.ApplyPreloadStage(session, state, text, session.CatalogStage);
        Assert.Equal(0, state.Progress);
        Assert.StartsWith("1 / 5", state.Detail, StringComparison.Ordinal);

        MainWindow.ApplyPreloadStage(session, state, text, staleStage);

        Assert.True(state.IsRunning);
        Assert.Equal(0, state.Progress);
        Assert.EndsWith(text.CatalogLoadingDetail, state.Detail, StringComparison.Ordinal);
        await session.CancelAndDrainAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await retry);
    }

    /// <summary>Failure and completion expose explicit retry and visibility transitions.</summary>
    [Fact]
    public void LoadingStateFailsClosedAndCompletesExplicitly()
    {
        var state = new ForegroundLoadingState();
        state.Begin("Loading capabilities", "Preparing the canonical catalog.");

        state.Fail(
            "Capabilities unavailable",
            "The catalog could not be loaded.",
            "Retry",
            "Cancel startup");

        Assert.True(state.IsVisible);
        Assert.False(state.IsRunning);
        Assert.True(state.HasFailed);
        Assert.True(state.CanRetry);
        Assert.Equal("Retry", state.RetryLabel);
        Assert.True(state.CanCancel);

        state.Begin("Loading capabilities", "Trying again.");
        state.Complete();

        Assert.False(state.IsVisible);
        Assert.False(state.IsRunning);
        Assert.False(state.HasFailed);
        Assert.False(state.CanRetry);
        Assert.False(state.CanCancel);
    }

    /// <summary>Each public transition publishes one coherent live-region status after all bound fields agree.</summary>
    [Fact]
    public void LoadingStateBatchesAccessibleStatusNotifications()
    {
        var state = new ForegroundLoadingState();
        var statuses = new List<string>();
        var properties = new List<string?>();
        state.PropertyChanged += (_, args) =>
        {
            properties.Add(args.PropertyName);
            if (args.PropertyName == nameof(ForegroundLoadingState.AccessibleStatus))
            {
                statuses.Add(state.AccessibleStatus);
            }
        };

        state.Begin("Preparing capabilities", "Loading the canonical catalog.", 0.1);
        state.ReportProgress(0.2, "Preparing the canonical capability routes.");
        state.SetReducedMotion(true);
        state.Fail("Capabilities unavailable", "Retry to restore Merge and Replace.", "Retry");

        Assert.Equal(
            [
                "Preparing capabilities 10% — Loading the canonical catalog.",
                "Preparing capabilities 20% — Preparing the canonical capability routes.",
                "Capabilities unavailable — Retry to restore Merge and Replace.",
            ],
            statuses);
        Assert.Contains(string.Empty, properties);

        int accessibleBeforeCompletion = statuses.Count;
        state.Complete();

        Assert.Equal(accessibleBeforeCompletion, statuses.Count);
    }

    /// <summary>Localized retry and progress changes announce one complete Traditional Chinese status each.</summary>
    [Fact]
    public void LoadingStateBatchesTraditionalChineseStatus()
    {
        var state = new ForegroundLoadingState();
        var statuses = new List<string>();
        state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ForegroundLoadingState.AccessibleStatus))
            {
                statuses.Add(state.AccessibleStatus);
            }
        };

        state.Begin("正在準備功能", "正在載入 canonical catalog。", 0.1, "取消啟動");
        state.ReportProgress(0.8, "正在準備 canonical capability routes。");
        state.Fail("功能目前無法使用", "請重試以恢復 Merge 與 Replace。", "重試", "取消啟動");

        Assert.Equal(
            [
                "正在準備功能 10% — 正在載入 canonical catalog。",
                "正在準備功能 80% — 正在準備 canonical capability routes。",
                "功能目前無法使用 — 請重試以恢復 Merge 與 Replace。",
            ],
            statuses);
        Assert.Equal("取消啟動", state.CancelLabel);
    }

    private sealed class OneUpdateLoader(CanonicalCapabilityCatalogLoadUpdate update) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }
    }

    private sealed class BlockingLoader(TaskCompletionSource entered) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CanonicalCapabilityCatalogLoadUpdate(0, Result: null);
            _ = entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
