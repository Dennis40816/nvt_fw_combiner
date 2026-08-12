using Avalonia;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Starts the Avalonia desktop shell with an explicitly supplied dependency graph.</summary>
public static class DesktopApplication
{
    /// <summary>Gets the informational version shown by the desktop process.</summary>
    public static string InformationalVersion => ApplicationVersionProvider.InformationalVersion;

    /// <summary>Creates the desktop dependency graph under startup tracing, then runs the UI.</summary>
    public static int Run(
        Func<PresentationHostServices> hostServicesFactory,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(hostServicesFactory);
        ArgumentNullException.ThrowIfNull(args);
        var startupTrace = StartupTraceSession.StartFromEnvironment();
        startupTrace.Mark("host-services.started");
        PresentationHostServices hostServices = hostServicesFactory() ??
            throw new InvalidOperationException("The host-services factory returned null.");
        startupTrace.Mark("host-services.ready");
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
