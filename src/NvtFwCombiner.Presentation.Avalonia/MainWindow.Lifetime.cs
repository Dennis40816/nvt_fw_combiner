using Avalonia.Threading;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private readonly WindowPublicationLease _windowPublication = new();
    private readonly WindowLifetimeCoordinator _lifetime;
    internal WindowClosePhase ClosePhase => _lifetime.ClosePhase;
    internal Task CloseAttempt => _lifetime.CloseAttempt;
    private readonly List<Task> _sessionTasks = [];
    private bool _preloadReleaseScheduled;
    internal Task StartupWork { get; private set; } = Task.CompletedTask;
    internal Func<TimeSpan, Task> CloseDeadlineFactory
    {
        get => _lifetime.CloseDeadlineFactory;
        set => _lifetime.CloseDeadlineFactory = value;
    }

    private void ConnectLifetimeGates(ViewModels.MainWindowViewModel viewModel) =>
        _lifetime.ConnectGates(viewModel, () => IsEnabled);

    private WindowLifetimeCoordinator CreateWindowLifetime()
    {
        return new(
            currentViewModel: () => DataContext as ViewModels.MainWindowViewModel,
            sessionTasks: () => [.. _sessionTasks],
            cancelStartup: CancelStartupLoad,
            stopPreload: _preloadSession.StopAcceptingAndRevoke,
            preloadUsersSettled: () => _preloadSession.AllUsersSettled,
            completeLocalState: () => Task.WhenAll(
                _reportHistoryPersistence.CompleteAsync(), _shellPreferencePersistence.CompleteAsync()),
            reopenLocalState: ReopenLocalStateAfterFailedHandoff,
            replayLocalState: ReplayLocalStateAfterFailedHandoff,
            tryStartStableLauncher: TryStartStableLauncherAsync,
            suspendPublication: _windowPublication.Suspend,
            resumePublication: _windowPublication.Resume,
            revokePublication: _windowPublication.Revoke,
            cancelCaptureForClose: CancelCaptureForClose,
            setEnabled: enabled => IsEnabled = enabled,
            post: action => Dispatcher.UIThread.Post(action),
            close: Close,
            disposeResources: () =>
            {
                _localStateSave.Detach();
                _startupLoadCancellation.Cancel();
                _preloadSession.StopAcceptingAndRevoke();
                RetireSession(_startupLoadCancellation);
            });
    }

    private void CancelStartupLoad()
    {
        _startupLoadCancellation.Cancel();
    }

    private bool CancelCaptureForClose()
    {
        return LaunchCoordinator.CaptureSession.CancelForClose();
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
