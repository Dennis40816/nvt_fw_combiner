using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Controlled lifecycle edges not reached by the real-window close regressions.</summary>
public sealed class WindowLifetimeCoordinatorTests
{
    /// <summary>Closing is a post guard, not permission for an external request before dispatch.</summary>
    [Fact]
    public async Task QueuedFinalCloseRejectsExternalRequestBeforeItsCallback()
    {
        var host = new LifetimeHost();
        WindowLifetimeCoordinator owner = host.Owner;
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, owner.RequestClose());
        await owner.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Closing, owner.ClosePhase);
        Action finalClose = Assert.Single(host.Posts);
        int captures = host.CaptureCalls;
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.RejectFinalClose, owner.RequestClose());
        Assert.Equal(captures, host.CaptureCalls);
        Assert.Equal(0, host.CloseCalls);
        Assert.False(owner.CanPublishSettings);
        Assert.False(owner.CanPublishRunResult);
        host.Closing = () =>
        {
            Assert.Equal(WindowLifetimeCoordinator.CloseDecision.AuthorizeFinalClose, owner.RequestClose());
            owner.OnClosed();
        };
        finalClose();
        Assert.Equal(1, host.CloseCalls);
        Assert.Equal(WindowClosePhase.Closed, owner.ClosePhase);
        Assert.True(owner.LocalStateSealed);
        _ = Assert.Single(host.Posts);
    }

    /// <summary>A host Close that returns or throws before Closing cannot leave a reusable permit.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnusedFinalAuthorizationExpiresWhenPostedCallbackExits(bool throwClose)
    {
        var host = new LifetimeHost();
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
        await host.Owner.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Action finalClose = Assert.Single(host.Posts);
        if (throwClose)
        {
            host.Closing = () => throw new InvalidOperationException("Controlled host Close failure.");
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(finalClose);
            Assert.Equal("Controlled host Close failure.", failure.Message);
        }
        else
        {
            finalClose();
        }
        Assert.Equal(1, host.CloseCalls);
        Assert.Equal(WindowClosePhase.Closing, host.Owner.ClosePhase);
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.RejectFinalClose, host.Owner.RequestClose());
        _ = Assert.Single(host.Posts);
    }

    /// <summary>The missing-DataContext path must include both preload and session work before sealing.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MissingViewModelDrainsBothOwnersBeforePersistenceAndFinalPost(bool failPersistence, bool holdPreload)
    {
        TaskCompletionSource preload = NewSignal();
        TaskCompletionSource session = NewSignal();
        TaskCompletionSource persistence = NewSignal();
        var host = new LifetimeHost
        {
            Preload = holdPreload ? preload.Task : Task.CompletedTask,
            Work = [holdPreload ? Task.CompletedTask : session.Task],
            Persistence = persistence.Task,
        };
        WindowLifetimeCoordinator owner = host.Owner;
        try
        {
            Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, owner.RequestClose());
            Task attempt = owner.CloseAttempt;
            Assert.Equal(WindowClosePhase.Draining, owner.ClosePhase);
            Assert.False(owner.CanPublishSettings);
            Assert.True(owner.CanPublishRunResult);
            Assert.False(owner.LocalStateSealed);
            Assert.False(host.SealingEntered.Task.IsCompleted);
            // Omitting the held owner would synchronously enter Sealing before RequestClose returns.
            (holdPreload ? preload : session).SetResult();
            await host.SealingEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(WindowClosePhase.Sealing, owner.ClosePhase);
            Assert.True(owner.LocalStateSealed);
            Assert.False(host.LocalStateSealedAtSuspension);
            Assert.True(owner.CanPublishRunResult);
            Assert.Empty(host.Posts);
            Assert.False(attempt.IsCompleted);
            Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, owner.RequestClose());
            Assert.Same(attempt, owner.CloseAttempt);
            if (failPersistence)
            {
                persistence.SetException(new InvalidOperationException("Controlled persistence failure."));
            }
            else
            {
                persistence.SetResult();
            }
            await attempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(WindowClosePhase.Closing, owner.ClosePhase);
            Assert.False(owner.CanPublishRunResult);
            Assert.False(host.FinalRevokePublicationAllowed);
            _ = Assert.Single(host.Posts);
            Assert.Equal(
                new (string, WindowClosePhase)[]
                {
                    ("disabled", WindowClosePhase.Draining),
                    ("cancel-startup", WindowClosePhase.Draining),
                    ("stop-preload", WindowClosePhase.Draining),
                    ("suspend", WindowClosePhase.Sealing),
                    ("complete", WindowClosePhase.Sealing),
                    ("stop-preload", WindowClosePhase.Sealing),
                    ("revoke", WindowClosePhase.Sealing),
                    ("post", WindowClosePhase.Closing),
                }, host.Events);
        }
        finally
        {
            _ = preload.TrySetResult();
            _ = session.TrySetResult();
            _ = persistence.TrySetResult();
        }
    }

    /// <summary>The fallback drain deadline advances close even if neither resource owner has settled.</summary>
    [Fact]
    public async Task MissingViewModelDeadlineSealsWithoutWaitingForUnsettledOwners()
    {
        TaskCompletionSource preload = NewSignal();
        TaskCompletionSource session = NewSignal();
        TaskCompletionSource deadline = NewSignal();
        var host = new LifetimeHost { Preload = preload.Task, Work = [session.Task] };
        host.Owner.CloseDeadlineFactory = _ => deadline.Task;
        try
        {
            Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
            Assert.Equal(WindowClosePhase.Draining, host.Owner.ClosePhase);
            deadline.SetResult();
            await host.Owner.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(preload.Task.IsCompleted);
            Assert.False(session.Task.IsCompleted);
            Assert.Equal(WindowClosePhase.Closing, host.Owner.ClosePhase);
            _ = Assert.Single(host.Posts);
            Assert.Contains(("complete", WindowClosePhase.Sealing), host.Events);
        }
        finally
        {
            _ = preload.TrySetResult();
            _ = session.TrySetResult();
            _ = deadline.TrySetResult();
        }
    }

    /// <summary>Recovery without a ViewModel still reopens resources and fences a racing restart on exit.</summary>
    [Fact]
    public async Task MissingViewModelRecoveryReopensThenOrdinaryExitIgnoresRacingActivation()
    {
        TaskCompletionSource handoff = NewSignal();
        var host = new LifetimeHost { StartLauncher = async () => { await handoff.Task; return false; } };
        host.Owner.RequestStableLauncherRestart();
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
        Task first = host.Owner.CloseAttempt;
        Assert.Equal(WindowClosePhase.HandingOff, host.Owner.ClosePhase);
        Assert.True(host.Owner.LocalStateSealed);
        Assert.False(host.Owner.CanPublishRunResult);
        Assert.False(host.Owner.CanPublishSettings);
        handoff.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Open, host.Owner.ClosePhase);
        Assert.True(host.Owner.CanPublishRunResult);
        Assert.True(host.Owner.CanPublishSettings);
        Assert.False(host.Owner.LocalStateSealed);
        Assert.False(host.Owner.RestartRequested);
        Assert.True(host.LocalStateSealedAtReopen);
        Assert.False(host.LocalStateSealedWhileReplaying);
        Assert.False(host.PublicationAllowedWhileReplaying);
        Assert.Empty(host.Posts);
        Assert.Equal(
            new (string, WindowClosePhase)[]
            {
                ("disabled", WindowClosePhase.Draining),
                ("cancel-startup", WindowClosePhase.Draining),
                ("stop-preload", WindowClosePhase.Draining),
                ("suspend", WindowClosePhase.Sealing),
                ("complete", WindowClosePhase.Sealing),
                ("handoff", WindowClosePhase.HandingOff),
                ("reopen", WindowClosePhase.Recovering),
                ("replay", WindowClosePhase.Recovering),
                ("enabled", WindowClosePhase.Open),
                ("resume", WindowClosePhase.Open),
            }, host.Events);
        TaskCompletionSource session = NewSignal();
        host.Work = [session.Task];
        try
        {
            Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
            Assert.Equal(WindowClosePhase.Draining, host.Owner.ClosePhase);
            Assert.NotSame(first, host.Owner.CloseAttempt);
            host.Owner.RequestActivation();
            Assert.False(host.Owner.RestartRequested);
            session.SetResult();
            await host.Owner.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(1, host.HandoffCalls);
            Assert.Equal(WindowClosePhase.Closing, host.Owner.ClosePhase);
            _ = Assert.Single(host.Posts);
        }
        finally
        {
            _ = session.TrySetResult();
        }
    }

    /// <summary>A direct handoff failure from Open cannot invent a completed close wait during dirty replay.</summary>
    [Fact]
    public async Task DirectHandoffFailureKeepsUnflushedPublicationDuringReplay()
    {
        var host = new LifetimeHost { StartLauncher = () => Task.FromResult(false) };
        host.Owner.RequestStableLauncherRestart();
        Assert.False(await host.Owner.TryCompleteStableLauncherHandoffAsync()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(WindowClosePhase.Open, host.Owner.ClosePhase);
        Assert.True(host.PublicationAllowedWhileReplaying);
        Assert.False(host.LocalStateSealedAtReopen);
        Assert.False(host.LocalStateSealedWhileReplaying);
        Assert.True(host.Owner.CanPublishRunResult);
        Assert.True(host.Owner.CanPublishSettings);
        Assert.False(host.Owner.LocalStateSealed);
        Assert.False(host.Owner.RestartRequested);
        Assert.Equal(1, host.HandoffCalls);
        Assert.Equal(0, host.CloseCalls);
        Assert.Empty(host.Posts);
        Assert.Equal(
            new (string, WindowClosePhase)[]
            {
                ("handoff", WindowClosePhase.Open),
                ("reopen", WindowClosePhase.Open),
                ("replay", WindowClosePhase.Open),
                ("enabled", WindowClosePhase.Open),
                ("resume", WindowClosePhase.Open),
            }, host.Events);
    }

    /// <summary>A resource-reopen exception must still reach the existing final-close fallback.</summary>
    [Fact]
    public async Task RecoveryResourceFailureStillPostsOneFinalClose()
    {
        var host = new LifetimeHost
        {
            StartLauncher = () => Task.FromResult(false),
            Reopening = () => throw new InvalidOperationException("Controlled reopen failure."),
        };
        host.Owner.RequestStableLauncherRestart();
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
        await host.Owner.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, host.HandoffCalls);
        Assert.Equal(WindowClosePhase.Closing, host.Owner.ClosePhase);
        _ = Assert.Single(host.Posts);
        Assert.Contains(("revoke", WindowClosePhase.Recovering), host.Events);
        Assert.DoesNotContain(host.Events, item => item.Event == "resume");
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.RejectFinalClose, host.Owner.RequestClose());
    }

    /// <summary>Dispose closes the phase, revokes publication before it releases resources, and runs once.</summary>
    [Fact]
    public void DisposeRevokesPublicationBeforeResourcesAndIsIdempotent()
    {
        var host = new LifetimeHost();
        host.Owner.Dispose();
        host.Owner.Dispose();
        Assert.True(host.Owner.IsDisposed);
        Assert.Equal(WindowClosePhase.Closed, host.Owner.ClosePhase);
        Assert.False(host.Owner.CanPublishSettings);
        Assert.False(host.Owner.CanPublishRunResult);
        Assert.Equal(
            new (string, WindowClosePhase)[]
            {
                ("revoke", WindowClosePhase.Closed),
                ("dispose", WindowClosePhase.Closed),
            }, host.Events);
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
        Assert.Equal(0, host.CloseCalls);
    }

    /// <summary>A Settings activation on an open window asks for a restart and closes the window once.</summary>
    [Fact]
    public void ActivationOnOpenWindowRequestsRestartAndClosesOnce()
    {
        var host = new LifetimeHost();
        host.Owner.RequestActivation();
        Assert.True(host.Owner.RestartRequested);
        Assert.Equal(1, host.CloseCalls);
        Assert.Equal(WindowClosePhase.Open, host.Owner.ClosePhase);
    }

    /// <summary>An activation that arrives once the final close is queued changes nothing.</summary>
    [Fact]
    public async Task ActivationAfterFinalCloseIsQueuedIsIgnored()
    {
        var host = new LifetimeHost();
        Assert.Equal(WindowLifetimeCoordinator.CloseDecision.Cancel, host.Owner.RequestClose());
        await host.Owner.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Closing, host.Owner.ClosePhase);
        host.Owner.RequestActivation();
        Assert.False(host.Owner.RestartRequested);
        Assert.Equal(0, host.CloseCalls);
        _ = Assert.Single(host.Posts);
    }

    private static TaskCompletionSource NewSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class LifetimeHost
    {
        internal WindowLifetimeCoordinator Owner { get; }
        internal Task Preload { get; init; } = Task.CompletedTask;
        internal Task[] Work { get; set; } = [];
        internal Task Persistence { get; init; } = Task.CompletedTask;
        internal Func<Task<bool>> StartLauncher { get; init; } = () => Task.FromResult(true);
        internal Action? Closing { get; set; }
        internal Action? Reopening { get; init; }
        internal TaskCompletionSource SealingEntered { get; } = NewSignal();
        internal List<Action> Posts { get; } = [];
        internal List<(string Event, WindowClosePhase Phase)> Events { get; } = [];
        internal int CaptureCalls { get; private set; }
        internal int CloseCalls { get; private set; }
        internal int HandoffCalls { get; private set; }
        internal bool? LocalStateSealedAtSuspension { get; private set; }
        internal bool? FinalRevokePublicationAllowed { get; private set; }
        internal bool? LocalStateSealedAtReopen { get; private set; }
        internal bool? LocalStateSealedWhileReplaying { get; private set; }
        internal bool? PublicationAllowedWhileReplaying { get; private set; }

        internal LifetimeHost()
        {
            Owner = new(
                currentViewModel: () => null,
                sessionTasks: () => Work,
                cancelStartup: () => Record("cancel-startup"),
                stopPreload: () => Record("stop-preload"),
                preloadUsersSettled: () => Preload,
                completeLocalState: () =>
                {
                    Record("complete");
                    _ = SealingEntered.TrySetResult();
                    return Persistence;
                },
                reopenLocalState: () =>
                {
                    Record("reopen");
                    WindowLifetimeCoordinator owner = Owner ?? throw new InvalidOperationException("The lifetime coordinator has not been initialized.");
                    LocalStateSealedAtReopen = owner.LocalStateSealed;
                    Reopening?.Invoke();
                },
                replayLocalState: () =>
                {
                    Record("replay");
                    WindowLifetimeCoordinator owner = Owner ?? throw new InvalidOperationException("The lifetime coordinator has not been initialized.");
                    LocalStateSealedWhileReplaying = owner.LocalStateSealed;
                    PublicationAllowedWhileReplaying = owner.CanPublishRunResult;
                },
                tryStartStableLauncher: () => { HandoffCalls++; Record("handoff"); return StartLauncher(); },
                suspendPublication: () =>
                {
                    Record("suspend");
                    WindowLifetimeCoordinator owner = Owner ?? throw new InvalidOperationException("The lifetime coordinator has not been initialized.");
                    LocalStateSealedAtSuspension = owner.LocalStateSealed;
                },
                resumePublication: () => Record("resume"),
                revokePublication: () =>
                {
                    Record("revoke");
                    WindowLifetimeCoordinator owner = Owner ?? throw new InvalidOperationException("The lifetime coordinator has not been initialized.");
                    FinalRevokePublicationAllowed = owner.CanPublishRunResult;
                },
                cancelCaptureForClose: () => { CaptureCalls++; return false; },
                setEnabled: enabled => Record(enabled ? "enabled" : "disabled"),
                post: callback => { Record("post"); Posts.Add(callback); },
                close: () => { CloseCalls++; Closing?.Invoke(); },
                disposeResources: () => Record("dispose"))
            {
                CloseDeadlineFactory = _ => new TaskCompletionSource().Task,
            };
        }

        private void Record(string action)
        {
            Events.Add((action, Owner.ClosePhase));
        }
    }
}
