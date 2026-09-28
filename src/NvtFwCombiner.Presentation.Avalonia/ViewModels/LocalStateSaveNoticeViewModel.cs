using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Local state whose best-effort save is observed independently.</summary>
internal enum LocalStateSaveTarget
{
    ReportHistory,

    Preferences,
}

/// <summary>
/// Presentation owner of the persistent status-bar notice for failed local-state saves. A target stays
/// failed until a later save of that target succeeds; Retry asks each failed target's persistence owner
/// to save its latest snapshot again. The notice never blocks other work. The window that owns the
/// persistence coordinators owns this notice for its lifetime; members run on the UI thread.
/// </summary>
internal sealed class LocalStateSaveNoticeViewModel : ObservableObject
{
    // Win32 HRESULTs the atomic local-file writer surfaces through IOException.
    private const int ErrorSharingViolation = unchecked((int)0x80070020);
    private const int ErrorLockViolation = unchecked((int)0x80070021);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private static readonly string[] PublishedPropertyNames =
    [
        nameof(IsVisible), nameof(Title), nameof(Detail), nameof(DetailToolTip), nameof(AccessibleStatus),
        nameof(RetryLabel),
    ];
    private readonly Func<ShellTextResources> _text;
    private readonly Dictionary<LocalStateSaveTarget, Func<bool>> _retries = [];
    private readonly Dictionary<LocalStateSaveTarget, Exception> _failures = [];
    private LocalStateSaveTarget _latestFailure;
    private bool _isDetached;

    internal LocalStateSaveNoticeViewModel(Func<ShellTextResources> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        RetryCommand = new RelayCommand(Retry, () => IsVisible && !_isDetached);
    }

    public IRelayCommand RetryCommand { get; }

    public bool IsVisible => _failures.Count > 0;

    public string Title => IsVisible ? _text().LocalStateSaveFailedTitle : string.Empty;

    /// <summary>One sentence whose reason follows the most recent unresolved failure.</summary>
    public string Detail => LatestFailure is { } failure
        ? string.Format(
            CultureInfo.CurrentCulture,
            _text().LocalStateSaveFailedDetailFormat,
            DescribeReason(_text(), failure))
        : string.Empty;

    /// <summary>Names each unsaved state with the diagnostic its save reported.</summary>
    public string DetailToolTip
    {
        get
        {
            ShellTextResources text = _text();
            return string.Join(
                Environment.NewLine,
                _failures.OrderBy(static pair => pair.Key).Select(pair =>
                    $"{TargetLabel(text, pair.Key)}: {Diagnostic(pair.Value)}"));
        }
    }

    public string AccessibleStatus => IsVisible ? $"{Title} — {Detail}" : string.Empty;

    /// <summary>The icon-only Retry button's tooltip and accessible name.</summary>
    public string RetryLabel => _text().RetryLabel;

    private Exception? LatestFailure => _failures.TryGetValue(_latestFailure, out Exception? latest)
        ? latest
        : _failures.Values.FirstOrDefault();

    /// <summary>Connects one target to the persistence owner that re-queues its latest snapshot.</summary>
    internal void Attach(LocalStateSaveTarget target, Func<bool> retry)
    {
        ArgumentNullException.ThrowIfNull(retry);
        if (!_isDetached)
        {
            _retries[target] = retry;
        }
    }

    /// <summary>Applies one completed save: <see langword="null"/> succeeded, otherwise the failure.</summary>
    internal void ObserveSave(LocalStateSaveTarget target, Exception? failure)
    {
        if (_isDetached)
        {
            return;
        }

        if (failure is null)
        {
            if (!_failures.Remove(target))
            {
                return;
            }
        }
        else
        {
            _failures[target] = failure;
            _latestFailure = target;
        }

        Publish();
    }

    internal void ApplyLanguageChanged()
    {
        if (!_isDetached)
        {
            Publish();
        }
    }

    /// <summary>Stops presenting later save outcomes and releases the retry owners.</summary>
    internal void Detach()
    {
        if (_isDetached)
        {
            return;
        }

        _isDetached = true;
        _retries.Clear();
        PresentationObserver.Invoke(RetryCommand.NotifyCanExecuteChanged);
    }

    private static string DescribeReason(ShellTextResources text, Exception failure)
    {
        return failure switch
        {
            UnauthorizedAccessException => text.LocalStateSaveAccessDeniedReason,
            ReportHistoryPersistenceException { Failure: ReportHistoryPersistenceFailure.EntryTooLargeToPersist } =>
                text.LocalStateSaveTooLargeReason,
            IOException { HResult: ErrorDiskFull or ErrorHandleDiskFull } => text.LocalStateSaveStorageFullReason,
            IOException { HResult: ErrorSharingViolation or ErrorLockViolation } => text.LocalStateSaveFileInUseReason,
            _ => text.LocalStateSaveUnexpectedReason,
        };
    }

    private static string TargetLabel(ShellTextResources text, LocalStateSaveTarget target)
    {
        return target == LocalStateSaveTarget.ReportHistory
            ? text.LocalStateReportHistoryLabel
            : text.LocalStatePreferencesLabel;
    }

    private static string Diagnostic(Exception failure)
    {
        return string.IsNullOrWhiteSpace(failure.Message) ? failure.GetType().Name : failure.Message;
    }

    private void Retry()
    {
        if (_isDetached)
        {
            return;
        }

        foreach (LocalStateSaveTarget target in _failures.Keys.ToArray())
        {
            if (_retries.TryGetValue(target, out Func<bool>? retry))
            {
                // The outcome returns through ObserveSave; the notice stays until a save succeeds.
                _ = retry();
            }
        }
    }

    private void Publish()
    {
        foreach (string propertyName in PublishedPropertyNames)
        {
            PresentationObserver.Invoke(() => OnPropertyChanged(propertyName));
        }

        PresentationObserver.Invoke(RetryCommand.NotifyCanExecuteChanged);
    }
}
