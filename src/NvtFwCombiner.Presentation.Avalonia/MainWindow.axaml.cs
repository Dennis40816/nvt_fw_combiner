using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private static readonly TimeSpan LocalStateCloseFlushTimeout = TimeSpan.FromSeconds(5);
    private readonly DispatcherTimer _reportToastHoldTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _reportToastFadeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>>
        _reportHistoryPersistence;
    private readonly LatestSnapshotPersistenceCoordinator<ShellPreferenceSnapshot>
        _shellPreferencePersistence;
    private readonly CancellationTokenSource _startupLoadCancellation = new();
    private readonly ForegroundLoadingState _catalogLoading = new();
    private readonly PresentationHostServices _hostServices;
    private readonly UiLaunchOptions _launchOptions;
    private readonly StartupTraceSession _startupTrace;
    private bool _isReportHistoryClosePending;
    private bool _isReportHistoryPersistenceComplete;
    private bool _isDisposed;
    private bool _isStartupLoadStarted;
    private bool _isCanonicalCatalogWarmupInProgress;
    private bool _isDeferredStartupComplete;
    private int _catalogLoadingAttempt;

    /// <summary>Initializes the XAML loader constructor; production supplies explicit startup state.</summary>
    public MainWindow()
        : this(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            App.HostServices ?? throw new InvalidOperationException("Presentation host services are not configured."),
            ShellPreferenceSnapshot.Default)
    {
    }

    internal MainWindow(
        UiLaunchOptions launchOptions,
        StartupTraceSession startupTrace,
        PresentationHostServices hostServices,
        ShellPreferenceSnapshot startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);
        ArgumentNullException.ThrowIfNull(startupTrace);
        ArgumentNullException.ThrowIfNull(hostServices);
        ArgumentNullException.ThrowIfNull(startupPreferences);
        _launchOptions = launchOptions;
        _startupTrace = startupTrace;
        _hostServices = hostServices;
        _reportHistoryPersistence = new(
            (snapshots, cancellationToken) => ReportHistoryFileStore.SaveAsync(
                hostServices.LocalFiles,
                ReportHistoryFileStore.DefaultHistoryPath,
                snapshots,
                cancellationToken),
            snapshots => [.. snapshots]);
        _shellPreferencePersistence = new(
            (snapshot, cancellationToken) => ShellPreferenceFileStore.SaveAsync(
                hostServices.LocalFiles,
                ShellPreferenceFileStore.DefaultPreferencesPath,
                snapshot,
                cancellationToken),
            static snapshot => snapshot);
        _startupTrace.Mark("main-window-constructor.started");

        InitializeComponent();
        _startupTrace.Mark("main-window-xaml.ready");
        _reportToastHoldTimer.Tick += ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick += ReportToastFadeTimer_OnTick;
        MainWindowViewModel viewModel = CreateStartupViewModel(_hostServices, startupPreferences);
        _startupTrace.Mark("shell-view-model.created");
        _catalogLoading.SetReducedMotion(viewModel.IsReducedMotionEnabled);
        BeginCatalogLoading(
            viewModel.Text.CatalogLoadingTitle,
            viewModel.Text.CatalogLoadingDetail,
            progress: 0);
        CatalogLoadingSurfaceHost.DataContext = _catalogLoading;
        _startupTrace.Mark("shell-preferences.applied");
        DataContext = viewModel;
        _startupTrace.Mark("shell-data-context.assigned");
        ApplyDeferredShellContent(viewModel);
        _startupTrace.Mark("shell-initial-content.ready");
        ApplyThemePreference(viewModel.SelectedTheme);
        _startupTrace.Mark("shell-theme.applied");

        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += ViewModel_OnPropertyChanged;
        }
        viewModel.Reports.PropertyChanged += Reports_OnPropertyChanged;
        _startupTrace.Mark("shell-notifications.ready");

        _startupTrace.Mark("main-window-constructor.completed");
    }

    internal static MainWindowViewModel CreateStartupViewModel(
        PresentationHostServices hostServices,
        ShellPreferenceSnapshot startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        ArgumentNullException.ThrowIfNull(startupPreferences);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            hostServices,
            ShellTextResources.LanguageFromPreference(startupPreferences.Language));
        viewModel.LoadShellPreferences(startupPreferences);
        return viewModel;
    }

    /// <inheritdoc />
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_isDisposed)
        {
            _startupLoadCancellation.Cancel();
        }

        if (_isReportHistoryPersistenceComplete)
        {
            if (DataContext is MainWindowViewModel finalViewModel)
            {
                finalViewModel.RunSession.CancelActiveRun();
            }

            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_isReportHistoryClosePending)
        {
            base.OnClosing(e);
            return;
        }

        _isReportHistoryClosePending = true;
        IsEnabled = false;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.RunSession.CancelActiveRun();
        }

        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
        if (DataContext is MainWindowViewModel closingViewModel)
        {
            closingViewModel.Reports.PropertyChanged -= Reports_OnPropertyChanged;
        }

        var completion = Task.WhenAll(
            _reportHistoryPersistence.CompleteAsync(),
            _shellPreferencePersistence.CompleteAsync());
        base.OnClosing(e);
        try
        {
            await completion.WaitAsync(LocalStateCloseFlushTimeout);
        }
        catch (TimeoutException)
        {
            // Report history is best-effort local state; a stalled save must not trap the application open.
        }

        _isReportHistoryPersistenceComplete = true;
        _isReportHistoryClosePending = false;
        Dispatcher.UIThread.Post(Close);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
        if (DataContext is MainWindowViewModel closedViewModel)
        {
            closedViewModel.Reports.PropertyChanged -= Reports_OnPropertyChanged;
        }

        _reportToastHoldTimer.Stop();
        _reportToastFadeTimer.Stop();
        _reportToastHoldTimer.Tick -= ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick -= ReportToastFadeTimer_OnTick;
        base.OnClosed(e);
        Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _startupLoadCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowState = WindowState.Maximized;
        _startupTrace.Mark("main-window.opened");
        if (_isStartupLoadStarted || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _isStartupLoadStarted = true;
        CancellationToken startupCancellation = _startupLoadCancellation.Token;
        CatalogLoadingSurfaceHost.Content = _catalogLoading;
        await Task.Yield();
        await ContinueStartupAsync(
            viewModel,
            startupCancellation);
    }

    private async Task ContinueStartupAsync(
        MainWindowViewModel viewModel,
        CancellationToken startupCancellation)
    {
        if (!await TryWarmCanonicalCatalogAsync(
                viewModel,
                startupCancellation) ||
            _isDeferredStartupComplete)
        {
            return;
        }

        try
        {
            ApplyLaunchPage(viewModel, _launchOptions.Page);
            _startupTrace.Mark("startup-launch-page.ready");
            await ApplyDeferredLaunchOptionsAsync(
                viewModel,
                _hostServices.LocalFiles,
                _launchOptions,
                startupCancellation);
            _startupTrace.Mark("startup-launch-options.ready");
            await viewModel.MessageCenter.RefreshAfterStartupAsync(startupCancellation);
            if (viewModel.IsSettingsVisible)
            {
                viewModel.Settings.Refresh(viewModel.Text);
            }

            _startupTrace.Mark("startup-warmup.catalogs.ready");
            await WarmDeferredShellAsync(viewModel, startupCancellation);
            _isDeferredStartupComplete = true;
            _ = _startupTrace.Complete("startup-warmup.completed");
        }
        catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
        {
            _ = _startupTrace.Complete("startup-warmup.cancelled");
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Deferred shell warm-up did not complete: {0}", exception.Message);
            _ = _startupTrace.Complete("startup-warmup.failed");
        }
    }

    private async Task<bool> TryWarmCanonicalCatalogAsync(
        MainWindowViewModel viewModel,
        CancellationToken startupCancellation)
    {
        if (viewModel.WorkflowSession.IsCanonicalCatalogReady)
        {
            CompleteCatalogLoading();
            return true;
        }

        if (_isCanonicalCatalogWarmupInProgress)
        {
            return false;
        }

        _isCanonicalCatalogWarmupInProgress = true;
        BeginCatalogLoading(
            viewModel.Text.CatalogLoadingTitle,
            viewModel.Text.CatalogLoadingDetail,
            progress: 0);
        int attempt = ++_catalogLoadingAttempt;
        try
        {
            CapabilityCatalogReloadResult reload =
                await CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(
                    _hostServices.CanonicalCatalogLoader,
                    (progress, cancellationToken) => ReportCatalogProgressAsync(
                        viewModel,
                        progress,
                        attempt,
                        cancellationToken),
                    cancellationToken => ApplyCanonicalCatalogStateAsync(
                        viewModel,
                        attempt,
                        cancellationToken),
                    startupCancellation);

            if (!reload.Succeeded)
            {
                throw new InvalidOperationException(string.Join(
                    Environment.NewLine,
                    reload.Issues.Select(static issue =>
                        $"{issue.Code}: {issue.Message}")));
            }
            CompleteCatalogLoading();
            _startupTrace.Mark("startup-warmup.catalog-state.applied");
            return true;
        }
        catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
        {
            CompleteCatalogLoading();
            _ = _startupTrace.Complete("startup-warmup.cancelled");
            return false;
        }
        catch (Exception exception)
        {
            FailCatalogLoading(
                viewModel.Text.CatalogLoadingFailedTitle,
                viewModel.Text.CatalogLoadingFailedDetail,
                viewModel.Text.RetryLabel);
            Trace.TraceWarning("Canonical catalog warm-up did not complete: {0}", exception.Message);
            _startupTrace.Mark("startup-warmup.catalog-load.failed");
            return false;
        }
        finally
        {
            _isCanonicalCatalogWarmupInProgress = false;
        }
    }

    private async ValueTask ReportCatalogProgressAsync(
        MainWindowViewModel viewModel,
        CanonicalCatalogStartupProgress progress,
        int attempt,
        CancellationToken startupCancellation)
    {
        await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                ApplyCatalogProgress(
                    _catalogLoading,
                    viewModel.Text,
                    _catalogLoadingAttempt,
                    attempt,
                    progress);
            },
            DispatcherPriority.Render,
            startupCancellation);
    }

    internal static void ApplyCatalogProgress(
        ForegroundLoadingState catalogLoading,
        ShellTextResources text,
        int currentAttempt,
        int attempt,
        CanonicalCatalogStartupProgress progress)
    {
        ArgumentNullException.ThrowIfNull(catalogLoading);
        ArgumentNullException.ThrowIfNull(text);
        if (attempt != currentAttempt ||
            !catalogLoading.IsRunning ||
            !double.IsFinite(progress.Value) ||
            progress.Value is < 0 or > 1 ||
            (catalogLoading.Progress is { } current && progress.Value < current))
        {
            return;
        }

        string detail = progress.Phase switch
        {
            CanonicalCatalogStartupPhase.Dispatched => text.CatalogLoadingDetail,
            CanonicalCatalogStartupPhase.MaterializingRoutes => text.CatalogMaterializingDetail,
            CanonicalCatalogStartupPhase.ApplyingState => text.CatalogApplyingDetail,
            CanonicalCatalogStartupPhase.Ready => text.CatalogReadyDetail,
            _ => throw new InvalidOperationException("Unknown catalog startup phase."),
        };
        catalogLoading.ReportProgress(progress.Value, detail);
    }

    private async ValueTask ApplyCanonicalCatalogStateAsync(
        MainWindowViewModel viewModel,
        int attempt,
        CancellationToken startupCancellation)
    {
        await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (attempt == _catalogLoadingAttempt && _catalogLoading.IsRunning)
                {
                    viewModel.PublishCanonicalCatalogState();
                }
            },
            DispatcherPriority.Render,
            startupCancellation);
    }

    private async void CatalogLoadingSurface_OnRetryRequested(object? sender, EventArgs e)
    {
        if (_isDisposed || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await ContinueStartupAsync(
            viewModel,
            _startupLoadCancellation.Token);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        ApplyDeferredShellContent(viewModel);

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTheme))
        {
            ApplyThemePreference(viewModel.SelectedTheme);
        }

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedLanguage) or
            nameof(MainWindowViewModel.IsReducedMotionEnabled))
        {
            RefreshCatalogLoadingPresentation(viewModel);
        }

        if (IsShellPreferenceProperty(e.PropertyName))
        {
            _shellPreferencePersistence.Queue(viewModel.ExportShellPreferences());
        }

    }

    private void Reports_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ReportPresentationViewModel reports)
        {
            return;
        }

        if (e.PropertyName == nameof(ReportPresentationViewModel.ReportHistoryCount))
        {
            _reportHistoryPersistence.Queue(reports.ExportReportHistory());
        }

        if (e.PropertyName != nameof(ReportPresentationViewModel.HasReportToast))
        {
            return;
        }

        if (reports.HasReportToast)
        {
            _reportToastFadeTimer.Stop();
            _reportToastHoldTimer.Stop();
            reports.SetReportToastOpacity(1);
            _reportToastHoldTimer.Start();
        }
        else
        {
            _reportToastHoldTimer.Stop();
            _reportToastFadeTimer.Stop();
        }
    }

    private static bool IsShellPreferenceProperty(string? propertyName)
    {
        return propertyName is
            nameof(MainWindowViewModel.SelectedTheme) or
            nameof(MainWindowViewModel.SelectedLanguage) or
            nameof(MainWindowViewModel.IsReducedMotionEnabled);
    }

    private void ApplyDeferredShellContent(MainWindowViewModel viewModel)
    {
        LoadContent(DeviceContextHost, viewModel.IsDeviceContextVisible, viewModel);
        LoadContent(HomePageHost, viewModel.IsHomeVisible, viewModel);
        LoadContent(SettingsPageHost, viewModel.IsSettingsVisible, viewModel);
        LoadContent(HexEditorPageHost, viewModel.IsHexEditorVisible, viewModel);
        LoadContent(ReplacePageHost, viewModel.IsReplaceVisible, viewModel.Replace);
        LoadContent(MergePageHost, viewModel.IsMergeVisible, viewModel.Merge);
        LoadContent(ReportToastHost, viewModel.Reports.HasReportToast, viewModel.Reports);
        LoadContent(ReplaceSelectionModalHost, viewModel.Replace.IsReplaceSelectionModalOpen, viewModel.Replace);
        LoadContent(CtrlRamFirmwareVersionModalHost,
            viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen,
            viewModel.Replace);
        LoadContent(AbAFlashCodeDeliveryPromptModalHost,
            viewModel.Merge.IsAbAFlashCodeDeliveryPromptOpen,
            viewModel.Merge);
        LoadContent(WorkflowContextSetupModalHost, viewModel.WorkflowSession.IsWorkflowContextModalOpen, viewModel.WorkflowSession);
        LoadContent(FirmwareIcMismatchModalHost, viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen, viewModel.WorkflowSession);
        LoadContent(FirmwareNumberMismatchModalHost, viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen, viewModel.WorkflowSession);
        LoadContent(NavigationClearConfirmationModalHost, viewModel.IsNavigationClearConfirmationOpen, viewModel);
        LoadContent(MessageCenterModalHost, viewModel.MessageCenter.IsOpen, viewModel.MessageCenter);
        LoadContent(ReportModalHost, viewModel.Reports.IsReportModalOpen, viewModel.Reports);
        LoadContent(BuildCompletedModalHost, viewModel.BuildResult.IsOpen, viewModel);
    }

    private static void LoadContent(ContentControl host, bool shouldLoad, object content)
    {
        if (shouldLoad)
        {
            host.Content ??= content;
        }
    }

    private void ApplyThemePreference(string selectedTheme)
    {
        RequestedThemeVariant = selectedTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void RefreshCatalogLoadingPresentation(MainWindowViewModel viewModel)
    {
        _catalogLoading.SetReducedMotion(viewModel.IsReducedMotionEnabled);
        if (_catalogLoading.IsRunning)
        {
            _catalogLoading.Begin(
                viewModel.Text.CatalogLoadingTitle,
                viewModel.Text.CatalogLoadingDetail,
                _catalogLoading.Progress);
        }
        else if (_catalogLoading.HasFailed)
        {
            _catalogLoading.Fail(
                viewModel.Text.CatalogLoadingFailedTitle,
                viewModel.Text.CatalogLoadingFailedDetail,
                viewModel.Text.RetryLabel);
        }
    }

    private void BeginCatalogLoading(
        string title,
        string detail,
        double? progress)
    {
        ShellInteractionHost.IsEnabled = false;
        _catalogLoading.Begin(title, detail, progress);
    }

    private void FailCatalogLoading(
        string title,
        string detail,
        string retryLabel)
    {
        ShellInteractionHost.IsEnabled = false;
        _catalogLoading.Fail(title, detail, retryLabel);
    }

    private void CompleteCatalogLoading()
    {
        _catalogLoading.Complete();
        ShellInteractionHost.IsEnabled = true;
        Dispatcher.UIThread.Post(
            () => _ = HomeNavigationButton.Focus(NavigationMethod.Tab),
            DispatcherPriority.Input);
    }
}
