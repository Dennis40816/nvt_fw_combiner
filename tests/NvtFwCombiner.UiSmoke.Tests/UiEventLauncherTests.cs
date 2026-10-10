using System.Reflection;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.DistributionLauncher;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Verifies that Launcher event entry points observe their own lifetime.</summary>
public sealed class UiEventLauncherTests
{
    /// <summary>A disposed owner does not make the Opened event escape before observation.</summary>
    [AvaloniaFact]
    public void WindowOpenedOwnerDisposedDoesNotEscape()
    {
        var window = new LauncherWindow();
        MethodInfo? opened = typeof(LauncherWindow).GetMethod(
            "Window_Opened",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(opened);
        window.Dispose();

        Exception? failure = Record.Exception(() => opened.Invoke(window, [null, EventArgs.Empty]));

        Assert.Null(failure);
    }
}
