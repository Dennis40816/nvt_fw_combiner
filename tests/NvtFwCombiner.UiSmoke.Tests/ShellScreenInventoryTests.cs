using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Repeatable current-shell inventory, not native DPI or whole-app visual certification.</summary>
public sealed class ShellScreenInventoryTests
{
    /// <summary>Settings sections keep their real shell host and leave Home workflow state untouched.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HomeAndSettingsSectionsRemainReachableWithoutWorkflowMutation(bool dark)
    {
        using var workspace = TempWorkspace.Create("shell-screen-inventory");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            shell.SelectedLanguage = "English";
            shell.SelectedTheme = dark ? "Dark" : "Light";
            shell.ShowHomeCommand.Execute(null);
            Assert.True(shell.IsHomeVisible);
            Capture(window, "home", dark);
            string navigation = shell.Navigation.NavigationPath;
            string ic = shell.WorkflowSession.SelectedIc;
            shell.OpenSettingsCommand.Execute(null);
            foreach (SettingsSection section in new[]
            {
                SettingsSection.Preferences, SettingsSection.Overview, SettingsSection.SupportMatrix,
            })
            {
                shell.Settings.SelectSectionCommand.Execute(section);
                Dispatcher.UIThread.RunJobs();
                Assert.True(shell.IsSettingsModalOpen);
                Assert.Equal(section, shell.Settings.SelectedSection);
                Assert.Equal(navigation, shell.Navigation.NavigationPath);
                Assert.Equal(ic, shell.WorkflowSession.SelectedIc);
                Assert.False(shell.Navigation.IsNavigationClearConfirmationOpen);
                Border surface = Assert.Single(window.GetVisualDescendants().OfType<Border>(),
                    item => item.Name == "SettingsSurface");
                Assert.True(surface.IsEffectivelyVisible);
                Point origin = surface.TranslatePoint(default, window)!.Value;
                Assert.InRange(origin.X, 0, 1440 - surface.Bounds.Width);
                Assert.InRange(origin.Y, 0, 900 - surface.Bounds.Height);
                Capture(window, section.ToString(), dark);
            }
            shell.CloseSettingsCommand.Execute(null);
            Assert.False(shell.IsSettingsModalOpen);
            Assert.True(shell.IsHomeVisible);
            Assert.Equal(navigation, shell.Navigation.NavigationPath);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static void Capture(Window window, string surface, bool dark)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(1, window.RenderScaling);
        Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
        Assert.Equal(new PixelSize(1440, 900), frame.PixelSize);
        TestContext.Current.TestOutputHelper!.WriteLine(
            $"{surface}: theme={window.ActualThemeVariant}; language=English; scale={window.RenderScaling}; pixels={frame.PixelSize}");
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, $"inventory-{surface}-{(dark ? "dark" : "light")}.png"));
        }
    }
}
