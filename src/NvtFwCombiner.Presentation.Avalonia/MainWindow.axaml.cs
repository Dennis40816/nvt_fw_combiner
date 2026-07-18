using System.ComponentModel;
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
    private bool _isReportHistoryClosePending;
    private bool _isReportHistoryPersistenceComplete;
    private bool _isDisposed;
    private bool _isStartupLoadStarted;

    /// <summary>Initializes the main window controls.</summary>
    public MainWindow()
        : this(UiLaunchOptions.Empty)
    {
    }

    /// <summary>Initializes the main window controls with command-line startup state.</summary>
    public MainWindow(UiLaunchOptions launchOptions)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);
        _launchOptions = launchOptions;

        InitializeComponent();
        _reportToastHoldTimer.Tick += ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick += ReportToastFadeTimer_OnTick;
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        ShellPreferenceFileStore.LoadInto(viewModel);
        DataContext = viewModel;
        ApplyThemePreference(viewModel.SelectedTheme);

        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyInitialLaunchOptions(viewModel, launchOptions);
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
        if (_isStartupLoadStarted || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _isStartupLoadStarted = true;
        CancellationToken startupCancellation = _startupLoadCancellation.Token;
        await Task.Yield();
        try
        {
            await ApplyDeferredLaunchOptionsAsync(viewModel, _launchOptions, startupCancellation);
        }
        catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
        {
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

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
