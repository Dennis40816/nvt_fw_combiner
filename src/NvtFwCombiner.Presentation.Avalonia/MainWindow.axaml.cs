using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private static readonly TimeSpan LocalStateCloseFlushTimeout = TimeSpan.FromSeconds(5);
    private readonly DispatcherTimer _reportToastHoldTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _reportToastFadeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly LatestSnapshotPersistenceCoordinator<IReadOnlyList<ReportHistorySnapshot>>
        _reportHistoryPersistence = new(ReportHistoryFileStore.SaveAsync, snapshots => [.. snapshots]);
    private readonly LatestSnapshotPersistenceCoordinator<ShellPreferenceSnapshot>
        _shellPreferencePersistence = new(ShellPreferenceFileStore.SaveAsync, static snapshot => snapshot);
    private readonly CancellationTokenSource _startupLoadCancellation = new();
    private readonly UiLaunchOptions _launchOptions;
    private readonly StartupTraceSession _startupTrace;
    private bool _isReportHistoryClosePending;
    private bool _isReportHistoryPersistenceComplete;
    private bool _isDisposed;
    private bool _isStartupLoadStarted;

    /// <summary>Initializes the main window controls.</summary>
    public MainWindow()
        : this(UiLaunchOptions.Empty, StartupTraceSession.Disabled)
    {
    }

    /// <summary>Initializes the main window controls with command-line startup state.</summary>
    public MainWindow(UiLaunchOptions launchOptions)
        : this(launchOptions, StartupTraceSession.Disabled)
    {
    }

    internal MainWindow(UiLaunchOptions launchOptions, StartupTraceSession startupTrace)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);
        ArgumentNullException.ThrowIfNull(startupTrace);
        _launchOptions = launchOptions;
        _startupTrace = startupTrace;
        _startupTrace.Mark("main-window-constructor.started");

        InitializeComponent();
        _startupTrace.Mark("main-window-xaml.ready");
        _reportToastHoldTimer.Tick += ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick += ReportToastFadeTimer_OnTick;
        ShellPreferenceSnapshot preferences = ShellPreferenceFileStore.Load(
            ShellPreferenceFileStore.DefaultPreferencesPath);
        _startupTrace.Mark("shell-preferences.loaded");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            ShellTextResources.LanguageFromPreference(preferences.Language));
        _startupTrace.Mark("shell-view-model.created");
        viewModel.LoadShellPreferences(preferences);
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
        _startupTrace.Mark("shell-notifications.ready");

        ApplyInitialLaunchOptions(viewModel, launchOptions);
        _startupTrace.Mark("initial-launch-options.applied");
        _startupTrace.Mark("main-window-constructor.completed");
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
                finalViewModel.CancelActiveRun();
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
            viewModel.CancelActiveRun();
        }

        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= ViewModel_OnPropertyChanged;
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
        await Task.Yield();
        try
        {
            var catalogWarmup = Task.Run(
                () => PrimeDeferredCatalogs(viewModel.SelectedIc, viewModel.SelectedNumber, startupCancellation),
                startupCancellation);
            await ApplyDeferredLaunchOptionsAsync(viewModel, _launchOptions, startupCancellation);
            _startupTrace.Mark("startup-launch-options.ready");
            await catalogWarmup;
            _startupTrace.Mark("startup-warmup.catalogs.ready");
            await WarmDeferredShellAsync(viewModel, startupCancellation);
            _ = _startupTrace.Complete("startup-warmup.completed");
        }
        catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
        {
            _ = _startupTrace.Complete("startup-warmup.cancelled");
        }
        catch (Exception exception)
        {
            // Warm-up is a best-effort latency optimization. Navigation keeps its existing first-use path.
            Trace.TraceWarning("Deferred shell warm-up did not complete: {0}", exception.Message);
            _ = _startupTrace.Complete("startup-warmup.failed");
        }
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

        if (IsShellPreferenceProperty(e.PropertyName))
        {
            _shellPreferencePersistence.Queue(viewModel.ExportShellPreferences());
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ReportHistoryCount))
        {
            _reportHistoryPersistence.Queue(viewModel.ExportReportHistory());
        }

        if (e.PropertyName != nameof(MainWindowViewModel.HasReportToast))
        {
            return;
        }

        if (viewModel.HasReportToast)
        {
            _reportToastFadeTimer.Stop();
            _reportToastHoldTimer.Stop();
            viewModel.SetReportToastOpacity(1);
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
        LoadContent(ReplacePageHost, viewModel.IsReplaceVisible, viewModel);
        LoadContent(MergePageHost, viewModel.IsMergeVisible, viewModel);
        LoadContent(ReportToastHost, viewModel.HasReportToast, viewModel);
        LoadContent(ReplaceSelectionModalHost, viewModel.IsReplaceSelectionModalOpen, viewModel);
        LoadContent(CtrlRamFirmwareVersionModalHost, viewModel.IsCtrlRamFirmwareVersionModalOpen, viewModel);
        LoadContent(WorkflowContextSetupModalHost, viewModel.IsWorkflowContextModalOpen, viewModel);
        LoadContent(FirmwareIcMismatchModalHost, viewModel.IsFirmwareIcMismatchModalOpen, viewModel);
        LoadContent(NavigationClearConfirmationModalHost, viewModel.IsNavigationClearConfirmationOpen, viewModel);
        LoadContent(ReportModalHost, viewModel.IsReportModalOpen, viewModel);
        LoadContent(BuildCompletedModalHost, viewModel.IsBuildCompletedModalOpen, viewModel);
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
}
