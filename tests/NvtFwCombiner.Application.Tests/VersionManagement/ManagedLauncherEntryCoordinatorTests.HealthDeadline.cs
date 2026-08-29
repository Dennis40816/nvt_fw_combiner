using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class ManagedLauncherEntryCoordinatorTests
{
    /// <summary>An uncooperative state read cannot hold the entry path past 250 ms.</summary>
    [Fact]
    public async Task UncooperativeStateLoadIsAbandonedAtHealthDeadline()
    {
        string root = Root("uncooperative-state");
        var time = new ManualTimeProvider();
        var state = new PendingStateStore();
        var roots = new RecordingRootProbe(ManagedInstallationRootStatus.Present);
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            state,
            roots,
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            healthObservationDeadline: TimeSpan.FromMilliseconds(250),
            timeProvider: time);

        Task<ManagedLauncherEntryResult> running = coordinator.RunAsync(
            TestContext.Current.CancellationToken).AsTask();
        Assert.False(running.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(249));
        Assert.False(running.IsCompleted);
        time.Advance(TimeSpan.FromMilliseconds(1));

        ManagedLauncherEntryResult result = await running.WaitAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedLauncherEntryOutcome.HealthUnavailable, result.Outcome);
        Assert.False(state.Pending.IsCompleted);
        Assert.Equal(0, roots.ObserveCount);
        Assert.Equal(0, handoff.StartCount);

        state.Complete(BoundState(root));
        await state.Returned.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, roots.ObserveCount);
        Assert.Equal(0, handoff.StartCount);
    }

    /// <summary>An uncooperative exact-root read cannot start a child after timeout.</summary>
    [Fact]
    public async Task UncooperativeRootProbeIsAbandonedAtHealthDeadline()
    {
        string root = Root("uncooperative-root");
        var time = new ManualTimeProvider();
        var roots = new PendingRootProbe();
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            new EntryStateStore(BoundState(root)),
            roots,
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            healthObservationDeadline: TimeSpan.FromMilliseconds(250),
            timeProvider: time);

        Task<ManagedLauncherEntryResult> running = coordinator.RunAsync(
            TestContext.Current.CancellationToken).AsTask();
        await roots.Entered.WaitAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMilliseconds(250));

        ManagedLauncherEntryResult result = await running.WaitAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedLauncherEntryOutcome.HealthUnavailable, result.Outcome);
        Assert.False(roots.Pending.IsCompleted);
        Assert.Equal(0, handoff.StartCount);

        roots.Complete(ManagedInstallationRootStatus.Present);
        await roots.Returned.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, handoff.StartCount);
    }

    /// <summary>A real timer bounds an adapter that ignores cancellation; this is not P95 evidence.</summary>
    [Fact]
    public async Task RealClockHealthCutoffReturnsBeforeUncooperativeRootProbeCompletes()
    {
        string root = Root("real-clock-uncooperative-root");
        var roots = new PendingRootProbe();
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            new EntryStateStore(BoundState(root)),
            roots,
            handoff,
            healthObservationDeadline: TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        ManagedLauncherEntryResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken).AsTask().WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(ManagedLauncherEntryOutcome.HealthUnavailable, result.Outcome);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), stopwatch.Elapsed.ToString());
        Assert.False(roots.Pending.IsCompleted);
        Assert.Equal(0, handoff.StartCount);

        roots.Complete(ManagedInstallationRootStatus.Present);
        await roots.Returned.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, handoff.StartCount);
    }

    /// <summary>In-flight caller cancellation remains cancellation, not health failure.</summary>
    [Fact]
    public async Task InFlightCallerCancellationIsNotConvertedIntoHealthFailure()
    {
        string root = Root("in-flight-cancel");
        using var cancellation = new CancellationTokenSource();
        var state = new PendingStateStore();
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            state,
            new RecordingRootProbe(ManagedInstallationRootStatus.Present),
            handoff);

        Task<ManagedLauncherEntryResult> running = coordinator.RunAsync(cancellation.Token).AsTask();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await running);
        Assert.Equal(0, handoff.StartCount);
        state.Complete(BoundState(root));
        await state.Returned.WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The health timer is retired before Bootstrap admission begins.</summary>
    [Fact]
    public async Task HealthDeadlineCannotCancelBootstrapAfterClassification()
    {
        string root = Root("health-retired-before-bootstrap");
        var time = new ManualTimeProvider();
        var handoff = new ControlledAdmissionHandoff();
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            new EntryStateStore(BoundState(root)),
            new RecordingRootProbe(ManagedInstallationRootStatus.Present),
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            healthObservationDeadline: TimeSpan.FromMilliseconds(250),
            timeProvider: time);

        Task<ManagedLauncherEntryResult> running = coordinator.RunAsync(
            TestContext.Current.CancellationToken).AsTask();
        await handoff.AdmissionStarted.WaitAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMilliseconds(250));

        Assert.False(running.IsCompleted);
        Assert.False(handoff.AdmissionCancelled);
        handoff.Admit();
        ManagedLauncherEntryResult result = await running.WaitAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherEntryOutcome.LaunchInstalled, result.Outcome);
        Assert.False(handoff.AdmissionCancelled);
    }

    private sealed class PendingStateStore : IVersionManagerStateStore
    {
        private readonly TaskCompletionSource<VersionManagerStateLoadResult> _pending =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _returned =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Pending => _pending.Task;
        internal Task Returned => _returned.Task;

        internal void Complete(VersionManagerState state)
        {
            _ = _pending.TrySetResult(new(state, VersionManagerStateLoadIssue.None));
        }

        public async ValueTask<VersionManagerStateLoadResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                return await _pending.Task.ConfigureAwait(false);
            }
            finally
            {
                _ = _returned.TrySetResult();
            }
        }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }

        public ValueTask SaveAsync(
            VersionManagerState state,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }
    }

    private sealed class PendingRootProbe : IManagedInstallationRootProbe
    {
        private readonly TaskCompletionSource<ManagedInstallationRootObservation> _pending =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _returned =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Pending => _pending.Task;
        internal Task Entered => _entered.Task;
        internal Task Returned => _returned.Task;

        internal void Complete(ManagedInstallationRootStatus status)
        {
            _ = _pending.TrySetResult(new(status));
        }

        public async ValueTask<ManagedInstallationRootObservation> ObserveAsync(
            string managedRoot,
            CancellationToken cancellationToken)
        {
            _ = _entered.TrySetResult();
            try
            {
                return await _pending.Task.ConfigureAwait(false);
            }
            finally
            {
                _ = _returned.TrySetResult();
            }
        }
    }

    private sealed class ControlledAdmissionHandoff : IImmutableBootstrapHandoff
    {
        private readonly TaskCompletionSource<ImmutableBootstrapAdmissionResult> _admission =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _admissionStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task AdmissionStarted => _admissionStarted.Task;
        internal bool AdmissionCancelled { get; private set; }

        internal void Admit()
        {
            _ = _admission.TrySetResult(new(ImmutableBootstrapAdmissionOutcome.Admitted));
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
            "Ownership transfers into the returned launch receipt and the coordinator disposes it.")]
        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new ImmutableBootstrapStartResult(
                new ControlledAdmissionLaunch(this),
                ImmutableBootstrapStartIssue.None));
        }

        private sealed class ControlledAdmissionLaunch(ControlledAdmissionHandoff owner)
            : IImmutableBootstrapLaunch
        {
            public async ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
                ImmutableBootstrapWaitBudget budget,
                CancellationToken cancellationToken)
            {
                _ = owner._admissionStarted.TrySetResult();
                try
                {
                    return await owner._admission.Task.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    owner.AdmissionCancelled = true;
                    throw;
                }
            }

            public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
                ImmutableBootstrapWaitBudget budget,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(new ImmutableBootstrapCompletionResult(
                    ImmutableBootstrapCompletionOutcome.Ready));
            }

            public void Dispose()
            {
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly Lock _sync = new();
        private readonly List<ManualTimer> _timers = [];
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
                    if (!owner._timers.Contains(this))
                    {
                        owner._timers.Add(this);
                    }
                    _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                        ? null
                        : checked(owner._timestamp + Math.Max(0, dueTime.Ticks));
                    _periodTicks = period == Timeout.InfiniteTimeSpan ? null : period.Ticks;
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
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
