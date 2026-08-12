namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Serializes mutable presentation preparations while preserving each caller's result.</summary>
internal sealed class SerialTaskQueue
{
    private readonly Lock _gate = new();
    private Task _tail = Task.CompletedTask;

    internal Task Enqueue(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        TaskScheduler scheduler = SynchronizationContext.Current is null
            ? TaskScheduler.Current
            : TaskScheduler.FromCurrentSynchronizationContext();
        lock (_gate)
        {
            Task current = _tail.ContinueWith(
                    _ => action(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    scheduler)
                .Unwrap();
            _tail = current.ContinueWith(
                static _ => { },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return current;
        }
    }
}
