using System.ComponentModel;
using System.Diagnostics;
using NvtFwCombiner.Platform.Processes;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Default process runner that uses ProcessStartInfo.ArgumentList with shell execution disabled.</summary>
/// <remarks>
/// The runner is the single owner of an invocation's terminal phase (ADR 0081): the caller's cancellation callback
/// only signals, one termination work item per invocation performs the tree kill, and every host wait after the
/// terminal signal shares one total cleanup deadline. A run returns within that deadline even when OS termination
/// never completes; the detached work then keeps its resources until it settles. A process-wide capacity bounds the
/// invocations that are running or still cleaning up (owner decision 92), so resources cannot accumulate without limit.
/// </remarks>
public sealed partial class SystemExternalProcessRunner : IExternalProcessRunner
{
    private readonly ExternalProcessRunnerSeams _seams;

    /// <summary>Creates the production runner, sharing the process-wide invocation capacity.</summary>
    public SystemExternalProcessRunner()
        : this(ExternalProcessRunnerSeams.Production)
    {
    }

    /// <summary>Fault-injection and ordering seam for tests; production uses <see cref="ExternalProcessRunnerSeams.Production"/>.</summary>
    internal SystemExternalProcessRunner(ExternalProcessRunnerSeams seams)
    {
        ArgumentNullException.ThrowIfNull(seams);
        seams.Timing.Validate();
        _seams = seams;
    }

    /// <inheritdoc />
    /// <exception cref="ExternalProcessCleanupCapacityException">
    /// The capacity of invocations that are running or still cleaning up is full; no process is started.
    /// </exception>
    /// <exception cref="ExternalProcessStartFailedException">
    /// The operating system refused to start the approved external process; no process is started.
    /// </exception>
    public async ValueTask<ExternalProcessResult> RunAsync(
        ExternalProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();

        // Owner decision 92: atomically reserve one slot before starting a process, so the invocations that are
        // running or still cleaning up never exceed the limit, even under concurrency. The reservation is released
        // exactly once: on a start failure here, or by the invocation when its work settles (inline or detached).
        // The refusal reports the in-use count the atomic reservation observed, not a later re-read.
        if (!_seams.Capacity.TryReserve(out int observedInUse))
        {
            throw new ExternalProcessCleanupCapacityException(observedInUse, _seams.Capacity.Limit);
        }

        Process? process;
        try
        {
#pragma warning disable CA2000 // The invocation custody owns the process and releases it when its work settles.
            process = ProcessLaunchGate.Start(CreateProcessStartInfo(startInfo));
#pragma warning restore CA2000
        }
        catch (Win32Exception exception)
        {
            // The OS refused the launch (for example the approved executable was removed, blocked, or is not a
            // valid Win32 application between the manifest hash check and the launch). Translate it to the one
            // stable start-failure signal every caller already has a typed path for, instead of letting the raw
            // BCL exception escape (BUG-20260926-process-start-failure-escapes-typed-result).
            _seams.Capacity.Release();
            throw new ExternalProcessStartFailedException(exception);
        }
        catch
        {
            _seams.Capacity.Release();
            throw;
        }

        if (process is null)
        {
            _seams.Capacity.Release();
            throw new InvalidOperationException("External process did not start.");
        }

        // Ownership of the reservation passes to the invocation, which releases it exactly once.
        var invocation = new Invocation(process, _seams);
        try
        {
            return await invocation.ExecuteAsync(startInfo.Timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            invocation.Release();
        }
    }

    internal static ProcessStartInfo CreateProcessStartInfo(ExternalProcessStartInfo startInfo)
    {
        var result = new ProcessStartInfo(startInfo.ExecutablePath)
        {
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in startInfo.Arguments)
        {
            result.ArgumentList.Add(argument);
        }

        return result;
    }
}

/// <summary>
/// Host cleanup timing (ADR 0081, owner decision 86). <see cref="Deadline"/> is the one total bound that counts
/// from the terminal signal and covers termination confirmation, output drain and reader stop. It is not a manifest
/// or profile timeout.
/// </summary>
/// <param name="Deadline">Total host wait after the terminal signal.</param>
/// <param name="HeldOutputGrace">After a natural exit, how long the streams may stay open before they count as held.</param>
/// <param name="ReaderStopReserve">The final part of the deadline reserved for stopped readers to return.</param>
internal readonly record struct ExternalProcessCleanupTiming(
    TimeSpan Deadline,
    TimeSpan HeldOutputGrace,
    TimeSpan ReaderStopReserve)
{
    /// <summary>Production timing: 5 s in total, of which up to 2 s is exit grace and the last 1 s is reader stop.</summary>
    internal static ExternalProcessCleanupTiming Default { get; } = new(
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(1));

    /// <summary>Absolute Stopwatch timestamps for the reader-stop point and the single deadline.</summary>
    /// <remarks>
    /// The reader stop lies inside the deadline (deadline minus reserve); the reserve is not added after the
    /// deadline, so every wait shares the one deadline (owner decision 86).
    /// </remarks>
    internal CleanupSchedule Schedule(long signaledAt)
    {
        long deadlineTicks = Ticks(Deadline);
        long reserveTicks = Ticks(ReaderStopReserve);
        return new CleanupSchedule(
            signaledAt + Ticks(HeldOutputGrace),
            signaledAt + deadlineTicks - reserveTicks,
            signaledAt + deadlineTicks);
    }

    internal static long Ticks(TimeSpan span)
    {
        return (long)(span.TotalSeconds * Stopwatch.Frequency);
    }

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Deadline, TimeSpan.Zero, nameof(Deadline));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(HeldOutputGrace, TimeSpan.Zero, nameof(HeldOutputGrace));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ReaderStopReserve, TimeSpan.Zero, nameof(ReaderStopReserve));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            HeldOutputGrace + ReaderStopReserve,
            Deadline,
            nameof(HeldOutputGrace));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            Deadline,
            TimeSpan.FromMilliseconds(int.MaxValue),
            nameof(Deadline));
    }
}

/// <summary>Absolute Stopwatch timestamps for one invocation's cleanup phase.</summary>
internal readonly record struct CleanupSchedule(long HeldOutputGraceAt, long ReaderStopAt, long DeadlineAt);

/// <summary>
/// Process-wide hard cap on external-process invocations that are running or still cleaning up (ADR 0081, owner
/// decision 92). A slot is reserved atomically before a process starts and held for the whole invocation; if the
/// invocation's cleanup work is still running when the run returns, the same reservation stays held until that work
/// settles. The in-use count therefore never exceeds the limit, so detached resources cannot accumulate without bound.
/// </summary>
internal sealed class ExternalProcessCapacity(int limit)
{
    /// <summary>Fixed default limit. See ADR 0081 for the rationale.</summary>
    internal const int DefaultLimit = 8;

    private int _inUse;

    internal int Limit { get; } = limit > 0
        ? limit
        : throw new ArgumentOutOfRangeException(nameof(limit));

    /// <summary>Slots currently held by invocations that are running or still cleaning up.</summary>
    internal int InUse => Volatile.Read(ref _inUse);

    /// <summary>
    /// Atomically takes one slot when below the limit. On refusal nothing is taken and
    /// <paramref name="observedInUse"/> is the in-use count the reservation observed (at or above the limit):
    /// from the initial read when the capacity was already full, or from a failed compare-and-set.
    /// </summary>
    internal bool TryReserve(out int observedInUse)
    {
        int current = Volatile.Read(ref _inUse);
        while (current < Limit)
        {
            int seen = Interlocked.CompareExchange(ref _inUse, current + 1, current);
            if (seen == current)
            {
                observedInUse = current + 1;
                return true;
            }

            current = seen;
        }

        observedInUse = current;
        return false;
    }

    /// <summary>Returns one previously reserved slot. Called exactly once per successful reservation.</summary>
    internal void Release()
    {
        int updated = Interlocked.Decrement(ref _inUse);
        Debug.Assert(updated >= 0, "The invocation capacity was released more times than it was reserved.");
    }
}

/// <summary>Internal ordering points of one invocation, published to the test observer seam.</summary>
internal enum ExternalProcessRunnerPhase
{
    /// <summary>The process started and both drains and the exit observation are running.</summary>
    Started,

    /// <summary>The terminal signal is a natural exit, or the exit observation failed.</summary>
    ExitSignaled,

    /// <summary>The terminal signal is the manifest timeout.</summary>
    TimeoutSignaled,

    /// <summary>The terminal signal is caller cancellation.</summary>
    CancellationSignaled,

    /// <summary>After a natural exit the held-output grace elapsed with a stream still open.</summary>
    OutputHeldAfterExit,

    /// <summary>The single termination work item was started.</summary>
    TerminationStarted,

    /// <summary>The readers were asked to stop.</summary>
    ReaderStopRequested,

    /// <summary>The terminal decision is made; the result is returned or cancellation is thrown next.</summary>
    Returning,

    /// <summary>The run returned while cleanup work is still running; the invocation keeps its original slot.</summary>
    Detached,

    /// <summary>
    /// Final success signal: every background task settled, every handle was disposed without error, and the slot was
    /// returned, in that order.
    /// </summary>
    ResourcesReleased,

    /// <summary>
    /// Final failure signal: every background task settled and the slot was returned, but disposing at least one handle
    /// threw. The disposal faults were observed; <see cref="ResourcesReleased"/> is not published.
    /// </summary>
    ResourcesReleaseFailed,
}

/// <summary>Production behavior plus the narrow seams tests use to inject faults, blocking, ordering and limits.</summary>
internal sealed record ExternalProcessRunnerSeams(
    ExternalProcessCleanupTiming Timing,
    Action<Process> TerminateTree,
    Func<TextReader, CancellationToken, Task<BoundedProcessOutput>> Drain,
    Func<Process, CancellationToken, Task> ObserveExit,
    Action<ExternalProcessRunnerPhase>? Observe,
    ExternalProcessCapacity Capacity,
    Action<IDisposable> DisposeResource)
{
    internal static ExternalProcessRunnerSeams Production { get; } = new(
        ExternalProcessCleanupTiming.Default,
        static process => process.Kill(entireProcessTree: true),
        BoundedProcessOutputReader.DrainAsync,
        static (process, cancellationToken) => process.WaitForExitAsync(cancellationToken),
        Observe: null,
        new ExternalProcessCapacity(ExternalProcessCapacity.DefaultLimit),
        static resource => resource.Dispose());
}
