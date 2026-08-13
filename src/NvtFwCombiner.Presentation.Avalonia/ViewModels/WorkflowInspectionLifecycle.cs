namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum WorkflowInspectionAttemptState { Idle, Running, Succeeded, Failed, Cancelled }

internal readonly record struct WorkflowInspectionOperationResult(bool Succeeded, string? FailureType = null);

internal delegate Task<WorkflowInspectionOperationResult> WorkflowInspectionOperation(
    IProgress<AuthoringInspectionProgress> progress,
    Func<bool> isCurrent,
    CancellationToken cancellationToken);

internal sealed class WorkflowInspectionProgressObserver(
    Action<AuthoringInspectionProgress> report) : IProgress<AuthoringInspectionProgress>
{
    public void Report(AuthoringInspectionProgress progress)
    {
        report(progress);
    }
}

internal sealed class WorkflowInspectionSet(
    Action statusChanged,
    string secondaryMode,
    string tertiaryMode)
{
    private readonly WorkflowInspectionLifecycle[] _lifecycles =
    [
        new(statusChanged),
        new(statusChanged),
        new(statusChanged),
    ];

    internal WorkflowInspectionLifecycle this[string mode] => mode switch
    {
        _ when mode == secondaryMode => _lifecycles[1],
        _ when mode == tertiaryMode => _lifecycles[2],
        _ => _lifecycles[0],
    };

    internal void ForEach(Action<WorkflowInspectionLifecycle> action)
    {
        Array.ForEach(_lifecycles, action);
    }
}

/// <summary>
/// Owns one workflow's inspection request generation, cancellation, progress, and retry.
/// Feature owners still interpret, retain, and publish every inspection result.
/// </summary>
internal sealed class WorkflowInspectionLifecycle
{
    private readonly Lock _admissionLock = new();
    private readonly Action _statusChanged;
    private Task _activeTask = Task.CompletedTask;
    private CancellationTokenSource? _cancellation;
    private WorkflowInspectionRequest? _request;
    private string? _failureType;
    private long _generation;

    internal WorkflowInspectionLifecycle(Action? statusChanged = null)
    {
        _statusChanged = statusChanged ?? (() => { });
        Loading = new(
            () => TryRetryAsync(CancellationToken.None),
            () => CancelAsync(CancellationToken.None));
    }

    public ForegroundLoadingState Loading { get; }
    internal WorkflowInspectionAttemptState State { get; private set; }
    internal AuthoringInspectionProgress? Progress { get; private set; }
    internal bool IsRunning => State == WorkflowInspectionAttemptState.Running;
    internal Task ActiveTask => Volatile.Read(ref _activeTask);

    internal Task<WorkflowInspectionAttemptState> StartAsync(
        ShellTextResources text,
        WorkflowInspectionOperation execute,
        CancellationToken cancellationToken)
    {
        return Schedule(new(text, execute), cancellationToken);
    }

    internal Task TryRetryAsync(CancellationToken cancellationToken)
    {
        return State == WorkflowInspectionAttemptState.Failed && _request is { } request
            ? Schedule(request, cancellationToken)
            : Task.CompletedTask;
    }

    internal Task CancelAsync(CancellationToken cancellationToken)
    {
        CancelActive();
        return ActiveTask.WaitAsync(cancellationToken);
    }

    internal void Invalidate()
    {
        CancelActive();
        _ = Interlocked.Increment(ref _generation);
        if (IsRunning)
        {
            SetState(WorkflowInspectionAttemptState.Cancelled);
        }
        Loading.Complete();
    }

    internal void ApplyText(ShellTextResources text)
    {
        if (_request is null || State is not (
                WorkflowInspectionAttemptState.Running or WorkflowInspectionAttemptState.Failed))
        {
            return;
        }

        _request = _request with { Text = text };
        Present();
    }

    private Task<WorkflowInspectionAttemptState> Schedule(
        WorkflowInspectionRequest request,
        CancellationToken cancellationToken)
    {
        lock (_admissionLock)
        {
            long generation = Interlocked.Increment(ref _generation);
            Task predecessor = ActiveTask;
            CancelActive();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Volatile.Write(ref _cancellation, cancellation);
            Task<WorkflowInspectionAttemptState> active =
                RunAfterAsync(predecessor, generation, request, cancellation);
            Volatile.Write(ref _activeTask, active);
            return active;
        }
    }

    private async Task<WorkflowInspectionAttemptState> RunAfterAsync(
        Task predecessor,
        long generation,
        WorkflowInspectionRequest request,
        CancellationTokenSource cancellation)
    {
        using CancellationTokenSource ownedCancellation = cancellation;
        CancellationToken requestCancellation = cancellation.Token;
        WorkflowInspectionAttemptState terminal = WorkflowInspectionAttemptState.Cancelled;
        try
        {
            await predecessor;
            if (!IsCurrent(generation))
            {
                return terminal;
            }

            _request = request;
            _failureType = null;
            Progress = null;
            SetState(WorkflowInspectionAttemptState.Running);
            requestCancellation.ThrowIfCancellationRequested();
            Present();
            SynchronizationContext? presentationContext = SynchronizationContext.Current;
            var progress = new WorkflowInspectionProgressObserver(value =>
                Report(generation, presentationContext, value, requestCancellation));
            WorkflowInspectionOperationResult result = await request.Execute(
                progress, () => IsCurrent(generation), requestCancellation);
            requestCancellation.ThrowIfCancellationRequested();
            _failureType = result.FailureType ?? nameof(WorkflowInspectionOperationResult);
            terminal = result.Succeeded
                ? WorkflowInspectionAttemptState.Succeeded
                : WorkflowInspectionAttemptState.Failed;
        }
        catch (OperationCanceledException)
        {
            terminal = WorkflowInspectionAttemptState.Cancelled;
        }
        catch (Exception exception)
        {
            if (!cancellation.IsCancellationRequested && IsCurrent(generation))
            {
                _failureType = exception.GetType().Name;
                terminal = WorkflowInspectionAttemptState.Failed;
            }
        }
        finally
        {
            lock (_admissionLock)
            {
                if (ReferenceEquals(_cancellation, cancellation))
                {
                    _cancellation = null;
                }
            }
        }
        Finish(generation, terminal);
        return terminal;
    }

    internal void Report(
        long generation,
        SynchronizationContext? presentationContext,
        AuthoringInspectionProgress progress,
        CancellationToken requestCancellation)
    {
        void Deliver()
        {
            if (!IsCurrent(generation) || requestCancellation.IsCancellationRequested)
            {
                throw new OperationCanceledException(requestCancellation);
            }
            if (!IsRunning)
            {
                throw new InvalidOperationException("Inspection progress cannot update a terminal attempt.");
            }
            AuthoringInspectionProgress? previous = Progress;
            if (progress.TotalWork <= 0 || progress.CompletedWork < 0 ||
                progress.CompletedWork > progress.TotalWork ||
                (previous is { } prior && (progress.TotalWork != prior.TotalWork ||
                    progress.CompletedWork < prior.CompletedWork)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(progress),
                    progress,
                    "Inspection progress must be monotonic with one stable positive total.");
            }

            bool announce = (long)progress.CompletedWork * 10 / progress.TotalWork >
                (previous is { } reported
                    ? (long)reported.CompletedWork * 10 / reported.TotalWork
                    : 0);
            Progress = progress;
            ShellTextResources text = _request!.Text;
            Loading.ReportProgress(
                (double)progress.CompletedWork / progress.TotalWork,
                text.GetFirmwareInspectionProgressDetail(progress.CompletedWork, progress.TotalWork),
                announce);
        }

        if (presentationContext is null || presentationContext == SynchronizationContext.Current)
        {
            Deliver();
        }
        else
        {
            presentationContext.Send(static state => ((Action)state!).Invoke(), (Action)Deliver);
        }
    }

    internal bool IsCurrent(long generation)
    {
        return generation == Volatile.Read(ref _generation);
    }

    private void Finish(long generation, WorkflowInspectionAttemptState state)
    {
        if (!IsCurrent(generation) || !IsRunning)
        {
            return;
        }
        SetState(state);
        if (state == WorkflowInspectionAttemptState.Failed)
        {
            Present();
        }
        else
        {
            Loading.Complete();
        }
    }

    private void Present()
    {
        ShellTextResources text = _request!.Text;
        if (State == WorkflowInspectionAttemptState.Failed)
        {
            Loading.Fail(
                text.FirmwareInspectionFailedTitle,
                text.GetFirmwareInspectionFailureDetail(_failureType!),
                text.RetryLabel);
            return;
        }

        string detail = Progress is { } progress
            ? text.GetFirmwareInspectionProgressDetail(progress.CompletedWork, progress.TotalWork)
            : text.FirmwareInspectionLoadingStatus;
        Loading.Begin(
            text.FirmwareInspectionLoadingTitle,
            detail,
            Loading.Progress,
            text.FirmwareInspectionCancelLabel);
    }

    private void SetState(WorkflowInspectionAttemptState value)
    {
        State = value;
        PresentationObserver.Invoke(_statusChanged);
    }

    private void CancelActive()
    {
        lock (_admissionLock)
        {
            _cancellation?.Cancel();
        }
    }

    private sealed record WorkflowInspectionRequest(ShellTextResources Text, WorkflowInspectionOperation Execute);
}
