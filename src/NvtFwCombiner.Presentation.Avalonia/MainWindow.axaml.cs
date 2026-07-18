using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan ReportHistoryCloseFlushTimeout = TimeSpan.FromSeconds(5);
    private readonly DispatcherTimer _reportToastHoldTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _reportToastFadeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly ReportHistoryPersistenceCoordinator _reportHistoryPersistence =
        new(ReportHistoryFileStore.SaveAsync);
    private bool _isReportHistoryClosePending;
    private bool _isReportHistoryPersistenceComplete;

    /// <summary>Initializes the main window controls.</summary>
    public MainWindow()
        : this(UiLaunchOptions.Empty)
    {
    }

    /// <summary>Initializes the main window controls with command-line startup state.</summary>
    public MainWindow(UiLaunchOptions launchOptions)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);

        InitializeComponent();
        _reportToastHoldTimer.Tick += ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick += ReportToastFadeTimer_OnTick;
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        ReportHistoryFileStore.LoadInto(viewModel);
        ShellPreferenceFileStore.LoadInto(viewModel);
        DataContext = viewModel;
        ApplyThemePreference(viewModel.SelectedTheme);

        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyLaunchOptions(viewModel, launchOptions);
    }

    /// <inheritdoc />
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

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

        Task completion = _reportHistoryPersistence.CompleteAsync();
        base.OnClosing(e);
        try
        {
            await completion.WaitAsync(ReportHistoryCloseFlushTimeout);
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
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowState = WindowState.Maximized;
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
            ShellPreferenceFileStore.Save(viewModel);
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
