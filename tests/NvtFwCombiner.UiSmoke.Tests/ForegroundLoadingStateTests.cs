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

    /// <summary>Known progress is bounded, determinate, and remains available for future foreground operations.</summary>
    [Fact]
    public void LoadingStateAcceptsOnlyBoundedDeterminateProgress()
    {
        var state = new ForegroundLoadingState();
        state.Begin("Exporting", "Preparing output.");

        state.ReportProgress(0.42, "Writing output.");

        Assert.False(state.IsIndeterminate);
        Assert.True(state.HasDeterminateProgress);
        Assert.Equal(0.42, state.Progress);
        Assert.Equal("42%", state.ProgressPercentLabel);
        Assert.Equal("Writing output.", state.Detail);
        Assert.Equal("Exporting 42% — Writing output.", state.AccessibleStatus);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => state.ReportProgress(-0.01));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => state.ReportProgress(1.01));

        state.ReportProgress(1);

        Assert.Equal("100%", state.ProgressPercentLabel);
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
}
