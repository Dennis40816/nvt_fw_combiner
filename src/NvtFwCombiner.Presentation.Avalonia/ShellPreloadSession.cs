using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal enum ShellPreloadStageState { Pending, DependencyBlocked, Running, Succeeded, Failed, Skipped, Cancelled }

internal readonly record struct ShellPreloadAttemptIdentity(long SessionGeneration, string StageId, int AttemptNumber);

internal sealed class ShellPreloadSupersededException : Exception;

internal sealed record ShellPreloadAttemptSnapshot(
    ShellPreloadAttemptIdentity Identity,
    ShellPreloadStageState State,
    double? Progress,
    long? CompletedWork = null,
    long? TotalWork = null,
    string Diagnostic = "");

internal sealed record ShellPreloadStageSnapshot(
    string Id,
    int Index,
    int Count,
    bool IsRequired,
    string Title,
    string Detail,
    string RetryLabel,
    string SkipLabel,
    bool IsReducedMotionEnabled,
    ShellPreloadStageState State,
    ShellPreloadAttemptSnapshot? CurrentAttempt,
    ShellPreloadAttemptSnapshot? PreviousAttempt)
{
    internal bool CanRetry => !IsRequired && State == ShellPreloadStageState.Failed;
    internal bool CanSkip => CanRetry;
    internal bool IsRunning => State == ShellPreloadStageState.Running;
    internal bool HasProgress => CurrentAttempt?.Progress is not null;
    internal bool HasWork => CurrentAttempt?.CompletedWork is not null;
    internal bool IsIndeterminate => IsRunning && !HasProgress && !IsReducedMotionEnabled;
    internal bool HasDiagnostic => !string.IsNullOrWhiteSpace(CurrentAttempt?.Diagnostic);
    internal double Progress => CurrentAttempt?.Progress ?? 0;
    internal string ProgressLabel => HasProgress ? $"{Progress * 100:0}%" : string.Empty;
    internal string WorkLabel => HasWork ? $"{CurrentAttempt!.CompletedWork} / {CurrentAttempt.TotalWork}" : string.Empty;
    internal string RetryAccessibleLabel => $"{RetryLabel}: {Title}";
    internal string SkipAccessibleLabel => $"{SkipLabel}: {Title}";
    private string AccessibleProgressLabel => HasProgress ? $"{Math.Floor(Progress * 10) * 10:0}%" : string.Empty;
    internal string PositionLabel => $"{Index} / {Count}";
    internal string AccessibleStatus => string.Join(" · ", new[]
    {
        PositionLabel,
        Title,
        Detail,
        AccessibleProgressLabel,
        CurrentAttempt?.Diagnostic ?? string.Empty,
    }.Where(static value => value.Length > 0));
}

internal sealed record ShellOptionalPreloadWork(
    Action ApplyLaunchPage,
    Func<CancellationToken, Task> RestoreHistory,
    Func<Action<long, long>, CancellationToken, Task>? LoadStartupReport,
    Func<CancellationToken, Task> RefreshDiagnostics,
    Func<Action<int, int>, Func<bool>, CancellationToken, Task> WarmDeferredViews,
    Func<Action<long, long>, CancellationToken, Task>? RefreshExternalEnvironment = null);

internal sealed class ShellPreloadSession : ObservableObject, IDisposable
{
    internal const string CatalogStageId = "canonical-catalog";
    internal const string HistoryStageId = "report-history";
    internal const string ReportStageId = "startup-report";
    internal const string DiagnosticsStageId = "system-diagnostics";
    internal const string ExternalEnvironmentStageId = "external-environment";
    internal const string ViewsStageId = "deferred-views";
    internal const int OptionalWorkerBudget = 2;
    internal static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly ObservableCollection<ShellPreloadStageSnapshot> _stages;
    private readonly Dictionary<string, int> _stageIndices;
    private readonly Action<ShellPreloadStageSnapshot> _report;
    private readonly TimeSpan _drainTimeout;
    private readonly CancellationTokenSource _cancellation = new();
    private CancellationTokenSource _optionalCancellation = new();
    private readonly ConcurrentDictionary<string, Task> _active = new(StringComparer.Ordinal);
    private static long s_generation;
    private ShellTextResources _text;
    private string _accessibleStatus = string.Empty;
    private ShellOptionalPreloadWork? _optionalWork;
    private int _closed;

    internal ShellPreloadSession(
        Action<ShellPreloadStageSnapshot> report,
        ShellTextResources text,
        bool includeStartupReport = false,
        TimeSpan? drainTimeout = null)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _drainTimeout = drainTimeout ?? DrainTimeout;
        string[] ids = includeStartupReport
            ? [CatalogStageId, HistoryStageId, ReportStageId, DiagnosticsStageId,
                ExternalEnvironmentStageId, ViewsStageId]
            : [CatalogStageId, HistoryStageId, DiagnosticsStageId,
                ExternalEnvironmentStageId, ViewsStageId];
        _stages = new(ids.Select((id, index) => NewStage(id, index + 1, ids.Length)));
        _stageIndices = ids.Select((id, index) => (id, index)).ToDictionary(
            static item => item.id,
            static item => item.index,
            StringComparer.Ordinal);
        Stages = new(_stages);
    }

    internal ReadOnlyObservableCollection<ShellPreloadStageSnapshot> Stages { get; }
    internal ShellPreloadStageSnapshot CatalogStage => Stage(CatalogStageId);
    internal ShellPreloadStageSnapshot? SummaryStage => _optionalWork is null ? null :
        SnapshotStages().FirstOrDefault(static stage => !stage.IsRequired && stage.State is
            ShellPreloadStageState.Pending or ShellPreloadStageState.DependencyBlocked or
            ShellPreloadStageState.Running or ShellPreloadStageState.Failed) ?? _stages[^1];
    internal bool HasOptionalStatus => SummaryStage is
    {
        State:
        ShellPreloadStageState.Pending or ShellPreloadStageState.DependencyBlocked or
        ShellPreloadStageState.Running or ShellPreloadStageState.Failed
    };
    internal string AccessibleStatus => _accessibleStatus;
    internal bool CanCancelOptionals => _optionalWork is not null && SnapshotStages().Any(static stage =>
        !stage.IsRequired && stage.State is ShellPreloadStageState.Pending or
            ShellPreloadStageState.DependencyBlocked or ShellPreloadStageState.Running);
    internal long Generation { get; } = Interlocked.Increment(ref s_generation);
    internal bool CanRetryCatalog => !_cancellation.IsCancellationRequested &&
        !IsActive(CatalogStageId) && CatalogStage.State == ShellPreloadStageState.Failed;

    internal void AdoptReadyCatalog()
    {
        ThrowIfClosed();
        if (CatalogStage.CurrentAttempt is null)
        {
            ShellPreloadAttemptIdentity identity = Begin(CatalogStageId, determinate: true);
            Set(identity, ShellPreloadStageState.Succeeded, 1);
        }
    }

    internal Task<CapabilityCatalogReloadResult> RunCatalogAsync(
        ICanonicalCapabilityCatalogLoader loader,
        Func<CancellationToken, ValueTask> apply,
        bool retry,
        CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(apply);
        if (retry ? !CanRetryCatalog : CatalogStage.CurrentAttempt is not null)
        {
            throw new InvalidOperationException("The catalog attempt cannot start in its current state.");
        }

        ShellPreloadAttemptIdentity identity = Begin(CatalogStageId, determinate: true);
        Task<CapabilityCatalogReloadResult> task = RunCatalogCoreAsync(loader, apply, identity, cancellationToken);
        _active[CatalogStageId] = task;
        return task;
    }

    internal async Task RunOptionalStagesAsync(
        ShellOptionalPreloadWork work,
        CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        ArgumentNullException.ThrowIfNull(work);
        if (CatalogStage.State != ShellPreloadStageState.Succeeded || _optionalWork is not null)
        {
            throw new InvalidOperationException("Optional shell preload cannot start in its current state.");
        }

        _optionalWork = work;
        PresentationObserver.Invoke(NotifyStatus);
        PresentationObserver.Invoke(work.ApplyLaunchPage);
        using CancellationTokenSource linked = LinkOptionals(cancellationToken);
        await Task.WhenAll(
            RunReportChainAsync(linked.Token),
            RunExternalEnvironmentAndDiagnosticsAsync(linked.Token),
            StartOptionalAsync(ViewsStageId, linked.Token));
        await AwaitActiveOptionalsAsync();
    }

    internal async Task<bool> TryRetryOptionalAsync(string stageId, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _closed) != 0 || _optionalWork is null ||
            !Stage(stageId).CanRetry || IsActive(stageId))
        {
            return false;
        }

        if (_optionalCancellation.IsCancellationRequested)
        {
            await DrainAsync();
            if (Volatile.Read(ref _closed) != 0 || !Stage(stageId).CanRetry || IsActive(stageId) ||
                _active.Any(static pair => pair.Key != CatalogStageId && !pair.Value.IsCompleted))
            {
                return false;
            }
        }
        using CancellationTokenSource linked = LinkOptionals(cancellationToken);
        bool succeeded = await StartOptionalAsync(stageId, linked.Token);
        if (stageId == HistoryStageId && succeeded &&
            TryStage(ReportStageId) is { State: ShellPreloadStageState.DependencyBlocked })
        {
            SetState(ReportStageId, ShellPreloadStageState.Pending);
            _ = await StartOptionalAsync(ReportStageId, linked.Token);
        }
        return true;
    }

    internal bool TrySkipOptional(string stageId)
    {
        if (Volatile.Read(ref _closed) != 0 || !Stage(stageId).CanSkip || IsActive(stageId))
        {
            return false;
        }

        SetState(stageId, ShellPreloadStageState.Skipped);
        return true;
    }

    internal async Task CancelOptionalsAndDrainAsync()
    {
        _optionalCancellation.Cancel();
        for (int index = 0; index < _stages.Count; index++)
        {
            ShellPreloadStageSnapshot stage = _stages[index];
            if (!stage.IsRequired && stage.State is
                ShellPreloadStageState.Pending or ShellPreloadStageState.DependencyBlocked)
            {
                SetState(stage.Id, ShellPreloadStageState.Cancelled);
            }
        }
        InvalidateRunningAttempts(includeRequired: false);
        await DrainAsync();
    }

    internal async Task CancelAndDrainAsync()
    {
        _cancellation.Cancel();
        _optionalCancellation.Cancel();
        InvalidateRunningAttempts(includeRequired: true);
        await DrainAsync();
        Close();
    }

    internal void Relocalize(ShellTextResources text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        for (int index = 0; index < _stages.Count; index++)
        {
            ShellPreloadStageSnapshot stage = _stages[index];
            Publish(stage with
            {
                Title = Title(stage.Id),
                Detail = Detail(stage.State),
                RetryLabel = _text.RetryLabel,
                SkipLabel = _text.SkipPreloadLabel,
            });
        }
    }

    internal void SetReducedMotion(bool enabled)
    {
        foreach (ShellPreloadStageSnapshot stage in SnapshotStages())
        {
            if (stage.IsReducedMotionEnabled != enabled)
            {
                Publish(stage with { IsReducedMotionEnabled = enabled });
            }
        }
    }

    public void Dispose()
    {
        Close();
        _optionalCancellation.Dispose();
        _cancellation.Dispose();
    }

    private async Task<CapabilityCatalogReloadResult> RunCatalogCoreAsync(
        ICanonicalCapabilityCatalogLoader loader,
        Func<CancellationToken, ValueTask> apply,
        ShellPreloadAttemptIdentity identity,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellation.Token, cancellationToken);
        CancellationToken token = linked.Token;
        CapabilityCatalogReloadResult? terminal = null;
        double? progress = null;
        try
        {
            await foreach (CanonicalCapabilityCatalogLoadUpdate update in loader.LoadAsync(token).WithCancellation(token))
            {
                token.ThrowIfCancellationRequested();
                if (terminal is not null)
                {
                    throw new InvalidOperationException("Catalog update followed its terminal result.");
                }
                if (update.Result is { } result)
                {
                    if ((result.Succeeded && update.Progress != 1) || (!result.Succeeded && update.Progress is not null))
                    {
                        throw new InvalidOperationException("Catalog terminal progress does not match its result.");
                    }
                    terminal = result;
                    continue;
                }

                progress = ValidateProgress(update.Progress, progress, allowOne: false);
                Set(identity, ShellPreloadStageState.Running, progress);
            }

            CapabilityCatalogReloadResult reload = terminal ??
                throw new InvalidOperationException("Catalog loading completed without a terminal result.");
            token.ThrowIfCancellationRequested();
            if (!reload.Succeeded)
            {
                string diagnostic = string.Join(Environment.NewLine,
                    reload.Issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
                Set(identity, ShellPreloadStageState.Failed, progress, diagnostic);
                return reload;
            }

            Set(identity, ShellPreloadStageState.Running, 1);
            await apply(token);
            token.ThrowIfCancellationRequested();
            Set(identity, ShellPreloadStageState.Succeeded, 1);
            return reload;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Set(identity, ShellPreloadStageState.Cancelled, progress);
            throw;
        }
        catch (Exception exception)
        {
            Set(identity, ShellPreloadStageState.Failed, progress, exception.Message);
            throw;
        }
    }

    private async Task RunReportChainAsync(CancellationToken cancellationToken)
    {
        bool succeeded = await StartOptionalAsync(HistoryStageId, cancellationToken);
        if (TryStage(ReportStageId) is null)
        {
            return;
        }
        if (!succeeded)
        {
            SetState(ReportStageId, Stage(HistoryStageId).State == ShellPreloadStageState.Failed
                ? ShellPreloadStageState.DependencyBlocked
                : ShellPreloadStageState.Cancelled);
            return;
        }
        _ = await StartOptionalAsync(ReportStageId, cancellationToken);
    }

    private async Task RunExternalEnvironmentAndDiagnosticsAsync(CancellationToken cancellationToken)
    {
        _ = await StartOptionalAsync(ExternalEnvironmentStageId, cancellationToken);
        if (Stage(DiagnosticsStageId).State == ShellPreloadStageState.Pending)
        {
            _ = await StartOptionalAsync(DiagnosticsStageId, cancellationToken);
        }
    }

    private Task<bool> StartOptionalAsync(string stageId, CancellationToken cancellationToken)
    {
        Task<bool> task = RunOptionalCoreAsync(stageId, cancellationToken);
        _active[stageId] = task;
        return task;
    }

    private async Task<bool> RunOptionalCoreAsync(string stageId, CancellationToken cancellationToken)
    {
        ShellPreloadAttemptIdentity identity = Begin(stageId, determinate: stageId == ViewsStageId);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOptionalAsync(stageId, identity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            double? progress = Stage(stageId).CurrentAttempt?.Progress;
            if (stageId == ViewsStageId && progress != 1)
            {
                throw new InvalidOperationException("Deferred view progress did not reach its terminal count.");
            }
            Set(identity, ShellPreloadStageState.Succeeded, progress);
            return true;
        }
        catch (ShellPreloadSupersededException)
        {
            Set(identity, ShellPreloadStageState.Cancelled, Stage(stageId).CurrentAttempt?.Progress);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Set(identity, ShellPreloadStageState.Cancelled, Stage(stageId).CurrentAttempt?.Progress);
            return false;
        }
        catch (Exception exception)
        {
            Set(identity, ShellPreloadStageState.Failed, Stage(stageId).CurrentAttempt?.Progress, exception.Message);
            return false;
        }
    }

    private Task ExecuteOptionalAsync(
        string stageId,
        ShellPreloadAttemptIdentity identity,
        CancellationToken cancellationToken)
    {
        ShellOptionalPreloadWork work = _optionalWork ?? throw new InvalidOperationException("Optional preload is unavailable.");
        return stageId switch
        {
            HistoryStageId => work.RestoreHistory(cancellationToken),
            ReportStageId => (work.LoadStartupReport ??
                throw new InvalidOperationException("Startup report is unavailable."))(
                    (completed, total) => SetWork(identity, completed, total),
                    cancellationToken),
            DiagnosticsStageId => work.RefreshDiagnostics(cancellationToken),
            ExternalEnvironmentStageId => work.RefreshExternalEnvironment is { } refreshExternal
                ? refreshExternal(
                    (completed, total) => SetWork(identity, completed, total),
                    cancellationToken)
                : Task.CompletedTask,
            ViewsStageId => work.WarmDeferredViews(
                (completed, total) => SetWork(identity, completed, total),
                () => IsCurrent(identity),
                cancellationToken),
            _ => throw new InvalidOperationException($"Unknown optional preload stage '{stageId}'."),
        };
    }

    private ShellPreloadAttemptIdentity Begin(string stageId, bool determinate)
    {
        ShellPreloadStageSnapshot stage = Stage(stageId);
        if (stage.State is ShellPreloadStageState.Running or ShellPreloadStageState.Succeeded or
            ShellPreloadStageState.Skipped or ShellPreloadStageState.Cancelled)
        {
            throw new InvalidOperationException("The preload stage cannot start in its current state.");
        }

        var identity = new ShellPreloadAttemptIdentity(
            Generation, stageId, checked((stage.CurrentAttempt?.Identity.AttemptNumber ?? 0) + 1));
        Publish(stage with
        {
            State = ShellPreloadStageState.Running,
            Detail = _text.PreloadRunningDetail,
            CurrentAttempt = new(identity, ShellPreloadStageState.Running, determinate ? 0 : null),
            PreviousAttempt = stage.CurrentAttempt is { } previous
                ? previous with { Progress = null, CompletedWork = null, TotalWork = null }
                : null,
        });
        return identity;
    }

    private void SetWork(ShellPreloadAttemptIdentity identity, long completed, long total)
    {
        ThrowIfClosed();
        ShellPreloadAttemptSnapshot current = Stage(identity.StageId).CurrentAttempt ??
            throw new InvalidOperationException("The preload stage has no attempt.");
        if (current.Identity != identity || current.State != ShellPreloadStageState.Running)
        {
            throw new InvalidOperationException("The preload attempt is stale or terminal.");
        }
        if (total < 0 || completed < 0 || completed > total ||
            (current.TotalWork is { } previousTotal && previousTotal != total) ||
            (current.CompletedWork is { } previousCompleted && completed < previousCompleted))
        {
            throw new InvalidOperationException("Preload reported invalid work progress.");
        }
        double ratio = total == 0 ? 1 : (double)completed / total;
        Set(identity, ShellPreloadStageState.Running, ValidateProgress(ratio, current.Progress, allowOne: true),
            completedWork: completed, totalWork: total);
    }

    private void Set(
        ShellPreloadAttemptIdentity identity,
        ShellPreloadStageState state,
        double? progress,
        string diagnostic = "",
        long? completedWork = null,
        long? totalWork = null)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }
        ShellPreloadStageSnapshot stage = Stage(identity.StageId);
        ShellPreloadAttemptSnapshot current = stage.CurrentAttempt ??
            throw new InvalidOperationException("The preload stage has no attempt.");
        if (current.Identity != identity)
        {
            throw new InvalidOperationException("The preload attempt is stale or terminal.");
        }
        if (current.State != ShellPreloadStageState.Running)
        {
            return;
        }
        Publish(stage with
        {
            State = state,
            Detail = Detail(state),
            CurrentAttempt = current with
            {
                State = state,
                Progress = progress,
                CompletedWork = completedWork ?? current.CompletedWork,
                TotalWork = totalWork ?? current.TotalWork,
                Diagnostic = diagnostic,
            },
        });
    }

    private void SetState(string stageId, ShellPreloadStageState state)
    {
        ShellPreloadStageSnapshot stage = Stage(stageId);
        Publish(stage with { State = state, Detail = Detail(state) });
    }

    private void Publish(ShellPreloadStageSnapshot stage)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }
        PresentationObserver.Invoke(() => _stages[_stageIndices[stage.Id]] = stage);
        PresentationObserver.Invoke(NotifyStatus);
        PresentationObserver.Invoke(() => _report(stage));
    }

    private void NotifyStatus()
    {
        OnPropertyChanged(nameof(SummaryStage));
        OnPropertyChanged(nameof(HasOptionalStatus));
        OnPropertyChanged(nameof(CanCancelOptionals));
        _ = SetProperty(ref _accessibleStatus,
            HasOptionalStatus ? SummaryStage?.AccessibleStatus ?? string.Empty : string.Empty,
            nameof(AccessibleStatus));
    }

    private ShellPreloadStageSnapshot NewStage(string id, int index, int count)
    {
        return new(id, index, count, id == CatalogStageId, Title(id), _text.PreloadPendingDetail,
            _text.RetryLabel, _text.SkipPreloadLabel, false,
            ShellPreloadStageState.Pending, null, null);
    }

    internal ShellPreloadStageSnapshot Stage(string id)
    {
        return TryStage(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown preload stage.");
    }

    private ShellPreloadStageSnapshot? TryStage(string id)
    {
        return _stageIndices.TryGetValue(id, out int index) ? _stages[index] : null;
    }

    private IEnumerable<ShellPreloadStageSnapshot> SnapshotStages()
    {
        return Enumerable.Range(0, _stages.Count).Select(index => _stages[index]);
    }

    private bool IsActive(string id)
    {
        return _active.TryGetValue(id, out Task? task) && !task.IsCompleted;
    }

    private bool IsCurrent(ShellPreloadAttemptIdentity identity)
    {
        return Volatile.Read(ref _closed) == 0 &&
            TryStage(identity.StageId)?.CurrentAttempt is { State: ShellPreloadStageState.Running } attempt &&
            attempt.Identity == identity;
    }

    private CancellationTokenSource LinkOptionals(CancellationToken cancellationToken)
    {
        if (_optionalCancellation.IsCancellationRequested)
        {
            if (_active.Any(static pair => pair.Key != CatalogStageId && !pair.Value.IsCompleted))
            {
                throw new InvalidOperationException("Cancelled optional preload has not drained.");
            }
            _optionalCancellation.Dispose();
            _optionalCancellation = new();
        }
        return CancellationTokenSource.CreateLinkedTokenSource(
            _cancellation.Token, _optionalCancellation.Token, cancellationToken);
    }

    private string Title(string id)
    {
        return id switch
        {
            CatalogStageId => _text.CatalogLoadingTitle,
            HistoryStageId => _text.PreloadHistoryTitle,
            ReportStageId => _text.PreloadReportTitle,
            DiagnosticsStageId => _text.SystemInformationLabel,
            ExternalEnvironmentStageId => _text.PreloadExternalEnvironmentTitle,
            ViewsStageId => _text.PreloadViewsTitle,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown preload stage."),
        };
    }

    private string Detail(ShellPreloadStageState state)
    {
        return state switch
        {
            ShellPreloadStageState.Pending => _text.PreloadPendingDetail,
            ShellPreloadStageState.DependencyBlocked => _text.PreloadBlockedDetail,
            ShellPreloadStageState.Running => _text.PreloadRunningDetail,
            ShellPreloadStageState.Succeeded => _text.PreloadSucceededDetail,
            ShellPreloadStageState.Failed => _text.PreloadFailedDetail,
            ShellPreloadStageState.Skipped => _text.PreloadSkippedDetail,
            ShellPreloadStageState.Cancelled => _text.PreloadCancelledDetail,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private static double ValidateProgress(double? progress, double? previous, bool allowOne)
    {
        double next = progress ?? throw new InvalidOperationException("Preload reported an empty progress update.");
        double upper = allowOne ? 1 : double.BitDecrement(1);
        return !double.IsFinite(next) || next < 0 || next > upper || next < previous
            ? throw new InvalidOperationException("Preload reported invalid progress.")
            : next;
    }

    private async Task DrainAsync()
    {
        _ = await Task.WhenAny(Task.WhenAll(_active.Values), Task.Delay(_drainTimeout));
    }

    private async Task AwaitActiveOptionalsAsync()
    {
        while (true)
        {
            Task[] active = [.. _active
                .Where(static pair => pair.Key != CatalogStageId && !pair.Value.IsCompleted)
                .Select(static pair => pair.Value)];
            if (active.Length > 0)
            {
                await Task.WhenAll(active);
                continue;
            }
            if (!SnapshotStages().Any(static stage => !stage.IsRequired && stage.IsRunning))
            {
                return;
            }
            await Task.Yield();
        }
    }

    private void InvalidateRunningAttempts(bool includeRequired)
    {
        for (int index = 0; index < _stages.Count; index++)
        {
            ShellPreloadStageSnapshot stage = _stages[index];
            if ((includeRequired || !stage.IsRequired) &&
                stage.CurrentAttempt is { State: ShellPreloadStageState.Running } attempt)
            {
                Publish(stage with
                {
                    State = ShellPreloadStageState.Cancelled,
                    Detail = Detail(ShellPreloadStageState.Cancelled),
                    CurrentAttempt = attempt with { State = ShellPreloadStageState.Cancelled },
                });
            }
        }
    }

    private void Close()
    {
        _ = Interlocked.Exchange(ref _closed, 1);
    }

    private void ThrowIfClosed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
    }
}
