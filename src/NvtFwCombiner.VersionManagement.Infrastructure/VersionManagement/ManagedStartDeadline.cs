namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Bounds a managed start, including its synchronous contained-launch preparation.</summary>
internal sealed class ManagedStartDeadline
{
    private readonly CancellationTokenSource _deadline;
    private int _creationStarted;

    internal ManagedStartDeadline(TimeSpan readyDeadline, CancellationToken cancellationToken)
    {
        _deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _deadline.CancelAfter(readyDeadline);
    }

    internal CancellationToken Token => _deadline.Token;

    // Called inside the global process-start gate, after the final repository validation.
    internal bool TryBeginCreation()
    {
        if (Token.IsCancellationRequested)
        {
            return false;
        }
        Volatile.Write(ref _creationStarted, 1);
        return !Token.IsCancellationRequested;
    }

    internal async Task<TResult> RunAsync<TResult>(
        Func<Task<TResult>> start,
        Func<TResult> timeout,
        Func<TResult> terminationUnconfirmed,
        CancellationToken cancellationToken)
    {
        Task<TResult> worker = Task.Run(start);
        try
        {
            return await worker.WaitAsync(Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Once native creation began, its worker owns the terminal result.
            // READY may have been accepted just before cancellation; discarding
            // that result would leave a live accepted child without a state commit.
            if (Volatile.Read(ref _creationStarted) != 0)
            {
                try
                {
                    return await worker.WaitAsync(
                            ManagedProcessTermination.DefaultWaitTimeout,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return terminationUnconfirmed();
                }
            }
            throw;
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            if (Volatile.Read(ref _creationStarted) == 0)
            {
                // The worker still owns its lease and pipe. Its gate callback must reject
                // creation when released, then its finally block closes those resources.
                return timeout();
            }
            // A native creation already in flight has no cancellable Windows API.
            // Give the existing bounded cleanup policy time to confirm exit before
            // returning the ordinary timeout; uncertain cleanup blocks rollback.
            try
            {
                return await worker.WaitAsync(
                        ManagedProcessTermination.DefaultWaitTimeout,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return terminationUnconfirmed();
            }
        }
        finally
        {
            if (worker.IsCompleted)
            {
                _deadline.Dispose();
            }
            else
            {
                _ = worker.ContinueWith(
                    completed =>
                    {
                        _ = completed.Exception;
                        _deadline.Dispose();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }
}
