using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Avalonia application bootstrapper.</summary>
public sealed partial class App : global::Avalonia.Application
{
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
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
