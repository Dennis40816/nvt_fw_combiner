namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Serializes mutable presentation preparations while preserving each caller's result.</summary>
internal sealed class SerialTaskQueue
{
    private readonly Lock _gate = new();
    private Task _tail = Task.CompletedTask;

    internal Task Enqueue(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            Task current = RunAfterAsync(_tail, action);
            _tail = current.ContinueWith(
                static _ => { },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return current;
        }
    }

    private static async Task RunAfterAsync(Task predecessor, Func<Task> action)
    {
        await predecessor.ConfigureAwait(false);
        await action().ConfigureAwait(false);
    }
}
