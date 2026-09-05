namespace NvtFwCombiner.Application.Tests.VersionManagement;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _sync = new();
    private readonly List<ManualTimer> _timers = [];
    private readonly List<(int ExpectedCount, TaskCompletionSource Completion)> _timerCountWaiters = [];
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_sync)
        {
            return _utcNow;
        }
    }

    public override long GetTimestamp()
    {
        lock (_sync)
        {
            return _timestamp;
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        _ = timer.Change(dueTime, period);
        return timer;
    }

    internal Task WaitForActiveTimerCountAsync(int expectedCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);
        lock (_sync)
        {
            if (_timers.Count == expectedCount)
            {
                return Task.CompletedTask;
            }
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _timerCountWaiters.Add((expectedCount, completion));
            return completion.Task;
        }
    }

    internal void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        List<(TimerCallback Callback, object? State)> due = [];
        lock (_sync)
        {
            _timestamp = checked(_timestamp + delta.Ticks);
            _utcNow += delta;
            foreach (ManualTimer timer in _timers.ToArray())
            {
                timer.CollectDue(_timestamp, due);
            }
        }
        foreach ((TimerCallback callback, object? state) in due)
        {
            callback(state);
        }
    }

    private void CompleteTimerCountWaitersUnderLock()
    {
        for (int index = _timerCountWaiters.Count - 1; index >= 0; index--)
        {
            (int expectedCount, TaskCompletionSource completion) = _timerCountWaiters[index];
            if (expectedCount != _timers.Count)
            {
                continue;
            }
            _timerCountWaiters.RemoveAt(index);
            _ = completion.TrySetResult();
        }
    }

    private sealed class ManualTimer(
        ManualTimeProvider owner,
        TimerCallback callback,
        object? state) : ITimer
    {
        private long? _dueTimestamp;
        private long? _periodTicks;
        private bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._sync)
            {
                if (_disposed)
                {
                    return false;
                }
                bool added = false;
                if (!owner._timers.Contains(this))
                {
                    owner._timers.Add(this);
                    added = true;
                }
                _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : checked(owner._timestamp + Math.Max(0, dueTime.Ticks));
                _periodTicks = period == Timeout.InfiniteTimeSpan ? null : period.Ticks;
                if (added)
                {
                    owner.CompleteTimerCountWaitersUnderLock();
                }
                return true;
            }
        }

        internal void CollectDue(
            long timestamp,
            List<(TimerCallback Callback, object? State)> due)
        {
            if (_disposed || _dueTimestamp is not { } dueTimestamp || timestamp < dueTimestamp)
            {
                return;
            }
            due.Add((callback, state));
            _dueTimestamp = _periodTicks is > 0
                ? checked(timestamp + _periodTicks.Value)
                : null;
        }

        public void Dispose()
        {
            lock (owner._sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _ = owner._timers.Remove(this);
                owner.CompleteTimerCountWaitersUnderLock();
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
