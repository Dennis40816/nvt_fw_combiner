using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MessageCenterViewModel : ObservableObject
{
    private readonly Func<ShellTextResources> _textProvider;
    private readonly ISystemInformationService _systemInformation;
    private readonly ISystemDiagnosticsExporter _exporter;
    private readonly Action<bool> _diagnosticsChanged;
    private Task? _activeRefresh;
    private bool _activeRefreshReloadsCatalog;

    internal MessageCenterViewModel(
        Func<ShellTextResources> textProvider,
        ISystemInformationService systemInformation,
        ISystemDiagnosticsExporter exporter,
        ReportPresentationViewModel reports,
        Action<bool> diagnosticsChanged)
    {
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _systemInformation = systemInformation ?? throw new ArgumentNullException(nameof(systemInformation));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        Reports = reports ?? throw new ArgumentNullException(nameof(reports));
        _diagnosticsChanged = diagnosticsChanged ?? throw new ArgumentNullException(nameof(diagnosticsChanged));
        OpenCommand = new RelayCommand(Open);
        CloseCommand = new RelayCommand(Close);
        ShowRunReportsCommand = new RelayCommand(() => SelectSystemInformation(false));
        ShowSystemInformationCommand = new RelayCommand(() => SelectSystemInformation(true));
        RefreshCommand = new AsyncRelayCommand(
            cancellationToken => RefreshAsync(reloadCatalog: true, cancellationToken));
        OpenCurrentReportCommand = new RelayCommand(OpenCurrentReport, () => Reports.CanOpenReport);
        OpenReportHistoryCommand = new RelayCommand(OpenReportHistory, () => Reports.CanOpenReportHistory);
    }

    public ShellTextResources Text => _textProvider();

    /// <summary>Latest immutable System Information observation.</summary>
    public SystemInformationSnapshot Current => _systemInformation.Current;

    /// <summary>Existing persisted run-report owner, referenced without merging its lifecycle.</summary>
    public ReportPresentationViewModel Reports { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunReportsSelected))]
    public partial bool IsSystemInformationSelected { get; private set; } = true;

    /// <summary>True when immutable run history is selected.</summary>
    public bool IsRunReportsSelected => !IsSystemInformationSelected;

    public int ActiveBadgeCount => Current.ActiveDiagnostics.Count;

    public bool HasActiveDiagnostics => ActiveBadgeCount > 0;

    public bool HasNoActiveDiagnostics => !HasActiveDiagnostics;

    /// <summary>Localized diagnostic projections; stable codes remain Application-owned.</summary>
    public IReadOnlyList<MessageCenterDiagnosticItem> ActiveDiagnostics =>
    [
        .. Current.ActiveDiagnostics.Select(diagnostic => new MessageCenterDiagnosticItem(
            diagnostic.Code,
            Text.GetSystemDiagnosticCategory(diagnostic.Category),
            diagnostic.Severity,
            Text.GetSystemDiagnosticMessage(diagnostic),
            Text.GetSystemDiagnosticAction(diagnostic))),
    ];

    public ActionableSystemDiagnostic? GlobalBuildBlocker => Current.ActiveDiagnostics
        .FirstOrDefault(static diagnostic =>
            diagnostic.Severity == SystemDiagnosticSeverity.Blocking);

    public bool IsGlobalBuildBlocked => IsRefreshInProgress || GlobalBuildBlocker is not null;

    public string GlobalBuildBlockerText => IsRefreshInProgress
        ? Text.RefreshingDiagnosticsLabel
        : GlobalBuildBlocker is { } blocker
            ? $"{Text.GetSystemDiagnosticMessage(blocker)} {Text.GetSystemDiagnosticAction(blocker)}"
            : string.Empty;

    /// <summary>Compact canonical catalog state and identity.</summary>
    public string CatalogSummary => Current.CatalogVersion is null
        ? Text.GetCatalogStateLabel(Current.CatalogState)
        : $"{Text.GetCatalogStateLabel(Current.CatalogState)} · {Current.CatalogVersion}";

    public string MessageCenterAccessibleName =>
        Text.FormatMessageCenterAccessibleName(ActiveBadgeCount);

    /// <summary>Live system-state announcement after refresh or resolution.</summary>
    public string SystemStatusAnnouncement => IsRefreshInProgress
        ? Text.RefreshingDiagnosticsLabel
        : Text.FormatSystemDiagnosticAnnouncement(ActiveBadgeCount);

    /// <summary>Visible refresh action progress without requiring animation.</summary>
    public string RefreshActionLabel => IsRefreshInProgress
        ? Text.RefreshingDiagnosticsLabel
        : Text.RefreshDiagnosticsLabel;

    /// <summary>True while the explicit system refresh is running off the UI thread.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGlobalBuildBlocked))]
    [NotifyPropertyChangedFor(nameof(GlobalBuildBlockerText))]
    [NotifyPropertyChangedFor(nameof(SystemStatusAnnouncement))]
    [NotifyPropertyChangedFor(nameof(RefreshActionLabel))]
    public partial bool IsRefreshInProgress { get; private set; }

    /// <summary>Current export result; never represented as a Build Report.</summary>
    [ObservableProperty]
    public partial string ExportStatus { get; private set; } = string.Empty;

    public IRelayCommand OpenCommand { get; }

    public IRelayCommand CloseCommand { get; }

    /// <summary>Selects immutable run reports/history.</summary>
    public IRelayCommand ShowRunReportsCommand { get; }

    public IRelayCommand ShowSystemInformationCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Opens the existing current immutable run report.</summary>
    public IRelayCommand OpenCurrentReportCommand { get; }

    /// <summary>Opens the existing persisted report-history surface.</summary>
    public IRelayCommand OpenReportHistoryCommand { get; }

    /// <summary>Refreshes after the background startup catalog warm-up.</summary>
    public Task RefreshAfterStartupAsync(CancellationToken cancellationToken)
    {
        return RefreshAsync(reloadCatalog: false, cancellationToken);
    }

    public async Task ExportAsync(string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            await _exporter.ExportAsync(
                _systemInformation.CreateBundle(),
                destinationPath,
                cancellationToken);
            ExportStatus = Text.DiagnosticsExportedLabel;
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            ExportStatus = Text.DiagnosticsExportFailedLabel;
        }
    }

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(ActiveDiagnostics));
        OnPropertyChanged(nameof(CatalogSummary));
        OnPropertyChanged(nameof(MessageCenterAccessibleName));
        OnPropertyChanged(nameof(SystemStatusAnnouncement));
        OnPropertyChanged(nameof(GlobalBuildBlockerText));
        OnPropertyChanged(nameof(RefreshActionLabel));
        if (!string.IsNullOrEmpty(ExportStatus))
        {
            ExportStatus = string.Empty;
        }
    }

    internal void NotifyReportHistoryChanged()
    {
        OnPropertyChanged(nameof(Reports));
        OpenCurrentReportCommand.NotifyCanExecuteChanged();
        OpenReportHistoryCommand.NotifyCanExecuteChanged();
    }

    private void Open()
    {
        ExportStatus = string.Empty;
        IsOpen = true;
    }

    private void Close()
    {
        IsOpen = false;
    }

    private void SelectSystemInformation(bool selected)
    {
        if (IsSystemInformationSelected == selected)
        {
            return;
        }

        IsSystemInformationSelected = selected;
    }

    private Task RefreshAsync(
        bool reloadCatalog,
        CancellationToken cancellationToken)
    {
        if (_activeRefresh is { IsCompleted: false } active)
        {
            return reloadCatalog && !_activeRefreshReloadsCatalog
                ? RefreshAfterActiveAsync(active, cancellationToken)
                : active.WaitAsync(cancellationToken);
        }

        Task refresh = RefreshCoreAsync(reloadCatalog, cancellationToken);
        _activeRefreshReloadsCatalog = reloadCatalog;
        _activeRefresh = refresh;
        return ObserveRefreshCompletionAsync(refresh);
    }

    private async Task RefreshCoreAsync(bool reloadCatalog, CancellationToken cancellationToken)
    {
        string? previousPublicationToken = Current.PublicationToken;
        bool publicationChanged = false;
        PresentationObserver.Invoke(() => IsRefreshInProgress = true);
        PresentationObserver.Invoke(() => _diagnosticsChanged(false));
        try
        {
            SystemInformationSnapshot refreshed = await Task.Run(
                () => _systemInformation.Refresh(reloadCatalog, cancellationToken),
                cancellationToken);
            publicationChanged = reloadCatalog && !StringComparer.Ordinal.Equals(
                previousPublicationToken,
                refreshed.PublicationToken);
            PresentationObserver.Invoke(() => ExportStatus = string.Empty);
            NotifySystemStateChanged();
        }
        finally
        {
            PresentationObserver.Invoke(() => IsRefreshInProgress = false);
            if (publicationChanged)
            {
                _diagnosticsChanged(true);
            }
            else
            {
                PresentationObserver.Invoke(() => _diagnosticsChanged(false));
            }
        }
    }

    private async Task RefreshAfterActiveAsync(Task active, CancellationToken cancellationToken)
    {
        try
        {
            await active.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The explicit operator refresh still owns a fresh full attempt.
        }
        await RefreshAsync(reloadCatalog: true, cancellationToken);
    }

    private async Task ObserveRefreshCompletionAsync(Task refresh)
    {
        try
        {
            await refresh;
        }
        finally
        {
            if (ReferenceEquals(_activeRefresh, refresh))
            {
                _activeRefresh = null;
            }
        }
    }

    private void NotifySystemStateChanged()
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(Current)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ActiveBadgeCount)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasActiveDiagnostics)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasNoActiveDiagnostics)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ActiveDiagnostics)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(GlobalBuildBlocker)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsGlobalBuildBlocked)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(GlobalBuildBlockerText)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(CatalogSummary)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(MessageCenterAccessibleName)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(SystemStatusAnnouncement)));
    }

    private void OpenReportHistory()
    {
        if (!Reports.ShowReportHistoryCommand.CanExecute(null))
        {
            return;
        }

        Close();
        Reports.ShowReportHistoryCommand.Execute(null);
    }

    private void OpenCurrentReport()
    {
        if (!Reports.ShowReportCommand.CanExecute(null))
        {
            return;
        }

        Close();
        Reports.ShowReportCommand.Execute(null);
    }
}

internal sealed record MessageCenterDiagnosticItem(
    string Code,
    string Category,
    SystemDiagnosticSeverity Severity,
    string Message,
    string Action)
{
    public string AccessibleText => $"{Category}. {Message} {Action} {Code}";
}
