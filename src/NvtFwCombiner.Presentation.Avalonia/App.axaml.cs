using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Avalonia application bootstrapper.</summary>
public sealed partial class App : global::Avalonia.Application
{
    /// <summary>Gets UI startup state parsed by the process entry point.</summary>
    internal static UiLaunchOptions StartupOptions { get; private set; } = UiLaunchOptions.Empty;

    /// <summary>Gets the opt-in startup trace associated with this process.</summary>
    internal static StartupTraceSession StartupTrace { get; private set; } = StartupTraceSession.Disabled;

    /// <summary>Sets UI startup state before the framework creates the main window.</summary>
    internal static void SetStartupOptions(UiLaunchOptions startupOptions)
    {
        SetStartupOptions(startupOptions, StartupTraceSession.Disabled);
    }

    /// <summary>Sets UI startup state and its opt-in trace before framework initialization.</summary>
    internal static void SetStartupOptions(
        UiLaunchOptions startupOptions,
        StartupTraceSession startupTrace)
    {
        ArgumentNullException.ThrowIfNull(startupOptions);
        ArgumentNullException.ThrowIfNull(startupTrace);

        StartupOptions = startupOptions;
        StartupTrace = startupTrace;
    }

    /// <summary>Loads the compiled Avalonia XAML for the application.</summary>
    public override void Initialize()
    {
        StartupTrace.Mark("application-xaml.started");
        AvaloniaXamlLoader.Load(this);
        StartupTrace.Mark("application-xaml.ready");
    }

    /// <summary>Creates the desktop main window when the framework lifetime is ready.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        StartupTrace.Mark("framework-initialization.started");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(StartupOptions, StartupTrace);
            StartupTrace.Mark("main-window.assigned");
        }

        base.OnFrameworkInitializationCompleted();
        StartupTrace.Mark("framework-initialization.completed");
    }
}
