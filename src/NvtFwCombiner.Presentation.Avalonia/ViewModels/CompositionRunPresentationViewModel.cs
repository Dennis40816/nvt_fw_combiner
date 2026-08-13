using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns one active Preview/Build lifetime and its immutable context projection.</summary>
internal sealed class CompositionRunPresentationViewModel : ObservableObject
{
    private readonly CompositionRunStateBindings _stateBindings;
    private CancellationTokenSource? _activeRunCancellationSource;
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

    public UiRunResultViewModel LastRunResult { get; private set; } = new(
        "No run yet",
        "Drop required BIN files, then run Build.",
        "No output",
        succeeded: true);

    /// <summary>True while one composition Preview or Build owns the external processing lifetime.</summary>
    public bool IsRunInProgress => _activeRunCancellationSource is not null;

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
        _activeRunCancellationSource?.Cancel();
    }

    private CancellationTokenSource BeginRun(bool build)
    {
        if (_activeRunCancellationSource is not null)
        {
            throw new InvalidOperationException("Another Preview or Build operation is already running.");
        }

        var cancellationSource = new CancellationTokenSource();
        CompositionProgress.Reset();
        _activeRunIsBuild = build;
        ActiveRunShowsNumberSelector = _stateBindings.ShouldShowNumberSelector();
        ActiveRunIc = _stateBindings.SelectedIc();
        ActiveRunNumber = _stateBindings.SelectedNumber();
        ActiveRunMode = _stateBindings.SelectedMode();
        ActiveRunDeviceContextRefreshSummary = _stateBindings.DeviceContextRefreshSummary();
        _activeRunCancellationSource = cancellationSource;
        NotifyActiveRunContextChanged();
        OnPropertyChanged(nameof(RunProgressAccessibleLabel));
        _stateBindings.RefreshCommandState();
        return cancellationSource;
    }

    private void CompleteRun(CancellationTokenSource cancellationSource)
    {
        if (ReferenceEquals(_activeRunCancellationSource, cancellationSource))
        {
            _activeRunCancellationSource = null;
            _stateBindings.RefreshCommandState();
            ActiveRunShowsNumberSelector = false;
            ActiveRunIc = string.Empty;
            ActiveRunNumber = string.Empty;
            ActiveRunMode = string.Empty;
            ActiveRunDeviceContextRefreshSummary = string.Empty;
            NotifyActiveRunContextChanged();
        }

        cancellationSource.Dispose();
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

    internal async Task RunCompositionAsync(
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        CancellationTokenSource? cancellationSource = null;
        CancellationTokenSource? progressObservationSource = null;
        CompositionRunProgressFeed? progress = null;
        Task progressObservation = Task.CompletedTask;
        try
        {
            cancellationSource = BeginRun(build);
            progress = new CompositionRunProgressFeed();
            progressObservationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token);
            progressObservation = ObserveRunProgressAsync(progress, progressObservationSource.Token);
            await Task.Yield();
            CompositionRunResult result = await Task.Run(
                () => run(progress, cancellationSource.Token).AsTask(), cancellationSource.Token);
            await (progress.IsAttached ? progressObservation : Task.CompletedTask);
            await ProjectAndApplyRunResultAsync(result, build, cancellationSource.Token);
        }
        catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })
        {
            return;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            string action = build ? "Build" : "Preview";
            LastRunResult = new UiRunResultViewModel(
                $"{action} failed",
                exception.Message,
                "No output",
                succeeded: false);
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
                if (cancellationSource is not null)
                {
                    CompleteRun(cancellationSource);
                }
            }
        }
    }

    internal async Task ShowDiagnosticPreviewAsync(CompositionRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        GeneralReplaceDiagnosticPreviewSummary diagnostic = report.DiagnosticPreview ??
            throw new ArgumentException(
                "A plan-only Preview requires its typed diagnostic marker.",
                nameof(report));
        CancellationTokenSource cancellationSource = BeginRun(build: false);
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
            LastRunResult = new UiRunResultViewModel(
                "Preview blocked",
                diagnostic.Message,
                "No output",
                succeeded: false);
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
            CompleteRun(cancellationSource);
        }
    }

    internal void ShowActionReadiness(
        CapabilityActionReadinessSnapshot readiness,
        bool build)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        string action = build ? "Build" : "Preview";
        CapabilityActionBlocker? blocker = build
            ? readiness.Build.PrimaryBlocker
            : readiness.Preview.PrimaryBlocker;
        LastRunResult = new UiRunResultViewModel(
            $"{action} blocked",
            blocker?.Message ?? $"{action} is unavailable.",
            "No output",
            succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
    }

    /// <summary>Projects one completed run off-dispatcher and publishes it only while its generation is current.</summary>
    internal async Task ProjectAndApplyRunResultAsync(
        CompositionRunResult result,
        bool build,
        CancellationToken cancellationToken)
    {
        ReportPresentationViewModel reports = _stateBindings.Reports();
        if (!result.HasRunReport)
        {
            ApplyReadinessOnlyResult(result, build);
            return;
        }

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
            result,
            build,
            report,
            reportJson,
            publishReport: reports.IsCurrentReportProjection(reportProjectionGeneration));
        CompositionProgress.MarkReportReady(reports.IsCurrentReportProjection(reportProjectionGeneration));
    }

    private void ApplyRunResult(
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
        LastRunResult = new UiRunResultViewModel(
            result.Succeeded
                ? deliveryComplete ? $"{action} succeeded" : $"{action} partially delivered"
                : $"{action} blocked",
            detail,
            result.Succeeded ? result.CommittedOutputId ?? result.OutputFileName : "No output",
            deliveryComplete);
        OnPropertyChanged(nameof(LastRunResult));
        _ = _stateBindings.TryShowBuildCompleted(result, build);

        if (!publishReport)
        {
            return;
        }

        _stateBindings.Reports().PublishGeneratedReport(
            report,
            reportJson,
            action,
            show: build && (!deliveryComplete || string.IsNullOrWhiteSpace(result.CommittedOutputId)));
    }

    private void ApplyReadinessOnlyResult(CompositionRunResult result, bool build)
    {
        string action = build ? "Build" : "Preview";
        CapabilityActionBlocker? blocker = build
            ? result.ActionReadiness?.Build.PrimaryBlocker
            : result.ActionReadiness?.Preview.PrimaryBlocker;
        LastRunResult = new UiRunResultViewModel(
            $"{action} blocked",
            blocker?.Message ?? result.OutcomeStatus,
            "No output",
            succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
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

    internal void ResetRunResultForContextChange()
    {
        LastRunResult = new UiRunResultViewModel(
            "Context changed",
            $"{_stateBindings.SelectedIc()} / {_stateBindings.SelectedNumber()}: run Build to validate the latest context.",
            "No output",
            succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
    }

    internal void PublishRunResult(UiRunResultViewModel result)
    {
        LastRunResult = result;
        OnPropertyChanged(nameof(LastRunResult));
    }

    internal void ApplyLanguageChanged(ShellLanguage language)
    {
        CompositionProgress.ApplyLanguage(language);
        if (string.Equals(LastRunResult.Title, "No run yet", StringComparison.Ordinal) ||
            string.Equals(LastRunResult.Title, "尚未執行", StringComparison.Ordinal))
        {
            ShellTextResources text = _stateBindings.Text();
            LastRunResult = new UiRunResultViewModel(
                text.InitialRunTitle,
                text.InitialRunDetail,
                text.NoOutputLabel,
                succeeded: true);
        }

        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(RunProgressAccessibleLabel));
        OnPropertyChanged(nameof(RunProgressStatusLabel));
        OnPropertyChanged(nameof(RunProgressDisplayLabel));
    }

    internal void NotifyContextChanged()
    {
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
