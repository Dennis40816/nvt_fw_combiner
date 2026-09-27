using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using Avalonia.Threading;

namespace NvtFwCombiner.Presentation.Avalonia;

internal enum WindowClosePhase { Open, Draining, Sealing, HandingOff, Recovering, Closing, Closed }

public sealed partial class MainWindow
{
    private readonly WindowPublicationLease _windowPublication = new();
    private bool _finalClosePosted;
    private bool _internalFinalClose;
    private bool _deferredActivationRequested;
    internal WindowClosePhase ClosePhase { get; private set; } = WindowClosePhase.Open;
    internal Task CloseAttempt { get; private set; } = Task.CompletedTask;
    private readonly List<Task> _sessionTasks = [];
    private bool _preloadReleaseScheduled;
    internal Task StartupWork { get; private set; } = Task.CompletedTask;
    internal Func<TimeSpan, Task> CloseDeadlineFactory { get; set; } = static duration => Task.Delay(duration);

    private async Task RunCloseAttemptAsync()
    {
        Task runCompletion = Task.CompletedTask;
        try
        {
            try
            {
                _startupLoadCancellation.Cancel();
                if (DataContext is MainWindowViewModel viewModel)
                {
                    runCompletion = viewModel.RunSession.ActiveRunCompletion;
                    Task settingsCompletion = viewModel.Settings.WhenOperationsIdleAsync();
                    viewModel.RunSession.CancelActiveRun();
                    _preloadSession.StopAcceptingAndRevoke();
                    var work = Task.WhenAll([runCompletion, settingsCompletion,
                        _preloadSession.AllUsersSettled, .. _sessionTasks]);
                    await WaitWithinCloseDeadlineAsync(work);
                }
                else
                {
                    _preloadSession.StopAcceptingAndRevoke();
                    await WaitWithinCloseDeadlineAsync(Task.WhenAll(
                        [_preloadSession.AllUsersSettled, .. _sessionTasks]));
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Window close work drain failed: {0}", exception);
            }
            finally
            {
                if (!runCompletion.IsCompleted && DataContext is MainWindowViewModel timedOutViewModel)
                {
                    timedOutViewModel.RunSession.RevokeActiveRun();
                }
            }

            ClosePhase = WindowClosePhase.Sealing;
            _windowPublication.Suspend();
            _localStateSealed = true;
            try
            {
                var completion = Task.WhenAll(
                    _reportHistoryPersistence.CompleteAsync(),
                    _shellPreferencePersistence.CompleteAsync());
                await WaitWithinCloseDeadlineAsync(completion);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Window close local-state flush failed: {0}", exception);
            }
            finally
            {
                _isReportHistoryPersistenceComplete = true;
            }

            if (_restartThroughStableLauncher)
            {
                ClosePhase = WindowClosePhase.HandingOff;
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
        if (_finalClosePosted || ClosePhase == WindowClosePhase.Closed)
        {
            return;
        }
        _finalClosePosted = true;
        _preloadSession.StopAcceptingAndRevoke();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.RunSession.RevokeActiveRun();
        }
        _windowPublication.Revoke();
        ClosePhase = WindowClosePhase.Closing;
        Dispatcher.UIThread.Post(() =>
        {
            _internalFinalClose = true;
            try
            {
                Close();
            }
            finally
            {
                _internalFinalClose = false;
            }
        });
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
            _ = work.ContinueWith(completed =>
            {
                _ = completed.Exception;
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private void ObserveSessionTask(Task task)
    {
        _sessionTasks.Add(task);
        _ = task.ContinueWith(completed =>
        {
            _ = completed.Exception;
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private void RetireSession(CancellationTokenSource source)
    {
        Task[] users = [.. _sessionTasks];
        _sessionTasks.Clear();
        bool releasePreload = !_preloadReleaseScheduled;
        _preloadReleaseScheduled = true;
        _ = ObserveAndReleaseSessionAsync(source, users, releasePreload);
    }

    private async Task ObserveAndReleaseSessionAsync(
        CancellationTokenSource source,
        Task[] users,
        bool releasePreload)
    {
        try
        {
            Task[] owned = releasePreload ? [.. users, _preloadSession.AllUsersSettled] : users;
            await Task.WhenAll(owned);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Window session work failed during release: {0}", exception);
        }
        finally
        {
            try
            {
                source.Dispose();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError("Window session cancellation release failed: {0}", exception);
            }
            if (releasePreload)
            {
                try
                {
                    _preloadSession.Dispose();
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Trace.TraceError("Preload release failed: {0}", exception);
                }
            }
        }
    }
}

/// <summary>One window's reversible publication gate for admitted, noncancelled UI work.</summary>
internal sealed class WindowPublicationLease
{
    private readonly Lock _gate = new();
    private TaskCompletionSource _changed = NewSignal();
    private bool _suspended;
    private bool _revoked;

    internal bool CanPublish
    {
        get
        {
            lock (_gate)
            {
                return !_suspended && !_revoked;
            }
        }
    }

    internal void Suspend()
    {
        lock (_gate)
        {
            if (!_revoked)
            {
                _suspended = true;
            }
        }
    }

    internal void Resume()
    {
        lock (_gate)
        {
            if (_revoked || !_suspended)
            {
                return;
            }
            _suspended = false;
            TaskCompletionSource previous = _changed;
            _changed = NewSignal();
            previous.SetResult();
        }
    }

    internal void Revoke()
    {
        lock (_gate)
        {
            if (_revoked)
            {
                return;
            }
            _revoked = true;
            _changed.SetResult();
        }
    }

    internal async Task<bool> WaitToPublishAsync(Func<bool> operationValid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operationValid);
        while (true)
        {
            Task wake;
            lock (_gate)
            {
                if (_revoked || cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                if (!_suspended)
                {
                    return operationValid();
                }
                wake = _changed.Task;
            }
            try
            {
                await wake.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }
    }

    private static TaskCompletionSource NewSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
