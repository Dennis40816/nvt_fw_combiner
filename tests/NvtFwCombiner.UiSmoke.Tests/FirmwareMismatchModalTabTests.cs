using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Firmware mismatch prompts keep keyboard traversal within their existing actions.</summary>
public sealed class FirmwareMismatchModalTabTests
{
    /// <summary>Actual Tab and Shift+Tab follow the visible action order and wrap within the modal.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TabCyclesThroughActionsWithoutReachingBackgroundWorkflow(bool numberMismatch)
    {
        using var workspace = TempWorkspace.Create("firmware-mismatch-tab");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            shell.ShowMergeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.IsMergeVisible);
            OpenModal(shell.WorkflowSession, numberMismatch);
            Dispatcher.UIThread.RunJobs();
            UserControl modal = FindModal(window, numberMismatch);
            Button[] actions = [.. modal.GetVisualDescendants().OfType<Button>()];
            Assert.Equal(2, actions.Length);
            Assert.Same(numberMismatch
                ? shell.WorkflowSession.DismissFirmwareNumberMismatchCommand
                : shell.WorkflowSession.DismissFirmwareIcMismatchCommand, actions[0].Command);
            Assert.Same(numberMismatch
                ? shell.WorkflowSession.AcceptFirmwareNumberMismatchCommand
                : shell.WorkflowSession.AcceptFirmwareIcMismatchCommand, actions[1].Command);
            Assert.True(actions[0].TranslatePoint(default, window)!.Value.X < actions[1].TranslatePoint(default, window)!.Value.X);
            Assert.True(actions[0].Focus(NavigationMethod.Tab));
            string selectedIc = shell.WorkflowSession.SelectedIc;
            string selectedNumber = shell.WorkflowSession.SelectedNumber;

            foreach (RawInputModifiers modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    foreach (Button expected in new[] { actions[1], actions[0] })
                    {
                        window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
                        window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
                        Dispatcher.UIThread.RunJobs();
                        Assert.Same(expected, window.FocusManager!.GetFocusedElement());
                        Assert.True(modal.IsEffectivelyVisible);
                    }
                }
            }

            Assert.Equal(selectedIc, shell.WorkflowSession.SelectedIc);
            Assert.Equal(selectedNumber, shell.WorkflowSession.SelectedNumber);
            DismissModal(shell.WorkflowSession, numberMismatch);
            Dispatcher.UIThread.RunJobs();
            Assert.False(modal.IsEffectivelyVisible);
            Assert.True(shell.IsMergeVisible);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>First opening and reopening the retained view focus the safe action from the workflow.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningAndReopeningFocusTheSafeAction(bool numberMismatch)
    {
        using var workspace = TempWorkspace.Create("firmware-mismatch-initial-focus");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            shell.ShowMergeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Control background = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible && button.IsEffectivelyEnabled &&
                    ReferenceEquals(button.Command, shell.OpenSettingsCommand));
            Assert.True(background.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            IInputElement? next = window.FocusManager!.GetFocusedElement();
            Assert.NotSame(background, next);
            UserControl? retainedModal = null;
            for (int opening = 0; opening < 2; opening++)
            {
                Assert.True(background.Focus(NavigationMethod.Tab));
                OpenModal(shell.WorkflowSession, numberMismatch);
                Dispatcher.UIThread.RunJobs();
                UserControl modal = FindModal(window, numberMismatch);
                if (retainedModal is not null)
                {
                    Assert.Same(retainedModal, modal);
                }
                retainedModal = modal;
                Button cancel = Assert.Single(modal.GetVisualDescendants().OfType<Button>(), button =>
                    ReferenceEquals(button.Command, numberMismatch
                        ? shell.WorkflowSession.DismissFirmwareNumberMismatchCommand
                        : shell.WorkflowSession.DismissFirmwareIcMismatchCommand));
                Assert.Same(cancel, window.FocusManager!.GetFocusedElement());
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(modal.IsKeyboardFocusWithin);
                DismissModal(shell.WorkflowSession, numberMismatch);
                Dispatcher.UIThread.RunJobs();
                Assert.False(modal.IsEffectivelyVisible);
                Assert.Same(background, window.FocusManager.GetFocusedElement());
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(next, window.FocusManager.GetFocusedElement());
            }
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static UserControl FindModal(Window window, bool numberMismatch)
    {
        return numberMismatch
            ? Assert.Single(window.GetVisualDescendants().OfType<FirmwareNumberMismatchModal>())
            : Assert.Single(window.GetVisualDescendants().OfType<FirmwareIcMismatchModal>());
    }

    private static void OpenModal(WorkflowSessionPresentationViewModel workflow, bool numberMismatch)
    {
        if (numberMismatch) { workflow.IsFirmwareNumberMismatchModalOpen = true; }
        else { workflow.IsFirmwareIcMismatchModalOpen = true; }
    }

    private static void DismissModal(WorkflowSessionPresentationViewModel workflow, bool numberMismatch)
    {
        if (numberMismatch) { workflow.DismissFirmwareNumberMismatchCommand.Execute(null); }
        else { workflow.DismissFirmwareIcMismatchCommand.Execute(null); }
    }
}
