using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private readonly ForegroundLoadingState _preloadLoading = new();
    private readonly ShellPreloadSession _preloadSession;
    private readonly PresentationHostServices _hostServices;
    private readonly UiLaunchOptions _launchOptions;
    private readonly StartupTraceSession _startupTrace;
    private bool _isReportHistoryClosePending;
    private bool _isReportHistoryPersistenceComplete;
    private bool _isDisposed;
    private bool _isStartupLoadStarted;

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
        _preloadSession = new(
            stage => PresentPreloadStage(viewModel, stage),
            viewModel.Text,
            HasStartupReportStage(_launchOptions));
        _preloadSession.SetReducedMotion(viewModel.IsReducedMotionEnabled);
        _startupTrace.Mark("shell-view-model.created");
        _preloadLoading.SetReducedMotion(viewModel.IsReducedMotionEnabled);
        ShellInteractionHost.IsEnabled = false;
        _preloadLoading.Begin(
            viewModel.Text.CatalogLoadingTitle,
            $"{_preloadSession.CatalogStage.PositionLabel} · {viewModel.Text.CatalogLoadingDetail}",
            progress: 0);
        _preloadLoading.SetCancellationAction(viewModel.Text.CancelStartupLabel);
        CatalogLoadingSurfaceHost.DataContext = _preloadLoading;
        OptionalPreloadStatusHost.DataContext = _preloadSession;
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
            _shellPreferencePersistence.CompleteAsync(),
            _preloadSession.CancelAndDrainAsync());
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
        _preloadSession.Dispose();
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
        CatalogLoadingSurfaceHost.Content = _preloadLoading;
        await Task.Yield();
        await RunStartupPreloadAsync(viewModel, startupCancellation);
    }

    private async Task RunStartupPreloadAsync(
        MainWindowViewModel viewModel,
        CancellationToken startupCancellation)
    {
        if (!await RunRequiredPreloadAsync(viewModel, startupCancellation))
        {
            return;
        }

        try
        {
            await _preloadSession.RunOptionalStagesAsync(
                new(
                    () =>
                    {
                        ApplyLaunchPage(viewModel, _launchOptions.Page);
                        _startupTrace.Mark("startup-launch-page.ready");
                    },
                    async cancellationToken => RequireStartupPublication(await
                        viewModel.Reports.LoadReportHistoryAsync(
                            token => ReportHistoryFileStore.LoadAsync(
                                _hostServices.LocalFiles,
                                ReportHistoryFileStore.DefaultHistoryPath,
                                token),
                            cancellationToken)),
                    HasStartupReportStage(_launchOptions)
                        ? (progress, cancellationToken) => ApplyStartupReportAsync(
                            viewModel,
                            _hostServices.LocalFiles,
                            _launchOptions,
                            progress,
                            cancellationToken)
                        : null,
                    async cancellationToken =>
                    {
                        await viewModel.MessageCenter.RefreshAfterStartupAsync(cancellationToken);
                        if (viewModel.IsSettingsVisible)
                        {
                            viewModel.Settings.Refresh(viewModel.Text);
                        }
                    },
                    (progress, isCurrent, cancellationToken) => WarmDeferredShellAsync(
                        viewModel,
                        progress,
                        isCurrent,
                        cancellationToken),
                    async (progress, cancellationToken) =>
                        await viewModel.MessageCenter.RefreshExternalEnvironmentAfterStartupAsync(
                            progress,
                            cancellationToken)),
                startupCancellation);
            ShellPreloadStageState history = _preloadSession.Stage(ShellPreloadSession.HistoryStageId).State;
            bool hasStartupReport = HasStartupReportStage(_launchOptions);
            ShellPreloadStageState report = hasStartupReport
                ? _preloadSession.Stage(ShellPreloadSession.ReportStageId).State
                : ShellPreloadStageState.Succeeded;
            ShellPreloadStageState diagnostics = _preloadSession.Stage(ShellPreloadSession.DiagnosticsStageId).State;
            ShellPreloadStageState externalEnvironment = _preloadSession.Stage(
                ShellPreloadSession.ExternalEnvironmentStageId).State;
            if (history == ShellPreloadStageState.Succeeded &&
                (!hasStartupReport || report == ShellPreloadStageState.Succeeded))
            {
                _startupTrace.Mark("startup-launch-options.ready");
            }
            if (diagnostics == ShellPreloadStageState.Succeeded)
            {
                _startupTrace.Mark("startup-warmup.catalogs.ready");
            }

            ShellPreloadStageState[] optionals = [history, report, diagnostics, externalEnvironment,
                _preloadSession.Stage(ShellPreloadSession.ViewsStageId).State];
            string terminal = optionals.Any(static state => state is
                ShellPreloadStageState.Failed or ShellPreloadStageState.DependencyBlocked or
                ShellPreloadStageState.Pending or ShellPreloadStageState.Running)
                ? "startup-warmup.failed"
                : optionals.Contains(ShellPreloadStageState.Cancelled)
                    ? "startup-warmup.cancelled"
                    : "startup-warmup.completed";
            _ = _startupTrace.Complete(terminal);
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

    private async Task<bool> RunRequiredPreloadAsync(
        MainWindowViewModel viewModel,
        CancellationToken startupCancellation)
    {
        if (viewModel.WorkflowSession.IsCanonicalCatalogReady)
        {
            _preloadSession.AdoptReadyCatalog();
            return true;
        }

        if (_preloadSession.CatalogStage.CurrentAttempt is not null && !_preloadSession.CanRetryCatalog)
        {
            return false;
        }

        try
        {
            CapabilityCatalogReloadResult reload = await _preloadSession.RunCatalogAsync(
                _hostServices.CanonicalCatalogLoader,
                async cancellationToken => await Dispatcher.UIThread.InvokeAsync(
                    viewModel.PublishCanonicalCatalogState,
                    DispatcherPriority.Render,
                    cancellationToken),
                retry: _preloadSession.CatalogStage.CurrentAttempt is not null,
                startupCancellation);
            if (!reload.Succeeded)
            {
                _startupTrace.Mark("startup-warmup.catalog-load.failed");
                return false;
            }

            _startupTrace.Mark("startup-warmup.catalog-state.applied");
            return true;
        }
        catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
        {
            _ = _startupTrace.Complete("startup-warmup.cancelled");
            return false;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Canonical catalog warm-up did not complete: {0}", exception.Message);
            _startupTrace.Mark("startup-warmup.catalog-load.failed");
            return false;
        }
    }

    private async void CatalogLoadingSurface_OnRetryRequested(object? sender, EventArgs e)
    {
        if (_isDisposed || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await RunStartupPreloadAsync(viewModel, _startupLoadCancellation.Token);
    }

    private void CatalogLoadingSurface_OnCancelRequested(object? sender, EventArgs e)
    {
        _ = _preloadSession.CancelAndDrainAsync();
        Close();
    }

    private void PresentPreloadStage(
        MainWindowViewModel viewModel,
        ShellPreloadStageSnapshot stage)
    {
        if (!stage.IsRequired)
        {
            return;
        }

        bool succeeded = stage.CurrentAttempt?.State == ShellPreloadStageState.Succeeded;
        CommitRequiredStagePresentation(
            succeeded,
            ShellInteractionHost.IsEnabled,
            enabled => ShellInteractionHost.IsEnabled = enabled,
            () => Dispatcher.UIThread.Post(
                () => _ = HomeNavigationButton.Focus(NavigationMethod.Tab),
                DispatcherPriority.Input),
            () => ApplyPreloadStage(_preloadSession, _preloadLoading, viewModel.Text, stage));
    }

    internal static void CommitRequiredStagePresentation(
        bool succeeded,
        bool shellWasEnabled,
        Action<bool> setShellEnabled,
        Action restoreFocus,
        Action presentLoadingState)
    {
        setShellEnabled(succeeded);
        if (succeeded && !shellWasEnabled)
        {
            restoreFocus();
        }
        presentLoadingState();
    }

    internal static void ApplyPreloadStage(
        ShellPreloadSession session,
        ForegroundLoadingState loading,
        ShellTextResources text,
        ShellPreloadStageSnapshot stage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentNullException.ThrowIfNull(text);
        ShellPreloadAttemptSnapshot? attempt = stage.CurrentAttempt;
        if (attempt is null || session.CatalogStage.CurrentAttempt?.Identity != attempt.Identity)
        {
            return;
        }

        string prefix = $"{stage.Index} / {stage.Count} · ";
        if (attempt.State == ShellPreloadStageState.Failed)
        {
            loading.Fail(
                text.CatalogLoadingFailedTitle,
                prefix + text.CatalogLoadingFailedDetail,
                text.RetryLabel);
            loading.SetCancellationAction(text.CancelStartupLabel);
            return;
        }
        if (attempt.State is ShellPreloadStageState.Succeeded or ShellPreloadStageState.Cancelled)
        {
            loading.Complete();
            return;
        }

        string phase = attempt.Progress == 1
            ? text.CatalogApplyingDetail
            : attempt.Progress is > 0 ? text.CatalogMaterializingDetail : text.CatalogLoadingDetail;
        string detail = prefix + phase;
        double progress = attempt.Progress ?? 0;
        if (!loading.IsRunning)
        {
            loading.Begin(text.CatalogLoadingTitle, detail, progress);
            loading.SetCancellationAction(text.CancelStartupLabel);
            return;
        }

        bool announce = detail != loading.Detail ||
            (int)(progress * 10) != (int)((loading.Progress ?? 0) * 10);
        loading.ReportProgress(progress, detail, announce);
    }

    private async void OptionalPreloadRetryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string stageId })
        {
            Task<bool> retry = _preloadSession.TryRetryOptionalAsync(stageId, _startupLoadCancellation.Token);
            _ = OptionalPreloadStatusHost.Focus(NavigationMethod.Tab);
            _ = await retry;
        }
    }

    private void OptionalPreloadSkipButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string stageId })
        {
            _ = _preloadSession.TrySkipOptional(stageId);
            _ = OptionalPreloadStatusHost.Focus(NavigationMethod.Tab);
        }
    }

    private async void OptionalPreloadCancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Task cancellation = _preloadSession.CancelOptionalsAndDrainAsync();
        _ = OptionalPreloadStatusHost.Focus(NavigationMethod.Tab);
        await cancellation;
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
            RefreshPreloadPresentation(viewModel);
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

    private void RefreshPreloadPresentation(MainWindowViewModel viewModel)
    {
        _preloadLoading.SetReducedMotion(viewModel.IsReducedMotionEnabled);
        _preloadSession.SetReducedMotion(viewModel.IsReducedMotionEnabled);
        _preloadSession.Relocalize(viewModel.Text);
        ApplyPreloadStage(
            _preloadSession,
            _preloadLoading,
            viewModel.Text,
            _preloadSession.CatalogStage);
    }

}
