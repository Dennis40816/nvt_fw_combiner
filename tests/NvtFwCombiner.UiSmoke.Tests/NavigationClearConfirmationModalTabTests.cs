using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Window keyboard input stays in the active confirmation and preserves its commands.</summary>
public sealed class NavigationClearConfirmationModalTabTests
{
    /// <summary>Both confirmations cycle in visual order, cancel safely, and retain their accept effects.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, Key.Escape)]
    [InlineData(true, false, Key.Escape)]
    [InlineData(false, false, Key.Enter)]
    [InlineData(true, false, Key.Enter)]
    [InlineData(false, false, Key.Space)]
    [InlineData(true, false, Key.Space)]
    [InlineData(false, true, Key.Enter)]
    [InlineData(true, true, Key.Enter)]
    public void OpenConfirmationCyclesAndClosingPreservesCommandEffects(bool exit, bool accept, Key closingKey)
    {
        ShellPage selectedPage = ShellPage.Merge;
        bool hasSelectedInputs = true;
        int clearCount = 0;
        int exitCount = 0;
        var navigation = new ShellNavigationViewModel(new ShellNavigationBindings(
            () => selectedPage,
            () => ShellTextResources.For(ShellLanguage.English),
            _ => hasSelectedInputs,
            static () => { },
            page =>
            {
                Assert.Equal(ShellPage.Merge, page);
                hasSelectedInputs = false;
                clearCount++;
            },
            page => selectedPage = page,
            static page => page.ToString(),
            static () => { }));
        var trigger = new Button
        {
            Content = "Open confirmation",
            Command = new RelayCommand(() =>
            {
                if (exit)
                {
                    navigation.RequestExitConfirmation(() => exitCount++);
                }
                else
                {
                    navigation.NavigateToPage(ShellPage.Home);
                }
            }),
        };
        var background = new Button { Content = "Background action" };
        var modal = new NavigationClearConfirmationModal { DataContext = navigation };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Grid
            {
                Children =
                {
                    new StackPanel { Children = { trigger, background } },
                    modal,
                },
            },
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.False(modal.IsEffectivelyVisible);
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Press(window, Key.Space);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            Button cancel = modal.FindControl<Button>("CancelButton")!;
            Button confirm = Assert.Single(modal.GetVisualDescendants().OfType<Button>(), button => button != cancel);
            Assert.True(navigation.IsNavigationClearConfirmationOpen);
            Assert.Equal(exit, navigation.IsExitConfirmationOpen);
            Assert.Same(cancel, window.FocusManager!.GetFocusedElement());
            Assert.True(cancel.TranslatePoint(default, modal)!.Value.X < confirm.TranslatePoint(default, modal)!.Value.X);

            foreach (RawInputModifiers modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    Press(window, Key.Tab, modifiers);
                    Assert.Same(confirm, window.FocusManager.GetFocusedElement());
                    Press(window, Key.Tab, modifiers);
                    Assert.Same(cancel, window.FocusManager.GetFocusedElement());
                }
            }

            if (accept)
            {
                Press(window, Key.Tab);
                Assert.Same(confirm, window.FocusManager.GetFocusedElement());
            }
            Press(window, closingKey);

            Assert.False(navigation.IsNavigationClearConfirmationOpen);
            Assert.False(navigation.IsExitConfirmationOpen);
            Assert.False(modal.IsEffectivelyVisible);
            Assert.Equal(accept && !exit ? ShellPage.Home : ShellPage.Merge, selectedPage);
            Assert.Equal(accept && !exit ? 1 : 0, clearCount);
            Assert.Equal(accept && exit ? 1 : 0, exitCount);
            Assert.Equal(!accept || exit, hasSelectedInputs);
            Assert.Same(trigger, window.FocusManager.GetFocusedElement());
            Press(window, Key.Tab);
            Assert.Same(background, window.FocusManager.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        PhysicalKey physicalKey = key == Key.Tab ? PhysicalKey.Tab :
            key == Key.Escape ? PhysicalKey.Escape :
            key == Key.Enter ? PhysicalKey.Enter :
            key == Key.Space ? PhysicalKey.Space :
            throw new ArgumentOutOfRangeException(nameof(key));
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
        Dispatcher.UIThread.RunJobs();
    }
}
