using System.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Serializes local-state saves and lets a newer immutable snapshot supersede queued work.</summary>
internal sealed class LatestSnapshotPersistenceCoordinator<TSnapshot>
{
    private readonly Lock _gate = new();
    private readonly Func<TSnapshot, TSnapshot> _capture;
    private readonly Func<TSnapshot, CancellationToken, Task> _saveAsync;
    private readonly Action<Exception?>? _saveCompleted;
    private CancellationTokenSource? _latestCancellation;
    private Task _tail = Task.CompletedTask;
    private TSnapshot? _latestSnapshot;
    private bool _hasLatestSnapshot;
    private bool _isCompleted;

    /// <param name="saveAsync">Persists one captured snapshot.</param>
    /// <param name="capture">Copies a snapshot into an immutable value before it is queued.</param>
    /// <param name="saveCompleted">
    /// Observes every save that finished (<see langword="null"/>) or failed (the exception), in queue order on a
    /// background thread; a save superseded by a newer snapshot reports nothing, even when its write ignored the
    /// cancellation and finished.
    /// </param>
    internal LatestSnapshotPersistenceCoordinator(
        Func<TSnapshot, CancellationToken, Task> saveAsync,
        Func<TSnapshot, TSnapshot> capture,
        Action<Exception?>? saveCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(saveAsync);
        ArgumentNullException.ThrowIfNull(capture);
        _saveAsync = saveAsync;
        _capture = capture;
        _saveCompleted = saveCompleted;
    }

    internal void Queue(TSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        TSnapshot capturedSnapshot = _capture(snapshot);
        ArgumentNullException.ThrowIfNull(capturedSnapshot);

        lock (_gate)
        {
            if (_isCompleted)
            {
                throw new InvalidOperationException("Local-state persistence is already completing.");
            }

            QueueCaptured(capturedSnapshot);
        }
    }

    /// <summary>Queues the latest captured snapshot again; false once completing or before any snapshot.</summary>
    internal bool TryRetry()
    {
        lock (_gate)
        {
            if (_isCompleted || !_hasLatestSnapshot)
            {
                return false;
            }

            QueueCaptured(_latestSnapshot!);
            return true;
        }
    }

    internal Task WaitForIdleAsync()
    {
        lock (_gate)
        {
            return _tail;
        }
    }

    internal Task CompleteAsync()
    {
        lock (_gate)
        {
            _isCompleted = true;
            return _tail;
        }
    }

    internal Exception? LastFailure
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        private set;
    }

    private void QueueCaptured(TSnapshot capturedSnapshot)
    {
        var cancellation = new CancellationTokenSource();
        _latestCancellation?.Cancel();
        _latestCancellation = cancellation;
        _latestSnapshot = capturedSnapshot;
        _hasLatestSnapshot = true;
        Task predecessor = _tail;
        _tail = Task.Run(() => PersistAfterAsync(predecessor, capturedSnapshot, cancellation));
    }

    private async Task PersistAfterAsync(
        Task predecessor,
        TSnapshot snapshot,
        CancellationTokenSource cancellation)
    {
        bool isTerminal = false;
        bool isLatest = false;
        Exception? failure = null;
        try
        {
            await ObserveCompletionAsync(predecessor).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            await _saveAsync(snapshot, cancellation.Token).ConfigureAwait(false);
            isTerminal = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
            failure = exception;
            isTerminal = true;
        }
        finally
        {
            lock (_gate)
            {
                // Queue replaces the latest cancellation under this gate, so the save still owns it only when no
                // newer snapshot superseded it, even if its write ignored the cancellation and finished anyway.
                isLatest = ReferenceEquals(_latestCancellation, cancellation);
                if (isLatest)
                {
                    _latestCancellation = null;
                }
            }

            cancellation.Dispose();
        }

        if (isTerminal && isLatest)
        {
            ReportSaveCompleted(failure);
        }
    }

    private async Task ObserveCompletionAsync(Task predecessor)
    {
        try
        {
            await predecessor.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
    }

    private void RecordFailure(Exception exception)
    {
        lock (_gate)
        {
            LastFailure = exception;
        }
    }

    private void ReportSaveCompleted(Exception? failure)
    {
        if (_saveCompleted is null)
        {
            return;
        }

        try
        {
            _saveCompleted(failure);
        }
        catch (Exception exception)
        {
            // A failing observer must not poison later saves.
            Trace.TraceError("Local-state save observer failed: {0}", exception);
        }
    }
}
