using Avalonia;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Starts the Avalonia desktop shell with an explicitly supplied dependency graph.</summary>
public static class DesktopApplication
{
    /// <summary>Gets the informational version shown by the desktop process.</summary>
    public static string InformationalVersion => ApplicationVersionProvider.InformationalVersion;

    /// <summary>Runs the desktop UI until its classic desktop lifetime exits.</summary>
    public static int Run(PresentationHostServices hostServices, string[] args)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        ArgumentNullException.ThrowIfNull(args);
        var startupTrace = StartupTraceSession.StartFromEnvironment();
        var launchOptions = UiLaunchOptions.Parse(args);
        startupTrace.Mark("launch-options.parsed");
        App.SetStartup(hostServices, launchOptions, startupTrace);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Creates the configured Avalonia application builder.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        App.StartupTrace.Mark("avalonia-builder.started");
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
        App.StartupTrace.Mark("avalonia-builder.ready");
        return builder;
    }
}
