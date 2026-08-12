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

        state.Begin("Loading capabilities", "Preparing the canonical catalog.");

        Assert.True(state.IsVisible);
        Assert.True(state.IsRunning);
        Assert.True(state.IsIndeterminate);
        Assert.True(state.ShouldAnimate);
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

        state.ReportProgress(0.43);

        Assert.Equal("43%", state.ProgressPercentLabel);

        state.ReportProgress(0.51);

        Assert.Equal("51%", state.ProgressPercentLabel);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => state.ReportProgress(-0.01));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => state.ReportProgress(1.01));

        state.ReportProgress(1);

        Assert.Equal("100%", state.ProgressPercentLabel);
    }

    /// <summary>Late, invalid, decreasing, and completed-attempt catalog reports cannot corrupt the active surface.</summary>
    [Fact]
    public void CatalogProgressAcceptsOnlyCurrentMonotonicAttemptReports()
    {
        var state = new ForegroundLoadingState();
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        state.Begin("Loading capabilities", "First attempt.", progress: 0.4);
        state.Fail("Capabilities unavailable", "Try again.", "Retry");
        state.Begin("Loading capabilities", "Retrying.", progress: 0.1);

        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            1,
            new CanonicalCatalogStartupProgress(0.7, CanonicalCatalogStartupPhase.MaterializingRoutes));
        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(double.NaN, CanonicalCatalogStartupPhase.MaterializingRoutes));
        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(
                double.PositiveInfinity,
                CanonicalCatalogStartupPhase.MaterializingRoutes));

        Assert.Equal(0.1, state.Progress);
        Assert.Equal("Retrying.", state.Detail);

        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(0.2, CanonicalCatalogStartupPhase.MaterializingRoutes));

        Assert.Equal(0.2, state.Progress);
        Assert.Equal("20%", state.ProgressPercentLabel);

        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(0.5, CanonicalCatalogStartupPhase.MaterializingRoutes));
        Assert.Equal(0.5, state.Progress);
        Assert.Equal("50%", state.ProgressPercentLabel);

        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(0.8, CanonicalCatalogStartupPhase.MaterializingRoutes));
        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(0.2, CanonicalCatalogStartupPhase.MaterializingRoutes));

        Assert.Equal(0.8, state.Progress);
        Assert.Equal("80%", state.ProgressPercentLabel);
        Assert.Equal(text.CatalogMaterializingDetail, state.Detail);

        state.Complete();
        MainWindow.ApplyCatalogProgress(
            state,
            text,
            2,
            2,
            new CanonicalCatalogStartupProgress(1, CanonicalCatalogStartupPhase.Ready));

        Assert.False(state.IsVisible);
        Assert.Null(state.Progress);
    }

    /// <summary>Failure and completion expose explicit retry and visibility transitions.</summary>
    [Fact]
    public void LoadingStateFailsClosedAndCompletesExplicitly()
    {
        var state = new ForegroundLoadingState();
        state.Begin("Loading capabilities", "Preparing the canonical catalog.");

        state.Fail("Capabilities unavailable", "The catalog could not be loaded.", "Retry");

        Assert.True(state.IsVisible);
        Assert.False(state.IsRunning);
        Assert.True(state.HasFailed);
        Assert.True(state.CanRetry);
        Assert.Equal("Retry", state.RetryLabel);

        state.Begin("Loading capabilities", "Trying again.");
        state.Complete();

        Assert.False(state.IsVisible);
        Assert.False(state.IsRunning);
        Assert.False(state.HasFailed);
        Assert.False(state.CanRetry);
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
        Assert.Contains(nameof(ForegroundLoadingState.Progress), properties);
        Assert.Contains(nameof(ForegroundLoadingState.ProgressPercentLabel), properties);
        Assert.Contains(nameof(ForegroundLoadingState.ShouldAnimate), properties);

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

        state.Begin("正在準備功能", "正在載入 canonical catalog。", 0.1);
        state.ReportProgress(0.8, "正在準備 canonical capability routes。");
        state.Fail("功能目前無法使用", "請重試以恢復 Merge 與 Replace。", "重試");

        Assert.Equal(
            [
                "正在準備功能 10% — 正在載入 canonical catalog。",
                "正在準備功能 80% — 正在準備 canonical capability routes。",
                "功能目前無法使用 — 請重試以恢復 Merge 與 Replace。",
            ],
            statuses);
    }
}
