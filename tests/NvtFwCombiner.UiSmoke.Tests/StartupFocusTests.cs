using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Startup focus and first keyboard navigation through the visible shell.</summary>
public sealed class StartupFocusTests
{
    /// <summary>Startup gives a named silent focus target; Tab reveals focus on Home.</summary>
    [AvaloniaTheory]
    [InlineData("Light", "English", "en-light")]
    [InlineData("Dark", "Traditional Chinese", "zh-dark")]
    public async Task StartupFocusIsNamedAndQuietUntilKeyboardNavigation(
        string theme, string language, string evidenceName)
    {
        using var workspace = TempWorkspace.Create("startup-focus");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, new ShellPreferenceSnapshot(theme, language))
        {
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Dispatcher.UIThread.RunJobs();
            ToggleButton home = window.FindControl<ToggleButton>("HomeNavigationButton")!;
            Assert.True(home.IsChecked);
            Capture(window, $"after-{evidenceName}");

            Grid focusTarget = window.FindControl<Grid>("ShellInteractionHost")!;
            Assert.Same(focusTarget, window.FocusManager?.GetFocusedElement());
            Assert.True(focusTarget.Focusable);
            Assert.False(KeyboardNavigation.GetIsTabStop(focusTarget));
            Assert.Equal("NVT FW Combiner", AutomationProperties.GetName(focusTarget));
            Assert.False(home.IsFocused);
            Assert.DoesNotContain(":focus-visible", home.Classes);

            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            Dispatcher.UIThread.RunJobs();
            Assert.Same(home, window.FocusManager?.GetFocusedElement());
            Assert.Contains(":focus-visible", home.Classes);
            Capture(window, $"after-tab-{evidenceName}");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        string root = Assert.IsType<string>(Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT"));
        string directory = Path.Combine(root, "evidence", "f114-startfocus");
        _ = Directory.CreateDirectory(directory);
        frame.SavePng(Path.Combine(directory, $"{name}.png"));
    }
}
