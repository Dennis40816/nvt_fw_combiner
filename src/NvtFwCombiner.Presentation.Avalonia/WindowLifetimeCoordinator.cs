using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal enum WindowClosePhase { Open, Draining, Sealing, HandingOff, Recovering, Closing, Closed }

/// <summary>Owns one window's close decisions and sequencing; adapters retain their resources.</summary>
internal sealed class WindowLifetimeCoordinator(
    Func<MainWindowViewModel?> currentViewModel,
    Func<Task[]> sessionTasks,
    Action cancelStartup,
    Action stopPreload,
    Func<Task> preloadUsersSettled,
    Func<Task> completeLocalState,
    Action reopenLocalState,
    Action replayLocalState,
    Func<Task<bool>> tryStartStableLauncher,
    Action suspendPublication,
    Action resumePublication,
    Action revokePublication,
    Func<bool> cancelCaptureForClose,
    Action<bool> setEnabled,
    Action<Action> post,
    Action close,
    Action disposeResources)
{
    internal enum CloseDecision { Cancel, RejectFinalClose, AuthorizeFinalClose }

    // Restart intent also records failed-handoff history and a request deferred through recovery.
    private enum HandoffIntent { None, Restart, RestartDeferred, Failed, Retry, RetryDeferred, FailedDeferred }
    // Phase alone cannot distinguish an unfinished flush or reopened queues before dirty replay.
    private enum LocalStateBoundary { Accepting, Sealed, CloseWaitFinished, Reopened }
    private readonly record struct CloseState(
        WindowClosePhase Phase, LocalStateBoundary LocalState, bool FinalCloseAuthorized);
    private readonly record struct ExitState(bool Confirmed, HandoffIntent Handoff);
    private CloseState _closeState = new(WindowClosePhase.Open, LocalStateBoundary.Accepting, false);
    private ExitState _exitState;

    internal WindowClosePhase ClosePhase => _closeState.Phase;
    internal Task CloseAttempt { get; private set; } = Task.CompletedTask;
    internal Func<TimeSpan, Task> CloseDeadlineFactory { get; set; } = static duration => Task.Delay(duration);
    internal bool IsDisposed { get; private set; }
    internal bool CanPublishSettings => !IsDisposed && ClosePhase == WindowClosePhase.Open;
    internal bool CanPublishRunResult => !IsDisposed &&
        _closeState.LocalState is LocalStateBoundary.Accepting or LocalStateBoundary.Sealed;
    internal bool LocalStateSealed =>
        _closeState.LocalState is LocalStateBoundary.Sealed or LocalStateBoundary.CloseWaitFinished;
    internal bool RestartRequested =>
        _exitState.Handoff is HandoffIntent.Restart or HandoffIntent.RestartDeferred or HandoffIntent.Retry or HandoffIntent.RetryDeferred;
    private bool HasFailedHandoff =>
        _exitState.Handoff is HandoffIntent.Failed or HandoffIntent.Retry or HandoffIntent.RetryDeferred or HandoffIntent.FailedDeferred;
    private bool HasDeferredActivation =>
        _exitState.Handoff is HandoffIntent.RestartDeferred or HandoffIntent.RetryDeferred or HandoffIntent.FailedDeferred;

    internal void ConnectGates(MainWindowViewModel viewModel, Func<bool> isWindowEnabled)
    {
        viewModel.Settings.SetWindowPublication(() => isWindowEnabled() && CanPublishSettings);
        viewModel.RunSession.SetWindowPublication(() => CanPublishRunResult);
        viewModel.RunSession.SetWindowAdmission(() => ClosePhase == WindowClosePhase.Open);
    }

    internal CloseDecision RequestClose()
    {
        if (ClosePhase == WindowClosePhase.Closing)
        {
            if (!_closeState.FinalCloseAuthorized)
            {
                return CloseDecision.RejectFinalClose;
            }
            // Consume authorization before Avalonia raises Closing, so a reentrant Close is external.
            _closeState = _closeState with { FinalCloseAuthorized = false };
            return CloseDecision.AuthorizeFinalClose;
        }

        _exitState = _exitState with { Confirmed = _exitState.Confirmed | cancelCaptureForClose() };
        if (ClosePhase != WindowClosePhase.Open || IsDisposed)
        {
            return CloseDecision.Cancel;
        }
        if (HasFailedHandoff)
        {
            _exitState = _exitState with { Confirmed = true };
        }
        if (!_exitState.Confirmed && !RestartRequested &&
            currentViewModel() is { HasSelectedFiles: true } selectedViewModel)
        {
            selectedViewModel.Navigation.RequestExitConfirmation(ConfirmExitAndClose);
            return CloseDecision.Cancel;
        }

        _exitState = _exitState with { Confirmed = true };
        SetPhase(WindowClosePhase.Draining);
        setEnabled(false);
        CloseAttempt = RunCloseAttemptAsync();
        ObserveCloseAttempt(CloseAttempt);
        return CloseDecision.Cancel;
    }

    internal void ConfirmExitAndClose()
    {
        _exitState = _exitState with { Confirmed = true };
        close();
    }

    internal void RequestStableLauncherRestart()
    {
        // An ordinary exit after recovery has already selected exit, including activation racing its drain.
        if (HasFailedHandoff && !RestartRequested && ClosePhase != WindowClosePhase.Open)
        {
            return;
        }
        _exitState = _exitState with
        {
            Handoff = _exitState.Handoff switch
            {
                HandoffIntent.None => HandoffIntent.Restart,
                HandoffIntent.Failed => HandoffIntent.Retry,
                HandoffIntent.FailedDeferred => HandoffIntent.RetryDeferred,
                HandoffIntent.Restart or HandoffIntent.RestartDeferred or HandoffIntent.Retry
                    or HandoffIntent.RetryDeferred => _exitState.Handoff,
                _ => throw new InvalidOperationException("Unknown stable launcher handoff intent."),
            },
        };
    }

    internal void RequestActivation()
    {
        if (ClosePhase is WindowClosePhase.Closing or WindowClosePhase.Closed)
        {
            return;
        }
        RequestStableLauncherRestart();
        if (ClosePhase is WindowClosePhase.Draining or WindowClosePhase.Sealing)
        {
            return;
        }
        if (ClosePhase is WindowClosePhase.HandingOff or WindowClosePhase.Recovering)
        {
            _exitState = _exitState with
            {
                Handoff = _exitState.Handoff switch
                {
                    HandoffIntent.Restart => HandoffIntent.RestartDeferred,
                    HandoffIntent.Retry => HandoffIntent.RetryDeferred,
                    HandoffIntent.Failed => HandoffIntent.FailedDeferred,
                    HandoffIntent.None or HandoffIntent.RestartDeferred or HandoffIntent.RetryDeferred
                        or HandoffIntent.FailedDeferred => _exitState.Handoff,
                    _ => throw new InvalidOperationException("Unknown stable launcher handoff intent."),
                },
            };
            return;
        }
        if (ClosePhase == WindowClosePhase.Open)
        {
            close();
        }
    }

    internal void OnClosed()
    {
        SetPhase(WindowClosePhase.Closed);
    }

    internal void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        IsDisposed = true;
        OnClosed();
        revokePublication();
        disposeResources();
    }

    private void SetPhase(WindowClosePhase phase)
    {
        _closeState = _closeState with { Phase = phase };
    }

    private void RevokeRunThatOutlivedCloseDeadline()
    {
        if (currentViewModel() is { } timedOutViewModel &&
            !timedOutViewModel.RunSession.ActiveRunCompletion.IsCompleted)
        {
            timedOutViewModel.RunSession.RevokeActiveRun();
        }
    }

    private async Task RunCloseAttemptAsync()
    {
        try
        {
            try
            {
                cancelStartup();
                if (currentViewModel() is { } viewModel)
                {
                    viewModel.RunSession.CancelActiveRun();
                    stopPreload();
                    using var stopDrain = new CancellationTokenSource();
                    try
                    {
                        var work = Task.WhenAll(preloadUsersSettled(),
                            DrainAdmittedWindowWorkAsync(viewModel, stopDrain.Token));
                        await WaitWithinCloseDeadlineAsync(work);
                    }
                    finally
                    {
                        stopDrain.Cancel();
                    }
                }
                else
                {
                    stopPreload();
                    await WaitWithinCloseDeadlineAsync(Task.WhenAll(
                        [preloadUsersSettled(), .. sessionTasks()]));
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Window close work drain failed: {0}", exception);
            }
            finally
            {
                RevokeRunThatOutlivedCloseDeadline();
            }

            SetPhase(WindowClosePhase.Sealing);
            suspendPublication();
            _closeState = _closeState with { LocalState = LocalStateBoundary.Sealed };
            try
            {
                await WaitWithinCloseDeadlineAsync(completeLocalState());
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Window close local-state flush failed: {0}", exception);
            }
            finally
            {
                // The bounded wait ended; this does not claim a timed-out write reached durable storage.
                _closeState = _closeState with { LocalState = LocalStateBoundary.CloseWaitFinished };
            }

            if (RestartRequested)
            {
                SetPhase(WindowClosePhase.HandingOff);
                if (!await TryCompleteStableLauncherHandoffAsync())
                {
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Window close attempt failed: {0}", exception);
        }
        finally
        {
            if (ClosePhase != WindowClosePhase.Open)
            {
                PostFinalClose();
            }
        }
    }

    private void PostFinalClose()
    {
        // Closing itself is the exactly-once post guard; there is no separate posted flag.
        if (ClosePhase is WindowClosePhase.Closing or WindowClosePhase.Closed)
        {
            return;
        }
        stopPreload();
        currentViewModel()?.RunSession.RevokeActiveRun();
        revokePublication();
        SetPhase(WindowClosePhase.Closing);
        post(() =>
        {
            _closeState = _closeState with { FinalCloseAuthorized = true };
            try
            {
                close();
            }
            finally
            {
                _closeState = _closeState with { FinalCloseAuthorized = false };
            }
        });
    }

    private async Task DrainAdmittedWindowWorkAsync(MainWindowViewModel viewModel, CancellationToken stopDrain)
    {
        while (!stopDrain.IsCancellationRequested)
        {
            Task[] admitted = CaptureWindowWork(viewModel);
            var batch = Task.WhenAll(admitted);
            try
            {
                await batch.WaitAsync(stopDrain);
            }
            catch (OperationCanceledException) when (stopDrain.IsCancellationRequested)
            {
                _ = batch.ContinueWith(completed => _ = completed.Exception,
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                return;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Admitted window work failed during drain: {0}", exception);
            }
            if (CaptureWindowWork(viewModel).All(static task => task.IsCompleted))
            {
                return;
            }
        }
    }

    private Task[] CaptureWindowWork(MainWindowViewModel viewModel)
    {
        var tasks = new List<Task>(sessionTasks())
        {
            viewModel.RunSession.ActiveRunCompletion,
            viewModel.Settings.WhenOperationsIdleAsync(),
            viewModel.Reports.WhenSavesIdleAsync(),
        };
        viewModel.Merge.InspectionLifecycles.ForEach(lifecycle => tasks.Add(lifecycle.ActiveTask));
        viewModel.Replace.InspectionLifecycles.ForEach(lifecycle => tasks.Add(lifecycle.ActiveTask));
        if (viewModel.Reports.RelocalizationTask is { } relocalization)
        {
            tasks.Add(relocalization);
        }
        if (viewModel.Reports.OpenReportHistoryEntryAsyncCommand.ExecutionTask is { } history)
        {
            tasks.Add(history);
        }
        return [.. tasks];
    }

    private static void ObserveCloseAttempt(Task attempt)
    {
        _ = attempt.ContinueWith(completed =>
            System.Diagnostics.Trace.TraceError("Window close attempt escaped: {0}", completed.Exception),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private async Task WaitWithinCloseDeadlineAsync(Task work)
    {
        Task winner = await Task.WhenAny(work, CloseDeadlineFactory(TimeSpan.FromSeconds(5)));
        if (winner == work)
        {
            _ = work.Exception;
        }
        else
        {
            _ = work.ContinueWith(completed => _ = completed.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    internal async Task<bool> TryCompleteStableLauncherHandoffAsync()
    {
        bool started;
        try
        {
            started = await tryStartStableLauncher();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Stable launcher handoff failed: {0}", exception);
            started = false;
        }
        if (!started)
        {
            await ReportStableLauncherHandoffFailureAsync();
        }
        return started;
    }

    private async Task ReportStableLauncherHandoffFailureAsync()
    {
        _exitState = _exitState with
        {
            Handoff = HasDeferredActivation
                ? (RestartRequested ? HandoffIntent.RetryDeferred : HandoffIntent.FailedDeferred)
                : (RestartRequested ? HandoffIntent.Retry : HandoffIntent.Failed),
        };
        if (ClosePhase == WindowClosePhase.HandingOff)
        {
            SetPhase(WindowClosePhase.Recovering);
        }
        if (currentViewModel() is { } recoveryViewModel)
        {
            recoveryViewModel.Settings.MarkPendingRecoveryUnknown();
            var recoveryCancellation = new CancellationTokenSource();
            Task<PendingActivationRecoveryStatus> recovery =
                recoveryViewModel.Settings.HandleLauncherHandoffFailureAsync(recoveryCancellation.Token);
            try
            {
                Task first = await Task.WhenAny(recovery, CloseDeadlineFactory(TimeSpan.FromSeconds(5)));
                if (first == recovery)
                {
                    _ = await recovery;
                }
                else
                {
                    recoveryCancellation.Cancel();
                    recoveryViewModel.Settings.MarkPendingRecoveryUnknown();
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Pending activation recovery failed: {0}", exception);
                recoveryCancellation.Cancel();
                recoveryViewModel.Settings.MarkPendingRecoveryUnknown();
            }
            _ = ObserveRecoveryAndDisposeAsync(recovery, recoveryCancellation);
        }
        reopenLocalState();
        _closeState = _closeState with
        {
            LocalState = _closeState.LocalState switch
            {
                LocalStateBoundary.CloseWaitFinished or LocalStateBoundary.Reopened => LocalStateBoundary.Reopened,
                LocalStateBoundary.Accepting or LocalStateBoundary.Sealed => LocalStateBoundary.Accepting,
                _ => throw new InvalidOperationException("Unknown local state boundary."),
            },
        };
        replayLocalState();
        // A later Close exits normally. Only a new activation or Retry requests another handoff.
        bool deferredActivation = HasDeferredActivation;
        _exitState = new(false, HandoffIntent.Failed);
        _closeState = new(WindowClosePhase.Open, LocalStateBoundary.Accepting, false);
        setEnabled(true);
        resumePublication();
        if (currentViewModel() is { } viewModel)
        {
            viewModel.RunSession.PublishCurrentState();
            viewModel.Merge.InspectionLifecycles.ForEach(lifecycle => lifecycle.PublishCurrentState());
            viewModel.Replace.InspectionLifecycles.ForEach(lifecycle => lifecycle.PublishCurrentState());
            viewModel.Settings.PublishPendingRecoveryStatus();
        }
        if (deferredActivation)
        {
            if (currentViewModel() is { } deferredViewModel && deferredViewModel.Settings.CanRetryPendingActivation)
            {
                RequestStableLauncherRestart();
                post(close);
            }
        }
    }

    private static async Task ObserveRecoveryAndDisposeAsync(
        Task<PendingActivationRecoveryStatus> recovery, CancellationTokenSource cancellation)
    {
        try
        {
            _ = await recovery;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Late pending activation recovery failed: {0}", exception);
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}
