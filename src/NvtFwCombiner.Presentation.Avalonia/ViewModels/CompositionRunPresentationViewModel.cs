using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns one active Preview/Build lifetime and its immutable context projection.</summary>
internal sealed class CompositionRunPresentationViewModel : ObservableObject
{
    private readonly CompositionRunStateBindings _stateBindings;
    private sealed record RunAttempt(CompositionRunContext Context, Guid Id) : IDisposable
    {
        internal CancellationTokenSource Cancellation { get; } = new();
        public void Dispose()
        {
            Cancellation.Dispose();
        }
    }
    private RunAttempt? _activeAttempt;
    private bool _activeRunIsBuild;
    public bool ActiveRunShowsNumberSelector { get; private set; }
    private string ActiveRunDeviceContextRefreshSummary { get; set; } = string.Empty;

    public string ActiveRunIc { get; private set; } = string.Empty;

    public string ActiveRunNumber { get; private set; } = string.Empty;

    public string ActiveRunMode { get; private set; } = string.Empty;

    /// <summary>Gets the IC identity that the device-context surface must display.</summary>
    public string DisplayedDeviceIc => IsRunInProgress ? ActiveRunIc : _stateBindings.SelectedIc();

    /// <summary>Gets the Number identity that the device-context surface must display.</summary>
    public string DisplayedDeviceNumber => IsRunInProgress ? ActiveRunNumber : _stateBindings.SelectedNumber();

    public string DisplayedDeviceContextRefreshSummary => IsRunInProgress
        ? ActiveRunDeviceContextRefreshSummary
        : _stateBindings.DeviceContextRefreshSummary();

    /// <summary>Gets the immutable active-run identity shown beside the phase stepper.</summary>
    public string ActiveRunContextLabel => ActiveRunShowsNumberSelector
        ? $"{WorkflowModeDisplayConverters.GetDisplayName(ActiveRunMode)} · {ActiveRunIc} / {ActiveRunNumber}"
        : $"{WorkflowModeDisplayConverters.GetDisplayName(ActiveRunMode)} · {ActiveRunIc}";

    /// <summary>Gets the localized projection of Application-owned composition phases.</summary>
    public CompositionRunProgressViewModel CompositionProgress { get; }

    public UiRunResultViewModel LastRunResult => _stateBindings.DisplayedOwner().LastRunResult;

    /// <summary>True while one composition Preview or Build owns the external processing lifetime.</summary>
    public bool IsRunInProgress => _activeAttempt is not null;

    public string RunProgressAccessibleLabel => _activeRunIsBuild
        ? _stateBindings.Text().BuildRunProgressAccessibleLabel
        : _stateBindings.Text().PreviewRunProgressAccessibleLabel;

    /// <summary>Gets the current typed phase status, or the action-level fallback before Application starts.</summary>
    public string RunProgressStatusLabel => CompositionProgress.HasTypedProgress
        ? CompositionProgress.AccessibleStatus
        : RunProgressAccessibleLabel;

    public string RunProgressDisplayLabel => CompositionProgress.HasTypedProgress
        ? CompositionProgress.CurrentStepLabel
        : RunProgressAccessibleLabel;

    /// <summary>True while an active run has supplied its Application-owned phase sequence.</summary>
    public bool HasTypedRunProgress => IsRunInProgress && CompositionProgress.HasTypedProgress;

    public bool ShouldAnimateRunProgress => IsRunInProgress &&
        !_stateBindings.IsReducedMotionEnabled() &&
        (!CompositionProgress.HasTypedProgress || CompositionProgress.ShouldAnimateActiveStep);

    internal CompositionRunPresentationViewModel(
        ShellLanguage language,
        CompositionRunStateBindings stateBindings)
    {
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        CompositionProgress = new CompositionRunProgressViewModel(language);
        CompositionProgress.PropertyChanged += CompositionProgress_OnPropertyChanged;
    }

    /// <summary>Cancels the active composition so external workers can terminate before the window closes.</summary>
    internal void CancelActiveRun()
    {
        _activeAttempt?.Cancellation.Cancel();
    }

    /// <summary>A delayed page cancellation can cancel only the exact attempt it observed.</summary>
    internal bool CancelRun(WorkflowRunState owner, Guid attemptId)
    {
        if (_activeAttempt is not { } attempt ||
            !ReferenceEquals(attempt.Context.Owner, owner) || attempt.Id != attemptId)
        {
            return false;
        }
        attempt.Cancellation.Cancel();
        return true;
    }

    private RunAttempt? BeginRun(CompositionRunContext context, bool build)
    {
        if (_activeAttempt is not null)
        {
            return null;
        }

        var attempt = new RunAttempt(context, Guid.NewGuid());
        if (Interlocked.CompareExchange(ref _activeAttempt, attempt, null) is not null)
        {
            attempt.Dispose();
            return null;
        }
        try
        {
            CompositionProgress.Reset();
            _activeRunIsBuild = build;
            ActiveRunShowsNumberSelector = context.ShowsNumberSelector;
            ActiveRunIc = context.Ic;
            ActiveRunNumber = context.Number;
            ActiveRunMode = context.Mode;
            ActiveRunDeviceContextRefreshSummary = context.DeviceContextRefreshSummary;
            context.Owner.ActiveAttemptId = attempt.Id;
            NotifyActiveRunContextChanged();
            OnPropertyChanged(nameof(RunProgressAccessibleLabel));
            _stateBindings.RefreshCommandState();
            return attempt;
        }
        catch
        {
            CompleteRun(attempt);
            throw;
        }
    }

    private void CompleteRun(RunAttempt attempt)
    {
        try
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _activeAttempt, null, attempt), attempt))
            {
                attempt.Context.Owner.ActiveAttemptId = null;
                ActiveRunShowsNumberSelector = false;
                ActiveRunIc = string.Empty;
                ActiveRunNumber = string.Empty;
                ActiveRunMode = string.Empty;
                ActiveRunDeviceContextRefreshSummary = string.Empty;
                _stateBindings.RefreshCommandState();
                NotifyActiveRunContextChanged();
            }
        }
        finally
        {
            attempt.Dispose();
        }
    }

    private void NotifyActiveRunContextChanged()
    {
        OnPropertyChanged(nameof(DisplayedDeviceIc));
        OnPropertyChanged(nameof(DisplayedDeviceNumber));
        OnPropertyChanged(nameof(DisplayedDeviceContextRefreshSummary));
        OnPropertyChanged(nameof(ActiveRunIc));
        OnPropertyChanged(nameof(ActiveRunNumber));
        OnPropertyChanged(nameof(ActiveRunMode));
        OnPropertyChanged(nameof(ActiveRunContextLabel));
        _stateBindings.NotifyShellRunStateChanged();
    }

    internal async Task<UiRunResultViewModel?> RunCompositionAsync(
        CompositionRunContext context,
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        RunAttempt? attempt = BeginRun(context, build);
        if (attempt is null)
        {
            return null;
        }
        CancellationTokenSource cancellationSource = attempt.Cancellation;
        CancellationTokenSource? progressObservationSource = null;
        CompositionRunProgressFeed? progress = null;
        Task progressObservation = Task.CompletedTask;
        CompositionRunResult? completedResult = null;
        try
        {
            progress = new CompositionRunProgressFeed();
            progressObservationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token);
            progressObservation = ObserveRunProgressAsync(progress, progressObservationSource.Token);
            await Task.Yield();
            completedResult = await Task.Run(
                () => run(progress, cancellationSource.Token).AsTask(), cancellationSource.Token);
            await (progress.IsAttached ? progressObservation : Task.CompletedTask);
            await ProjectAndApplyRunResultAsync(context, completedResult, build, cancellationSource.Token);
        }
        catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })
        {
            if (!TryGetCommittedBuildOutput(completedResult, build, out string? committedOutputId))
            {
                return null;
            }

            PublishCommittedResultWithoutReport(
                context,
                completedResult,
                committedOutputId,
                _stateBindings.Text().CommittedOutputCancelledReportFailure);
        }
        catch (CompositionPreRunRefusalException exception)
        {
            string action = build ? "Build" : "Preview";
            context.Owner.Publish(new UiRunResultViewModel(
                $"{action} blocked",
                exception.Message,
                "No output",
                succeeded: false), context);
            OnPropertyChanged(nameof(LastRunResult));
        }
        catch (Exception exception) when (
            TryGetCommittedBuildOutput(completedResult, build, out string? committedOutputId) &&
            (exception is IOException or UnauthorizedAccessException ||
                ReportPresentationViewModel.IsReportMaterializationException(exception)))
        {
            PublishCommittedResultWithoutReport(context, completedResult, committedOutputId, exception.Message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            string action = build ? "Build" : "Preview";
            context.Owner.Publish(new UiRunResultViewModel(
                $"{action} failed",
                exception.Message,
                "No output",
                succeeded: false), context);
            OnPropertyChanged(nameof(LastRunResult));
            loadErrorReport(action, exception.Message);
            if (build)
            {
                _stateBindings.Reports().ShowReport();
            }
        }
        finally
        {
            try
            {
                if (progressObservationSource is not null)
                {
                    if (progress is not { IsAttached: true })
                    {
                        progressObservationSource.Cancel();
                    }

                    try
                    {
                        await progressObservation;
                    }
                    catch (OperationCanceledException) when (progressObservationSource.IsCancellationRequested)
                    {
                        // Cancellation or a planning-only result has no remaining typed phases to project.
                    }
                }
            }
            finally
            {
                progressObservationSource?.Dispose();
                CompleteRun(attempt);
            }
        }
        return context.Owner.LastRunResult;
    }

    /// <summary>True only after a successful Build has committed its output BIN.</summary>
    private static bool TryGetCommittedBuildOutput(
        [NotNullWhen(true)] CompositionRunResult? result,
        bool build,
        [NotNullWhen(true)] out string? committedOutputId)
    {
        if (build &&
            result is { Succeeded: true, Report.Output.Committed: true, CommittedOutputId: { } outputId } &&
            !string.IsNullOrWhiteSpace(outputId))
        {
            committedOutputId = outputId;
            return true;
        }

        committedOutputId = null;
        return false;
    }

    /// <summary>Keeps the committed output receipt visible when its report cannot be delivered.</summary>
    private void PublishCommittedResultWithoutReport(
        CompositionRunContext context,
        CompositionRunResult result,
        string committedOutputId,
        string reportFailure)
    {
        ShellTextResources text = _stateBindings.Text();
        context.Owner.Publish(new UiRunResultViewModel(
            text.CommittedOutputReportUnavailableTitle,
            text.FormatCommittedOutputReportUnavailableDetail(result.OutputSize, result.OutputSha256, reportFailure),
            committedOutputId,
            succeeded: false), context);
        OnPropertyChanged(nameof(LastRunResult));
        CompositionProgress.MarkReportUnavailable();
    }

    internal async Task ShowDiagnosticPreviewAsync(CompositionRunContext context, CompositionRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        GeneralReplaceDiagnosticPreviewSummary diagnostic = report.DiagnosticPreview ??
            throw new ArgumentException(
                "A plan-only Preview requires its typed diagnostic marker.",
                nameof(report));
        RunAttempt? attempt = BeginRun(context, build: false);
        if (attempt is null)
        {
            return;
        }
        CancellationTokenSource cancellationSource = attempt.Cancellation;
        try
        {
            await Task.Yield();
            ReportPresentationViewModel reports = _stateBindings.Reports();
            long projectionGeneration = reports.BeginReportProjection();
            Task<string> reportJsonTask = Task.Run(
                () => CompositionRunReportJson.SerializeDiagnosticPreview(report),
                cancellationSource.Token);
            Task<ReportReviewViewModel> projectionTask = reports.ProjectReportAsync(
                report,
                suppressOutput: true,
                "preview report",
                null,
                cancellationSource.Token,
                materializationErrorsAsReport: false,
                inspectionSnapshot: null);
            await Task.WhenAll(reportJsonTask, projectionTask);
            string reportJson = await reportJsonTask;
            ReportReviewViewModel projected = await projectionTask;
            cancellationSource.Token.ThrowIfCancellationRequested();
            context.Owner.Publish(new UiRunResultViewModel(
                "Preview blocked",
                diagnostic.Message,
                "No output",
                succeeded: false), context);
            OnPropertyChanged(nameof(LastRunResult));
            if (reports.IsCurrentReportProjection(projectionGeneration))
            {
                reports.PublishGeneratedReport(
                    projected,
                    reportJson,
                    "Preview",
                    show: false);
                CompositionProgress.MarkReportReady(true);
            }
        }
        finally
        {
            CompleteRun(attempt);
        }
    }

    internal void ShowActionReadiness(
        CompositionRunContext context,
        CapabilityActionReadinessSnapshot readiness,
        bool build)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        if (IsRunInProgress) { return; }
        string action = build ? "Build" : "Preview";
        CapabilityActionBlocker? blocker = build
            ? readiness.Build.PrimaryBlocker
            : readiness.Preview.PrimaryBlocker;
        context.Owner.Publish(new UiRunResultViewModel(
            $"{action} blocked",
            blocker?.Message ?? $"{action} is unavailable.",
            "No output",
            succeeded: false), context);
        OnPropertyChanged(nameof(LastRunResult));
    }

    /// <summary>Projects one completed run off-dispatcher and publishes it only while its generation is current.</summary>
    internal async Task ProjectAndApplyRunResultAsync(
        CompositionRunContext context,
        CompositionRunResult result,
        bool build,
        CancellationToken cancellationToken)
    {
        ReportPresentationViewModel reports = _stateBindings.Reports();
        long reportProjectionGeneration = reports.BeginReportProjection();
        string action = build ? "Build" : "Preview";
        Task<string> reportJsonTask = Task.Run(
            () => CompositionRunReportJson.Serialize(result),
            cancellationToken);
        Task<ReportReviewViewModel> projectionTask = reports.ProjectReportAsync(
            result.Report,
            suppressOutput: false,
            $"{action.ToLowerInvariant()} report",
            result.CommittedOutputId,
            cancellationToken,
            materializationErrorsAsReport: false,
            inspectionSnapshot: result.InspectionSnapshot);
        await Task.WhenAll(reportJsonTask, projectionTask);
        string reportJson = await reportJsonTask;
        ReportReviewViewModel report = await projectionTask;
        cancellationToken.ThrowIfCancellationRequested();

        ApplyRunResult(
            context,
            result,
            build,
            report,
            reportJson,
            publishReport: reports.IsCurrentReportProjection(reportProjectionGeneration));
        CompositionProgress.MarkReportReady(reports.IsCurrentReportProjection(reportProjectionGeneration));
    }

    private void ApplyRunResult(
        CompositionRunContext context,
        CompositionRunResult result,
        bool build,
        ReportReviewViewModel report,
        string reportJson,
        bool publishReport)
    {
        string action = build ? "Build" : "Preview";
        bool deliveryComplete = result.Succeeded && result.IsDeliveryComplete;
        string detail = !result.IsDeliveryComplete && !string.IsNullOrWhiteSpace(result.DeliveryFailureMessage)
            ? result.DeliveryFailureMessage
            : result.Succeeded
            ? $"{result.ProfileId} / {result.OutputSize} bytes / {_stateBindings.Text().RunResultReportReadyLabel}"
            : report.Issues.Count == 0 ? result.OutcomeStatus : report.Issues[0].Detail;
        context.Owner.Publish(new UiRunResultViewModel(
            result.Succeeded
                ? deliveryComplete ? $"{action} succeeded" : $"{action} partially delivered"
                : $"{action} blocked",
            detail,
            result.Succeeded ? result.CommittedOutputId ?? result.OutputFileName : "No output",
            deliveryComplete), context);
        OnPropertyChanged(nameof(LastRunResult));
        if (publishReport)
        {
            _stateBindings.Reports().PublishGeneratedReport(
                report,
                reportJson,
                action,
                show: build && (!deliveryComplete || string.IsNullOrWhiteSpace(result.CommittedOutputId)));
        }

        // A report-publication failure reaches the committed-output fallback before any success modal opens.
        _ = _stateBindings.TryShowBuildCompleted(result, build);
    }

    private async Task ObserveRunProgressAsync(
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        await foreach (CompositionRunProgressSnapshot snapshot in progress.ReadAllAsync(cancellationToken))
        {
            _ = CompositionProgress.TryApply(snapshot);
        }
    }

    private void CompositionProgress_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompositionRunProgressViewModel.HasTypedProgress))
        {
            OnPropertyChanged(nameof(HasTypedRunProgress));
        }

        if (e.PropertyName == nameof(CompositionRunProgressViewModel.AccessibleStatus))
        {
            OnPropertyChanged(nameof(RunProgressStatusLabel));
        }

        if (e.PropertyName == nameof(CompositionRunProgressViewModel.CurrentStepLabel))
        {
            OnPropertyChanged(nameof(RunProgressDisplayLabel));
        }

        if (e.PropertyName == nameof(CompositionRunProgressViewModel.ShouldAnimateActiveStep))
        {
            OnPropertyChanged(nameof(ShouldAnimateRunProgress));
        }
    }

    internal void ResetRunResultForContextChange(CompositionRunContext context)
    {
        context.Owner.Publish(new UiRunResultViewModel(
            "Context changed",
            $"{context.Ic} / {context.Number}: run Build to validate the latest context.",
            "No output",
            succeeded: false));
        OnPropertyChanged(nameof(LastRunResult));
    }

    internal void PublishRunResult(WorkflowRunState owner, UiRunResultViewModel result)
    {
        owner.Publish(result);
        OnPropertyChanged(nameof(LastRunResult));
    }

    internal void ApplyLanguageChanged(ShellLanguage language)
    {
        CompositionProgress.ApplyLanguage(language);
        foreach (WorkflowRunState owner in _stateBindings.Owners())
        {
            owner.ApplyLanguage(_stateBindings.Text());
        }

        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(RunProgressAccessibleLabel));
        OnPropertyChanged(nameof(RunProgressStatusLabel));
        OnPropertyChanged(nameof(RunProgressDisplayLabel));
    }

    internal void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(DisplayedDeviceIc));
        OnPropertyChanged(nameof(DisplayedDeviceNumber));
        OnPropertyChanged(nameof(DisplayedDeviceContextRefreshSummary));
    }

    internal void NotifyReducedMotionChanged()
    {
        OnPropertyChanged(nameof(ShouldAnimateRunProgress));
    }

    internal void NotifyCommandStateChanged()
    {
        OnPropertyChanged(nameof(IsRunInProgress));
        OnPropertyChanged(nameof(HasTypedRunProgress));
        OnPropertyChanged(nameof(RunProgressStatusLabel));
        OnPropertyChanged(nameof(RunProgressDisplayLabel));
        OnPropertyChanged(nameof(ShouldAnimateRunProgress));
    }
}
