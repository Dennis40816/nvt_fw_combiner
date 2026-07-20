using System.Collections.Concurrent;

namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class UiThreadTestContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _workItems = [];
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;

    public UiThreadTestContext()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = nameof(UiThreadTestContext),
        };
        _thread.Start();
        _started.Task.GetAwaiter().GetResult();
    }

    public int ThreadId { get; private set; }

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(
            async _ =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            },
            null);
        return completion.Task;
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _workItems.Add((callback, state));
    }

    public void Dispose()
    {
        _workItems.CompleteAdding();
        if (!_thread.Join(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("The UI test thread did not stop.");
        }

        _workItems.Dispose();
    }

    private void Run()
    {
        ThreadId = Environment.CurrentManagedThreadId;
        SetSynchronizationContext(this);
        _started.SetResult();
        foreach ((SendOrPostCallback callback, object? state) in _workItems.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}
