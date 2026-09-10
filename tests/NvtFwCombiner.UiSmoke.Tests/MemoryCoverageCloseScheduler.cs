namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Test-local scheduler that advances the actual requested close delay without wall-clock waits.</summary>
internal sealed class MemoryCoverageCloseScheduler
{
    internal List<ScheduledClose> Jobs { get; } = [];
    internal TimeSpan Now { get; private set; }

    internal IDisposable Schedule(Action callback, TimeSpan delay)
    {
        var job = new ScheduledClose(callback, Now + delay, delay);
        Jobs.Add(job);
        return job;
    }

    internal void AdvanceBy(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        TimeSpan target = Now + elapsed;
        while (Jobs.Where(job => !job.IsCancelled && !job.HasFired && job.Due <= target)
                   .OrderBy(job => job.Due).FirstOrDefault() is { } next)
        {
            Now = next.Due;
            next.Fire();
        }
        Now = target;
    }

    internal sealed class ScheduledClose(Action callback, TimeSpan due, TimeSpan requestedDelay) : IDisposable
    {
        internal TimeSpan Due { get; } = due;
        internal TimeSpan RequestedDelay { get; } = requestedDelay;
        internal bool IsCancelled { get; private set; }
        internal bool HasFired { get; private set; }

        internal void Fire()
        {
            HasFired = true;
            callback();
        }

        // Model already-queued work arriving after cancellation; normal clock advancement never does this.
        internal void ReplayStaleCallback()
        {
            callback();
        }

        public void Dispose()
        {
            IsCancelled = true;
        }
    }
}
