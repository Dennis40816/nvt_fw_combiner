using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Behavioral coverage for reusable foreground operation state.</summary>
public sealed class ForegroundLoadingStateTests
{
    /// <summary>Each localized disclosure notification isolates observer failures from the inspection attempt.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LanguageObserverFailuresDoNotFailInspection(bool chinese)
    {
        var inspection = new WorkflowInspectionLifecycle();
        var properties = new List<string?>();
        inspection.Loading.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ForegroundLoadingState.InspectionStatusCollapseLabel) or
                nameof(ForegroundLoadingState.InspectionStatusOpenLabel))
            {
                properties.Add(args.PropertyName);
                throw new InvalidOperationException("language observer failed");
            }
        };

        WorkflowInspectionAttemptState result = await inspection.StartAsync(
            ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English),
            static (_, _, _) => Task.FromResult(new WorkflowInspectionOperationResult(true)),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, result);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, inspection.State);
        Assert.Equal(
            [nameof(ForegroundLoadingState.InspectionStatusCollapseLabel), nameof(ForegroundLoadingState.InspectionStatusOpenLabel)],
            properties);
        Assert.Equal(chinese ? "收合檢查狀態" : "Collapse inspection status", inspection.Loading.InspectionStatusCollapseLabel);
        Assert.Equal(chinese ? "開啟檢查狀態" : "Open inspection status", inspection.Loading.InspectionStatusOpenLabel);
    }

    /// <summary>Disclosure retains the complete failure, original commands and live-region status without executing work.</summary>
    [Fact]
    public async Task CollapsingFailureOnlyChangesItsPresentation()
    {
        int retries = 0;
        int cancellations = 0;
        var state = new ForegroundLoadingState(
            () => { retries++; return Task.CompletedTask; },
            () => { cancellations++; return Task.CompletedTask; });
        var other = new ForegroundLoadingState();
        state.Fail("Inspection unavailable", "IOException: selected input could not be read.", "Retry", "Cancel");
        other.Fail("Inspection unavailable", "Another page's diagnostic.", "Retry");
        (string, string, string, string, double?, string) failure =
            (state.Title, state.Detail, state.RetryLabel, state.CancelLabel, state.Progress, state.AccessibleStatus);
        IAsyncRelayCommand? retry = state.RetryCommand;
        IAsyncRelayCommand? cancel = state.CancelCommand;
        var properties = new List<string?>();
        state.PropertyChanged += (_, args) => properties.Add(args.PropertyName);

        state.CollapseCommand.Execute(null);

        Assert.True(state.IsVisible);
        Assert.True(state.IsCollapsed);
        Assert.False(state.IsExpanded);
        Assert.False(state.CanCollapse);
        Assert.True(state.CanRetry);
        Assert.True(state.CanCancel);
        Assert.True(state.ExpandCommand.CanExecute(null));
        Assert.False(state.CollapseCommand.CanExecute(null));
        Assert.True(other.IsExpanded);
        Assert.False(other.IsCollapsed);
        Assert.Equal(failure, (state.Title, state.Detail, state.RetryLabel, state.CancelLabel, state.Progress, state.AccessibleStatus));
        Assert.Same(retry, state.RetryCommand);
        Assert.Same(cancel, state.CancelCommand);
        Assert.Equal(0, retries);
        Assert.Equal(0, cancellations);
        Assert.Contains(nameof(ForegroundLoadingState.IsExpanded), properties);
        Assert.Contains(nameof(ForegroundLoadingState.IsCollapsed), properties);
        Assert.Contains(nameof(ForegroundLoadingState.CanCollapse), properties);
        Assert.DoesNotContain(nameof(ForegroundLoadingState.AccessibleStatus), properties);

        state.ExpandCommand.Execute(null);

        Assert.True(state.IsExpanded);
        Assert.False(state.IsCollapsed);
        Assert.Equal(failure, (state.Title, state.Detail, state.RetryLabel, state.CancelLabel, state.Progress, state.AccessibleStatus));
        Assert.DoesNotContain(nameof(ForegroundLoadingState.AccessibleStatus), properties);
        state.CollapseCommand.Execute(null);
        await state.RetryCommand!.ExecuteAsync(null);
        Assert.Equal(1, retries);
        Assert.Equal(0, cancellations);
    }

    /// <summary>Failure re-projection retains disclosure; a running, completed or new attempt resets it.</summary>
    [Fact]
    public void DisclosureSurvivesTextRefreshAndResetsWithTheOriginalLifecycle()
    {
        var state = new ForegroundLoadingState();
        Assert.False(state.CollapseCommand.CanExecute(null));
        state.CollapseCommand.Execute(null);
        Assert.False(state.IsCollapsed);
        state.Begin("Inspecting selected files", "Checking input.", cancelLabel: "Cancel");
        state.CollapseCommand.Execute(null);
        Assert.True(state.IsExpanded);
        Assert.False(state.IsCollapsed);
        Assert.False(state.CollapseCommand.CanExecute(null));

        state.Fail("Inspection unavailable", "Read failed.", "Retry");
        state.CollapseCommand.Execute(null);
        state.Fail("檢查目前無法完成", "讀取失敗。", "重試");
        state.SetReducedMotion(true);
        Assert.True(state.IsCollapsed);
        Assert.Equal("讀取失敗。", state.Detail);
        Assert.Equal("重試", state.RetryLabel);

        state.Begin("正在檢查所選檔案", "再次嘗試。", cancelLabel: "取消檢查");
        Assert.True(state.IsExpanded);
        Assert.False(state.IsCollapsed);
        Assert.False(state.ExpandCommand.CanExecute(null));
        Assert.True(state.CanCancel);
        state.Fail("檢查目前無法完成", "再次讀取失敗。", "重試");
        Assert.True(state.IsExpanded);
        state.CollapseCommand.Execute(null);
        state.Complete();
        Assert.False(state.IsVisible);
        Assert.False(state.IsExpanded);
        Assert.False(state.IsCollapsed);
        Assert.False(state.CanCollapse);
        Assert.False(state.ExpandCommand.CanExecute(null));
        state.Fail("Inspection unavailable", "A later attempt failed.", "Retry");
        Assert.True(state.IsExpanded);
        Assert.False(state.IsCollapsed);
    }

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
        Assert.True(state.ShouldAnimate);
        Assert.True(state.CanCancel);
        Assert.Equal("Cancel startup", state.CancelLabel);
        Assert.False(state.HasDeterminateProgress);
        Assert.Empty(state.ProgressPercentLabel);
        Assert.Equal("Loading capabilities — Preparing the canonical catalog.", state.AccessibleStatus);

        state.SetReducedMotion(true);

        Assert.True(state.IsVisible);
        Assert.False(state.ShouldAnimate);
    }

    /// <summary>Known progress is bounded text while continuous activity remains visible.</summary>
    [Fact]
    public void LoadingStateAcceptsOnlyBoundedDeterminateProgress()
    {
        var state = new ForegroundLoadingState();
        state.Begin("Exporting", "Preparing output.");

        state.ReportProgress(0.42, "Writing output.");

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
        Assert.True(state.CanRetry);
        Assert.Equal("Retry", state.RetryLabel);
        Assert.True(state.CanCancel);

        state.Begin("Loading capabilities", "Trying again.");
        state.Complete();

        Assert.False(state.IsVisible);
        Assert.False(state.IsRunning);
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
            if (string.IsNullOrEmpty(args.PropertyName) ||
                args.PropertyName == nameof(ForegroundLoadingState.AccessibleStatus))
            {
                statuses.Add(state.AccessibleStatus);
            }
        };

        state.Begin("Preparing capabilities", "Loading the canonical catalog.", 0.1);
        state.ReportProgress(0.11, "Preparing the canonical capability routes.", announce: false);
        state.ReportProgress(0.2, "Preparing the canonical capability routes.");
        state.SetReducedMotion(true);
        state.Complete();
        state.Begin("Preparing capabilities", "Preparing the canonical capability routes.", 0.2);
        state.Fail("Capabilities unavailable", "Retry to restore Merge and Replace.", "Retry");

        Assert.Equal(
            [
                "Preparing capabilities 10% — Loading the canonical catalog.",
                "Preparing capabilities 20% — Preparing the canonical capability routes.",
                "Preparing capabilities 20% — Preparing the canonical capability routes.",
                "Capabilities unavailable — Retry to restore Merge and Replace.",
            ],
            statuses);
        Assert.DoesNotContain(properties, string.IsNullOrEmpty);
        Assert.Contains(nameof(ForegroundLoadingState.IsReducedMotionEnabled), properties);

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
