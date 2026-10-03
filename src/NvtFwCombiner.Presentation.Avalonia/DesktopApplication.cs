using Avalonia;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Starts the Avalonia desktop shell with an explicitly supplied dependency graph.</summary>
public static class DesktopApplication
{
    /// <summary>Gets the informational version shown by the desktop process.</summary>
    public static string InformationalVersion => ApplicationVersionProvider.InformationalVersion;

    /// <summary>Creates the desktop dependency graph under startup tracing, then runs the UI.</summary>
    /// <param name="hostServicesFactory">Creates the host graph composed over the same local-state directory.</param>
    /// <param name="startupFiles">Bounded file adapter for startup work that precedes host composition.</param>
    /// <param name="localStateDirectory">Executable-composed directory that holds the shell preferences.</param>
    /// <param name="args">Remaining command-line arguments.</param>
    /// <param name="protectedInputs">Resolved local inputs consumed by the executable before UI parsing.</param>
    public static int Run(
        Func<PresentationHostServices> hostServicesFactory,
        ILocalFileStore startupFiles,
        string localStateDirectory,
        string[] args,
        IReadOnlyList<string>? protectedInputs = null)
    {
        ArgumentNullException.ThrowIfNull(hostServicesFactory);
        ArgumentNullException.ThrowIfNull(startupFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(localStateDirectory);
        ArgumentNullException.ThrowIfNull(args);
        UiLaunchOptions launchOptions;
        try
        {
            launchOptions = UiLaunchOptions.Parse(args, startupFiles, protectedInputs);
        }
        catch (Exception exception) when (UiLaunchOptions.RequestsScriptedCompletion(args))
        {
            Console.Error.WriteLine($"validation startup request '{string.Join(" ", args)}': {exception.Message}");
            return 70;
        }
        return DispatchLaunch(launchOptions, Console.Out, Console.Error, startupTrace =>
            RunValidatedDesktop(hostServicesFactory, startupFiles, localStateDirectory, args, launchOptions, startupTrace));
    }

    internal static int DispatchLaunch(UiLaunchOptions options, TextWriter output, TextWriter error,
        Func<StartupTraceSession, int> runDesktop)
    {
        // Public completion precedes tracing, preferences, composition and window creation.
        if (CompletePublicRequest(options, output, error) is { } terminal)
        {
            return terminal;
        }
        StartupTraceSession trace = options.CapturePath is null
            ? StartupTraceSession.StartFromEnvironment() : StartupTraceSession.Disabled;
        return runDesktop(trace);
    }

    private static int RunValidatedDesktop(
        Func<PresentationHostServices> hostServicesFactory, ILocalFileStore startupFiles,
        string localStateDirectory, string[] args, UiLaunchOptions launchOptions, StartupTraceSession startupTrace)
    {
        if (launchOptions.CapturePath is { } capturePath)
        {
            try
            {
                DesktopScreenshotCapture.RefuseLocalStateAliasesAsync(
                    startupFiles, capturePath, localStateDirectory, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Console.Error.WriteLine($"validation --capture '{capturePath}': {exception.Message}");
                return 64;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"validation --capture '{capturePath}': {exception.Message}");
                return 70;
            }
        }
        try
        {
            return RunDesktop(hostServicesFactory, startupFiles, localStateDirectory, args, launchOptions, startupTrace);
        }
        catch (Exception exception) when (launchOptions.CapturePath is not null)
        {
            Console.Error.WriteLine($"startup --capture '{launchOptions.CapturePath}'{DesktopScreenshotCapture.DescribeProtectedInputs(launchOptions.ProtectedInputs)}: {exception.Message}");
            return 70;
        }
    }

    internal static int? CompletePublicRequest(UiLaunchOptions options, TextWriter output, TextWriter error)
    {
        if (options.IsScriptedRequest && options.Issues.Count > 0)
        {
            foreach (string issue in options.Issues)
            {
                error.WriteLine($"validation: {issue}");
            }
            return 64;
        }
        if (options.Help)
        {
            output.WriteLine(UiLaunchOptions.Usage);
            return 0;
        }
        return null;
    }

    private static int RunDesktop(
        Func<PresentationHostServices> hostServicesFactory,
        ILocalFileStore startupFiles,
        string localStateDirectory,
        string[] args,
        UiLaunchOptions launchOptions,
        StartupTraceSession startupTrace)
    {
        startupTrace.Mark("launch-options.parsed");
        (PresentationHostServices hostServices, Task<ShellPreferenceSnapshot> shellPreferences) =
            PrepareStartup(
                hostServicesFactory,
                () => ShellPreferenceFileStore.LoadAsync(
                    startupFiles,
                    ShellPreferenceFileStore.PathIn(localStateDirectory)),
                startupTrace);
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
        Task<ShellPreferenceSnapshot> shellPreferences = Task.Run(
            () => shellPreferenceLoader() ?? throw new InvalidOperationException("The shell-preference loader returned null."));
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
