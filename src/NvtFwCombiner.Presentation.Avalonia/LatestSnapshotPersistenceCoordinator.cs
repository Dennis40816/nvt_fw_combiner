namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Serializes local-state saves and lets a newer immutable snapshot supersede queued work.</summary>
internal sealed class LatestSnapshotPersistenceCoordinator<TSnapshot>
{
    private readonly Lock _gate = new();
    private readonly Func<TSnapshot, TSnapshot> _capture;
    private readonly Func<TSnapshot, CancellationToken, Task> _saveAsync;
    private CancellationTokenSource? _latestCancellation;
    private Task _tail = Task.CompletedTask;
    private bool _isCompleted;

    internal LatestSnapshotPersistenceCoordinator(
        Func<TSnapshot, CancellationToken, Task> saveAsync,
        Func<TSnapshot, TSnapshot> capture)
    {
        ArgumentNullException.ThrowIfNull(saveAsync);
        ArgumentNullException.ThrowIfNull(capture);
        _saveAsync = saveAsync;
        _capture = capture;
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

            var cancellation = new CancellationTokenSource();
            _latestCancellation?.Cancel();
            _latestCancellation = cancellation;
            Task predecessor = _tail;
            _tail = Task.Run(() => PersistAfterAsync(predecessor, capturedSnapshot, cancellation));
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

    private async Task PersistAfterAsync(
        Task predecessor,
        TSnapshot snapshot,
        CancellationTokenSource cancellation)
    {
        try
        {
            await ObserveCompletionAsync(predecessor).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            await _saveAsync(snapshot, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_latestCancellation, cancellation))
                {
                    _latestCancellation = null;
                }
            }

            cancellation.Dispose();
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
}
