using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The Settings surface owns focus entry, a cycle trap, Escape close and focus return.</summary>
    [AvaloniaFact]
    public async Task SettingsModalSupportsKeyboardModalLifecycle()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel());
        var launchButton = new Button { Content = "Settings" };
        var successorModalFocus = new Button { Content = "Successor modal" };
        var modal = new SettingsModal();
        var modalHost = new ContentControl { Content = modal };
        _ = modal.Bind(
            SettingsModal.IsOpenProperty,
            new Binding(nameof(MainWindowViewModel.IsSettingsModalOpen)));
        _ = modalHost.Bind(
            Visual.IsVisibleProperty,
            new Binding(nameof(MainWindowViewModel.IsSettingsModalOpen)));
        var window = new Window
        {
            DataContext = viewModel,
            Content = new Grid
            {
                Children = { launchButton, successorModalFocus, modalHost },
            },
        };
        try
        {
            window.Show();
            _ = launchButton.Focus(NavigationMethod.Tab);

            viewModel.OpenSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsSettingsModalOpen);
            Assert.True(modalHost.IsVisible);
            Assert.True(modal.IsOpen);
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(modal));
            Assert.NotSame(launchButton, window.FocusManager?.GetFocusedElement());

            modal.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
            });
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsSettingsModalOpen);
            Assert.Same(launchButton, window.FocusManager?.GetFocusedElement());

            viewModel.OpenSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(modal.IsOpen);
            Assert.NotSame(launchButton, window.FocusManager?.GetFocusedElement());

            viewModel.CloseSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(launchButton, window.FocusManager?.GetFocusedElement());

            viewModel.OpenSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            viewModel.IsNavigationClearConfirmationOpen = true;
            _ = successorModalFocus.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.IsSettingsModalOpen);
            Assert.Same(successorModalFocus, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The production host binds lifecycle to the canonical state and disables background interaction.</summary>
    [Fact]
    public void SettingsModalProductionHostOwnsLifecycleAndShellInertness()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string codeBehind = ReadPresentationFile("MainWindow.axaml.cs");
        string modal = ReadPresentationFile("Views/SettingsModal.axaml");

        Assert.Contains("<views:SettingsModal IsOpen=\"{Binding IsSettingsModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ApplyShellInteractionState(viewModel);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("!viewModel.IsSettingsModalOpen", codeBehind, StringComparison.Ordinal);
        Assert.Contains("shellInteractionHost.IsEnabled = interactive;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("shellInteractionHost.IsHitTestVisible = interactive;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding SettingsPreview.Title}\"", modal, StringComparison.Ordinal);
    }

    /// <summary>Launcher start is awaited from Closing, and Closed never performs a fire-and-forget handoff.</summary>
    [Fact]
    public void StableLauncherHandoffPrecedesTheFinalWindowClose()
    {
        string codeBehind = ReadPresentationFile("MainWindow.axaml.cs");
        int closing = codeBehind.IndexOf("protected override async void OnClosing", StringComparison.Ordinal);
        int handoff = codeBehind.IndexOf(
            "bool started = await TryCompleteStableLauncherHandoffAsync();",
            closing,
            StringComparison.Ordinal);
        int finalClose = codeBehind.IndexOf("Dispatcher.UIThread.Post(Close);", handoff, StringComparison.Ordinal);
        int closed = codeBehind.IndexOf("protected override void OnClosed", StringComparison.Ordinal);
        int dispose = codeBehind.IndexOf("public void Dispose()", closed, StringComparison.Ordinal);

        Assert.True(closing >= 0 && handoff > closing && finalClose > handoff);
        Assert.True(closed > finalClose && dispose > closed);
        Assert.DoesNotContain(
            "StableLauncherHandoff",
            codeBehind[closed..dispose],
            StringComparison.Ordinal);
    }

    /// <summary>Version status and destructive icons expose localized non-color-only accessible names.</summary>
    [Fact]
    public void SettingsVersionIconsUseBoundAccessibleNamesAndTooltips()
    {
        string versionPage = ReadPresentationFile("Resources/SettingsVersionPageTemplate.axaml");
        string sharedPages = ReadPresentationFile("Resources/MainWindowPageTemplates.axaml");

        Assert.Contains(
            "AutomationProperties.AccessibilityView=\"Content\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding Settings.SourceStatusText}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "ToolTip.Tip=\"{Binding Settings.SourceStatusText}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding DeleteActionLabel}\"",
            sharedPages,
            StringComparison.Ordinal);
        Assert.Contains(
            "ToolTip.Tip=\"{Binding DeleteActionLabel}\"",
            sharedPages,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AutomationProperties.Name=\"Delete installed version\"",
            sharedPages,
            StringComparison.Ordinal);
    }

    /// <summary>The source segment uses the approved ring and binds the shell motion preference.</summary>
    [Fact]
    public void VersionCheckingUsesDedicatedReducedMotionRingInsteadOfProgressBar()
    {
        string versionPage = ReadPresentationFile("Resources/SettingsVersionPageTemplate.axaml");

        Assert.Contains("<views:VersionCheckingIndicator", versionPage, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Settings.IsSourceChecking}\"", versionPage, StringComparison.Ordinal);
        Assert.Contains(
            "IsReducedMotionEnabled=\"{ReflectionBinding $parent[Window].DataContext.IsReducedMotionEnabled}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<ProgressBar", versionPage, StringComparison.Ordinal);
    }

    /// <summary>The compact ring renders in both themes and disables time-based motion when requested.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void VersionCheckingRingRendersForThemeAndMotionPreference(
        bool useDarkTheme,
        bool reducedMotion)
    {
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(Avalonia.Application.Current!.TryGetResource("NfcAccentBrush", theme, out object? indicator));
        Assert.True(Avalonia.Application.Current.TryGetResource("NfcAccentBorderBrush", theme, out object? track));
        var ring = new VersionCheckingIndicator
        {
            Width = 18,
            Height = 18,
            IndicatorBrush = Assert.IsType<IBrush>(indicator, exactMatch: false),
            TrackBrush = Assert.IsType<IBrush>(track, exactMatch: false),
            IsReducedMotionEnabled = reducedMotion,
        };
        var host = new Window
        {
            Width = 40,
            Height = 40,
            RequestedThemeVariant = theme,
            Content = ring,
        };
        try
        {
            host.Show();
            host.Measure(new Size(40, 40));
            host.Arrange(new Rect(0, 0, 40, 40));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Equal(
                !reducedMotion,
                VersionCheckingIndicator.ShouldAnimate(
                    isAttached: true,
                    isVisible: true,
                    reducedMotion));
            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
        }
        finally
        {
            host.Close();
        }
    }
}
