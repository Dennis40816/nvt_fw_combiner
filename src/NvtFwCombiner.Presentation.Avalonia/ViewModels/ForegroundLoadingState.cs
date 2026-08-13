using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Presentation-owned state for one foreground operation without inventing progress that its owner did not report.
/// </summary>
internal sealed class ForegroundLoadingState : ObservableObject
{
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _retryLabel = string.Empty;
    private string _cancelLabel = string.Empty;
    private double? _progress;
    private bool _isVisible;
    private bool _isRunning;
    private bool _hasFailed;
    private bool _isReducedMotionEnabled;

    public bool IsVisible => _isVisible;

    public bool IsRunning => _isRunning;

    public bool HasFailed => _hasFailed;

    public string Title => _title;

    public string Detail => _detail;

    public string RetryLabel => _retryLabel;

    /// <summary>Localized cancellation action label, or empty when cancellation is unavailable.</summary>
    public string CancelLabel => _cancelLabel;

    /// <summary>Reported determinate progress in the inclusive range 0..1, or null when no progress contract exists.</summary>
    public double? Progress => _progress;

    /// <summary>True when the operation owner supplied determinate progress.</summary>
    public bool HasDeterminateProgress => Progress.HasValue;

    public string ProgressPercentLabel => Progress is { } progress
        ? string.Create(CultureInfo.CurrentCulture, $"{progress * 100:0}%")
        : string.Empty;

    public bool IsIndeterminate => IsRunning;

    public bool ShouldAnimate => IsIndeterminate && !IsReducedMotionEnabled;

    public bool IsReducedMotionEnabled => _isReducedMotionEnabled;

    /// <summary>True only for a failed operation with an explicit retry action.</summary>
    public bool CanRetry => HasFailed && !string.IsNullOrWhiteSpace(RetryLabel);

    /// <summary>True while the visible operation offers an explicit cancellation action.</summary>
    public bool CanCancel => IsVisible && !string.IsNullOrWhiteSpace(CancelLabel);

    /// <summary>Localized live-region text that remains available without motion or color.</summary>
    public string AccessibleStatus
    {
        get
        {
            string heading = HasDeterminateProgress
                ? $"{Title} {ProgressPercentLabel}"
                : Title;
            return string.IsNullOrWhiteSpace(Detail)
                ? heading
                : $"{heading} — {Detail}";
        }
    }

    /// <summary>Shows a new foreground operation using determinate progress only when supplied by its owner.</summary>
    public void Begin(string title, string detail, double? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ValidateProgress(progress);

        bool accessibleStatusChanged = SetTitle(title) |
            SetDetail(detail) |
            SetProgress(progress);
        SetRetryLabel(string.Empty);
        _ = SetProperty(ref _cancelLabel, string.Empty, nameof(CancelLabel));
        SetFailed(false);
        SetRunning(true);
        SetVisible(true);
        OnPropertyChanged(nameof(CanCancel));
        NotifyAccessibleStatus(accessibleStatusChanged);
    }

    public void ReportProgress(double progress, string? detail = null, bool announce = true)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Determinate progress requires an active foreground operation.");
        }

        ValidateProgress(progress);
        bool accessibleStatusChanged = false;
        if (detail is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(detail);
            accessibleStatusChanged = SetDetail(detail);
        }

        accessibleStatusChanged |= SetProgress(progress);
        NotifyAccessibleStatus(announce && accessibleStatusChanged);
    }

    public void Fail(string title, string detail, string retryLabel = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        bool accessibleStatusChanged = SetTitle(title) |
            SetDetail(detail) |
            SetProgress(null);
        SetRetryLabel(retryLabel ?? string.Empty);
        _ = SetProperty(ref _cancelLabel, string.Empty, nameof(CancelLabel));
        SetRunning(false);
        SetFailed(true);
        SetVisible(true);
        OnPropertyChanged(nameof(CanCancel));
        NotifyAccessibleStatus(accessibleStatusChanged);
    }

    /// <summary>Hides the foreground surface after the owning operation completes.</summary>
    public void Complete()
    {
        SetRunning(false);
        SetFailed(false);
        SetRetryLabel(string.Empty);
        _ = SetProperty(ref _cancelLabel, string.Empty, nameof(CancelLabel));
        _ = SetProgress(null);
        SetVisible(false);
        OnPropertyChanged(nameof(CanCancel));
    }

    /// <summary>Exposes a localized cancellation action for the current visible operation.</summary>
    public void SetCancellationAction(string label)
    {
        _ = SetProperty(ref _cancelLabel, label ?? string.Empty, nameof(CancelLabel));
        OnPropertyChanged(nameof(CanCancel));
    }

    /// <summary>Updates whether non-essential activity motion is allowed.</summary>
    public void SetReducedMotion(bool isEnabled)
    {
        if (SetProperty(ref _isReducedMotionEnabled, isEnabled, nameof(IsReducedMotionEnabled)))
        {
            OnPropertyChanged(nameof(ShouldAnimate));
        }
    }

    private static void ValidateProgress(double? progress)
    {
        if (progress is < 0 or > 1 || double.IsNaN(progress ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Progress must be between 0 and 1.");
        }
    }

    private bool SetTitle(string value)
    {
        return SetProperty(ref _title, value, nameof(Title));
    }

    private bool SetDetail(string value)
    {
        return SetProperty(ref _detail, value, nameof(Detail));
    }

    private void SetRetryLabel(string value)
    {
        if (SetProperty(ref _retryLabel, value, nameof(RetryLabel)))
        {
            OnPropertyChanged(nameof(CanRetry));
        }
    }

    private bool SetProgress(double? value)
    {
        bool changed = SetProperty(ref _progress, value, nameof(Progress));
        if (changed)
        {
            OnPropertyChanged(nameof(HasDeterminateProgress));
            OnPropertyChanged(nameof(ProgressPercentLabel));
        }

        return changed;
    }

    private void SetVisible(bool value)
    {
        _ = SetProperty(ref _isVisible, value, nameof(IsVisible));
    }

    private void SetRunning(bool value)
    {
        if (SetProperty(ref _isRunning, value, nameof(IsRunning)))
        {
            OnPropertyChanged(nameof(IsIndeterminate));
            OnPropertyChanged(nameof(ShouldAnimate));
        }
    }

    private void SetFailed(bool value)
    {
        if (SetProperty(ref _hasFailed, value, nameof(HasFailed)))
        {
            OnPropertyChanged(nameof(CanRetry));
        }
    }

    private void NotifyAccessibleStatus(bool changed)
    {
        if (changed)
        {
            OnPropertyChanged(nameof(AccessibleStatus));
        }
    }
}
