using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.DistributionLauncher;

internal sealed partial class App : Avalonia.Application
{
    private static ManagedDistributionLauncherHostResult? _startup;
    private static string? _initialRoot;
    private static int _exitCode;

    internal static int ConfigureAndRun(
        ManagedDistributionLauncherHostResult startup,
        string initialRoot)
    {
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _initialRoot = Path.GetFullPath(initialRoot);
        _exitCode = (int)Program.MapExitCode(
            startup.PayloadIssue,
            startup.Entry?.Outcome,
            startup.Setup is not null);
        return Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime([]) == 0
            ? _exitCode
            : (int)DistributionLauncherExitCode.HostUnavailable;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new LauncherWindow(
                _startup ?? throw new InvalidOperationException("Launcher startup is unavailable."),
                _initialRoot ?? throw new InvalidOperationException("Launcher root is unavailable."),
                code =>
                {
                    _exitCode = code;
                    desktop.Shutdown();
                });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
