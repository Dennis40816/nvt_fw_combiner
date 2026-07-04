using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Avalonia application bootstrapper.</summary>
public sealed partial class App : global::Avalonia.Application
{
    /// <summary>Gets UI startup state parsed by the process entry point.</summary>
    internal static UiLaunchOptions StartupOptions { get; private set; } = UiLaunchOptions.Empty;

    /// <summary>Sets UI startup state before the framework creates the main window.</summary>
    internal static void SetStartupOptions(UiLaunchOptions startupOptions)
    {
        ArgumentNullException.ThrowIfNull(startupOptions);

        StartupOptions = startupOptions;
    }

    /// <summary>Loads the compiled Avalonia XAML for the application.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Creates the desktop main window when the framework lifetime is ready.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(StartupOptions);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
