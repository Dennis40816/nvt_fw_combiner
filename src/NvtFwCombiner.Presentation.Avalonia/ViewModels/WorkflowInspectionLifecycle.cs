namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum WorkflowInspectionAttemptState { Idle, Running, Succeeded, Failed, Cancelled }

internal delegate Task WorkflowInspectionOperation(
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

    internal void ApplyText(ShellTextResources text)
    {
        Array.ForEach(_lifecycles, lifecycle => lifecycle.ApplyText(text));
    }

    internal void Invalidate()
    {
        Array.ForEach(_lifecycles, static lifecycle => lifecycle.Invalidate());
    }

    internal void SetReducedMotion(bool enabled)
    {
        Array.ForEach(_lifecycles, lifecycle => lifecycle.Loading.SetReducedMotion(enabled));
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
    private AuthoringInspectionProgress? _progress;

    internal WorkflowInspectionLifecycle(Action? statusChanged = null)
    {
        _statusChanged = statusChanged ?? (() => { });
        Loading = new(
            () => TryRetryAsync(CancellationToken.None),
            () => CancelAsync(CancellationToken.None));
    }

    public ForegroundLoadingState Loading { get; }
    internal long Generation => Volatile.Read(ref _generation);
    internal WorkflowInspectionAttemptState State { get; private set; }
    internal int? CompletedWork => _progress?.CompletedWork;
    internal int? TotalWork => _progress?.TotalWork;
    internal bool IsRunning => State == WorkflowInspectionAttemptState.Running;
    internal Task ActiveTask => Volatile.Read(ref _activeTask);

    internal Task StartAsync(
        ShellTextResources text,
        WorkflowInspectionOperation execute,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(execute);
        return Schedule(new(text, execute), cancellationToken);
    }

    internal Task<bool> TryRetryAsync(CancellationToken cancellationToken)
    {
        return State == WorkflowInspectionAttemptState.Failed && _request is { } request
            ? Schedule(request, cancellationToken)
            : Task.FromResult(false);
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
        ArgumentNullException.ThrowIfNull(text);
        if (_request is null || State is not (
                WorkflowInspectionAttemptState.Running or WorkflowInspectionAttemptState.Failed))
        {
            return;
        }

        _request = _request with { Text = text };
        Present();
    }

    private Task<bool> Schedule(
        WorkflowInspectionRequest request,
        CancellationToken cancellationToken)
    {
        lock (_admissionLock)
        {
            long generation = Interlocked.Increment(ref _generation);
            CancelActive();
            Task<bool> active = RunAfterAsync(
                _activeTask,
                generation,
                request,
                cancellationToken);
            Volatile.Write(ref _activeTask, active);
            return active;
        }
    }

    private async Task<bool> RunAfterAsync(
        Task predecessor,
        long generation,
        WorkflowInspectionRequest request,
        CancellationToken cancellationToken)
    {
        await predecessor;
        if (!IsCurrent(generation))
        {
            return false;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken requestCancellation = cancellation.Token;
        _cancellation = cancellation;
        _request = request;
        _failureType = null;
        _progress = null;
        SetState(WorkflowInspectionAttemptState.Running);
        Present();
        try
        {
            requestCancellation.ThrowIfCancellationRequested();
            SynchronizationContext? presentationContext = SynchronizationContext.Current;
            var progress = new WorkflowInspectionProgressObserver(value =>
                Report(generation, presentationContext, value, requestCancellation));
            await request.Execute(progress, () => IsCurrent(generation), requestCancellation);
            Finish(generation, WorkflowInspectionAttemptState.Succeeded);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Finish(generation, WorkflowInspectionAttemptState.Cancelled);
        }
        catch (Exception exception)
        {
            _failureType = exception.GetType().Name;
            Finish(generation, WorkflowInspectionAttemptState.Failed);
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
            }
        }
        return true;
    }

    internal void Report(
        long generation,
        SynchronizationContext? presentationContext,
        AuthoringInspectionProgress progress,
        CancellationToken requestCancellation)
    {
        if (!IsCurrent(generation) || requestCancellation.IsCancellationRequested)
        {
            throw new OperationCanceledException(requestCancellation);
        }

        void Deliver()
        {
            if (!IsCurrent(generation) || requestCancellation.IsCancellationRequested)
            {
                throw new OperationCanceledException(requestCancellation);
            }
            AuthoringInspectionProgress? previous = _progress;
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
            _progress = progress;
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

        string detail = _progress is { } progress
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
        try
        {
            Volatile.Read(ref _cancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The request completed between observing and cancelling its source.
        }
    }

    private sealed record WorkflowInspectionRequest(
        ShellTextResources Text,
        WorkflowInspectionOperation Execute);
}
