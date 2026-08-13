using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Presentation-owned state for one foreground operation without inventing progress that its owner did not report.
/// </summary>
internal sealed class ForegroundLoadingState : ObservableObject
{
    internal ForegroundLoadingState(Func<Task>? retry = null, Func<Task>? cancel = null)
    {
        RetryCommand = retry is null ? null : new AsyncRelayCommand(retry);
        CancelCommand = cancel is null ? null : new AsyncRelayCommand(cancel);
    }

    public IAsyncRelayCommand? RetryCommand { get; }
    public IAsyncRelayCommand? CancelCommand { get; }
    public string Title { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
    public string RetryLabel { get; private set; } = string.Empty;
    public string CancelLabel { get; private set; } = string.Empty;
    public double? Progress { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsRunning { get; private set; }
    public bool HasFailed { get; private set; }
    public bool IsReducedMotionEnabled { get; private set; }
    public bool HasDeterminateProgress => Progress.HasValue;
    public string ProgressPercentLabel => Progress is { } progress
        ? string.Create(CultureInfo.CurrentCulture, $"{progress * 100:0}%")
        : string.Empty;
    public bool IsIndeterminate => IsRunning;
    public bool ShouldAnimate => IsIndeterminate && !IsReducedMotionEnabled;
    public bool CanRetry => HasFailed && !string.IsNullOrWhiteSpace(RetryLabel);
    public bool CanCancel => IsVisible && !string.IsNullOrWhiteSpace(CancelLabel);
    public string AccessibleStatus
    {
        get
        {
            string heading = HasDeterminateProgress ? $"{Title} {ProgressPercentLabel}" : Title;
            return string.IsNullOrWhiteSpace(Detail) ? heading : $"{heading} — {Detail}";
        }
    }

    public void Begin(
        string title,
        string detail,
        double? progress = null,
        string cancelLabel = "")
    {
        Validate(title, detail, progress);
        bool announce = Title != title || Detail != detail || Progress != progress;
        Title = title;
        Detail = detail;
        Progress = progress;
        RetryLabel = string.Empty;
        CancelLabel = cancelLabel ?? string.Empty;
        HasFailed = false;
        IsRunning = true;
        IsVisible = true;
        Publish(announce);
    }

    public void ReportProgress(double progress, string detail, bool announce = true)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Determinate progress requires an active foreground operation.");
        }
        ValidateProgress(progress);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        bool changed = Progress != progress || Detail != detail;
        Progress = progress;
        Detail = detail;
        Publish(announce && changed);
    }

    public void Fail(
        string title,
        string detail,
        string retryLabel = "",
        string cancelLabel = "")
    {
        Validate(title, detail, progress: null);
        bool announce = Title != title || Detail != detail || Progress is not null;
        Title = title;
        Detail = detail;
        Progress = null;
        RetryLabel = retryLabel ?? string.Empty;
        CancelLabel = cancelLabel ?? string.Empty;
        IsRunning = false;
        HasFailed = true;
        IsVisible = true;
        Publish(announce);
    }

    public void Complete()
    {
        IsRunning = false;
        HasFailed = false;
        RetryLabel = string.Empty;
        CancelLabel = string.Empty;
        Progress = null;
        IsVisible = false;
        Publish(announce: false);
    }

    public void SetReducedMotion(bool enabled)
    {
        if (IsReducedMotionEnabled != enabled)
        {
            IsReducedMotionEnabled = enabled;
            Publish(announce: false);
        }
    }

    private static void Validate(string title, string detail, double? progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ValidateProgress(progress);
    }

    private static void ValidateProgress(double? progress)
    {
        if (progress is < 0 or > 1 || double.IsNaN(progress ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Progress must be between 0 and 1.");
        }
    }

    private void Publish(bool announce)
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(string.Empty));
        if (announce)
        {
            PresentationObserver.Invoke(() => OnPropertyChanged(nameof(AccessibleStatus)));
        }
    }
}
