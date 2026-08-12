using Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

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
        (PresentationHostServices hostServices, Task<ShellPreferenceSnapshot> shellPreferences) =
            PrepareStartup(
                hostServicesFactory,
                static () => ShellPreferenceFileStore.LoadAsync(
                    ShellPreferenceFileStore.DefaultPreferencesPath),
                startupTrace);
        var launchOptions = UiLaunchOptions.Parse(args);
        startupTrace.Mark("launch-options.parsed");
        App.SetStartup(hostServices, launchOptions, startupTrace, shellPreferences);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    internal static (
        PresentationHostServices HostServices,
        Task<ShellPreferenceSnapshot> ShellPreferences) PrepareStartup(
        Func<PresentationHostServices> hostServicesFactory,
        Func<Task<ShellPreferenceSnapshot>> shellPreferenceLoader,
        StartupTraceSession startupTrace)
    {
        ArgumentNullException.ThrowIfNull(hostServicesFactory);
        ArgumentNullException.ThrowIfNull(shellPreferenceLoader);
        ArgumentNullException.ThrowIfNull(startupTrace);

        startupTrace.Mark("shell-preferences.started");
        Task<ShellPreferenceSnapshot> shellPreferences = shellPreferenceLoader() ??
            throw new InvalidOperationException("The shell-preference loader returned null.");
        startupTrace.Mark("host-services.started");
        PresentationHostServices hostServices = hostServicesFactory() ??
            throw new InvalidOperationException("The host-services factory returned null.");
        startupTrace.Mark("host-services.ready");
        return (hostServices, shellPreferences);
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
