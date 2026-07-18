using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Serializes report-history saves and lets a newer immutable snapshot supersede queued work.</summary>
internal sealed class ReportHistoryPersistenceCoordinator
{
    private readonly Lock _gate = new();
    private readonly Func<IReadOnlyList<ReportHistorySnapshot>, CancellationToken, Task> _saveAsync;
    private CancellationTokenSource? _latestCancellation;
    private Task _tail = Task.CompletedTask;
    private bool _isCompleted;

    internal ReportHistoryPersistenceCoordinator(
        Func<IReadOnlyList<ReportHistorySnapshot>, CancellationToken, Task> saveAsync)
    {
        ArgumentNullException.ThrowIfNull(saveAsync);
        _saveAsync = saveAsync;
    }

    internal void Queue(IReadOnlyList<ReportHistorySnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ReportHistorySnapshot[] capturedSnapshots = [.. snapshots];

        lock (_gate)
        {
            if (_isCompleted)
            {
                throw new InvalidOperationException("Report history persistence is already completing.");
            }

            var cancellation = new CancellationTokenSource();
            _latestCancellation?.Cancel();
            _latestCancellation = cancellation;
            Task predecessor = _tail;
            _tail = Task.Run(() => PersistAfterAsync(predecessor, capturedSnapshots, cancellation));
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
        IReadOnlyList<ReportHistorySnapshot> snapshots,
        CancellationTokenSource cancellation)
    {
        try
        {
            await ObserveCompletionAsync(predecessor).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            await _saveAsync(snapshots, cancellation.Token).ConfigureAwait(false);
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
