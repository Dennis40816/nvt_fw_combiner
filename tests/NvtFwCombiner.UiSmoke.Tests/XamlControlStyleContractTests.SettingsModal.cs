using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
}
