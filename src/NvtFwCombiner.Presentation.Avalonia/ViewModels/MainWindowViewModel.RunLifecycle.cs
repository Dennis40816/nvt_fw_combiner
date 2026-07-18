using System.ComponentModel;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private CancellationTokenSource? _activeRunCancellationSource;
    private bool _activeRunIsBuild;
    private bool ActiveRunShowsNumberSelector { get; set; }
    private string ActiveRunDeviceContextRefreshSummary { get; set; } = string.Empty;

    /// <summary>Gets the IC captured for the active run.</summary>
    public string ActiveRunIc { get; private set; } = string.Empty;

    /// <summary>Gets the IC Number token captured for the active run.</summary>
    public string ActiveRunNumber { get; private set; } = string.Empty;

    /// <summary>Gets the workflow mode captured for the active run.</summary>
    public string ActiveRunMode { get; private set; } = string.Empty;

    /// <summary>Gets the IC identity that the device-context surface must display.</summary>
    public string DisplayedDeviceIc => IsRunInProgress ? ActiveRunIc : SelectedIc;

    /// <summary>Gets the Number identity that the device-context surface must display.</summary>
    public string DisplayedDeviceNumber => IsRunInProgress ? ActiveRunNumber : SelectedNumber;

    private string DisplayedDeviceContextRefreshSummary => IsRunInProgress
        ? ActiveRunDeviceContextRefreshSummary
        : DeviceContextRefreshSummary;

    /// <summary>Gets the immutable active-run identity shown beside the phase stepper.</summary>
    public string ActiveRunContextLabel => ActiveRunShowsNumberSelector
        ? $"{ActiveRunMode} · {ActiveRunIc} / {ActiveRunNumber}"
        : $"{ActiveRunMode} · {ActiveRunIc}";

    /// <summary>Gets the localized projection of Application-owned composition phases.</summary>
    public CompositionRunProgressViewModel CompositionProgress { get; }

    /// <summary>True while one composition Preview or Build owns the external processing lifetime.</summary>
    public bool IsRunInProgress => _activeRunCancellationSource is not null;

    /// <summary>Gets the localized screen-reader label for the active composition action.</summary>
    public string RunProgressAccessibleLabel => _activeRunIsBuild
        ? Text.BuildRunProgressAccessibleLabel
        : Text.PreviewRunProgressAccessibleLabel;

    /// <summary>Gets the current typed phase status, or the action-level fallback before Application starts.</summary>
    public string RunProgressStatusLabel => CompositionProgress.HasTypedProgress
        ? CompositionProgress.AccessibleStatus
        : RunProgressAccessibleLabel;

    /// <summary>Gets the concise current phase label shown beside the separate lifecycle ordinal.</summary>
    public string RunProgressDisplayLabel => CompositionProgress.HasTypedProgress
        ? CompositionProgress.CurrentStepLabel
        : RunProgressAccessibleLabel;

    /// <summary>True while an active run has supplied its Application-owned phase sequence.</summary>
    public bool HasTypedRunProgress => IsRunInProgress && CompositionProgress.HasTypedProgress;

    /// <summary>True when the active progress surface may use restrained indeterminate motion.</summary>
    public bool ShouldAnimateRunProgress => IsRunInProgress &&
        !IsReducedMotionEnabled &&
        (!CompositionProgress.HasTypedProgress || CompositionProgress.ShouldAnimateActiveStep);

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
        ActiveRunShowsNumberSelector = ShouldShowNumberSelectorForSelectedPage();
        ActiveRunIc = SelectedIc;
        ActiveRunNumber = SelectedNumber;
        ActiveRunMode = IsMergeVisible ? SelectedMergeMode : SelectedReplaceMode;
        ActiveRunDeviceContextRefreshSummary = DeviceContextRefreshSummary;
        _activeRunCancellationSource = cancellationSource;
        NotifyActiveRunContextChanged();
        OnPropertyChanged(nameof(RunProgressAccessibleLabel));
        RefreshCommandState();
        return cancellationSource;
    }

    private void CompleteRun(CancellationTokenSource cancellationSource)
    {
        if (ReferenceEquals(_activeRunCancellationSource, cancellationSource))
        {
            _activeRunCancellationSource = null;
            RefreshCommandState();
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
        OnPropertyChanged(nameof(IsDeviceContextSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextNumberSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextFamilyBadgeVisible));
        OnPropertyChanged(nameof(DisplayedDeviceIc));
        OnPropertyChanged(nameof(DisplayedDeviceNumber));
        OnPropertyChanged(nameof(ActiveRunIc));
        OnPropertyChanged(nameof(ActiveRunNumber));
        OnPropertyChanged(nameof(ActiveRunMode));
        OnPropertyChanged(nameof(ActiveRunContextLabel));
        OnPropertyChanged(nameof(DeviceContextStatus));
    }

    internal async Task RunCompositionAsync(
        bool build,
        Func<CompositionRunProgressFeed, CancellationToken, ValueTask<WorkbenchRunResult>> run,
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
            WorkbenchRunResult result = await Task.Run(
                () => run(progress, cancellationSource.Token).AsTask(),
                cancellationSource.Token);
            ApplyRunResult(result, build);
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
}
