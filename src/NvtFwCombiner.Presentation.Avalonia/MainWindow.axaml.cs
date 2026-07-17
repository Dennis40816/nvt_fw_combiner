using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _reportToastHoldTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _reportToastFadeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };

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
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelActiveRun();
        }

        base.OnClosing(e);
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
            ReportHistoryFileStore.Save(viewModel);
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
            nameof(MainWindowViewModel.SelectedLanguage);
    }

    private void ApplyThemePreference(string selectedTheme)
    {
        RequestedThemeVariant = selectedTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" or "High contrast" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
