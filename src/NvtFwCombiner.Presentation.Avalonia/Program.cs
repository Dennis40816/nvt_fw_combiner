using Avalonia;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTraceSession startupTrace = StartupTraceSession.StartFromEnvironment();
        UiLaunchOptions launchOptions = UiLaunchOptions.Parse(args);
        startupTrace.Mark("launch-options.parsed");
        App.SetStartupOptions(launchOptions, startupTrace);
        _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

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
