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

/// <summary>Real keyboard navigation stays within the active workflow context draft.</summary>
public sealed class WorkflowContextModalTabTests
{
    /// <summary>Tab follows visible fields and actions in both directions, including after reopening.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TabCyclesVisibleControlsAndSkipsBackgroundOnOpenAndReopen(bool showNumber)
    {
        using var workspace = TempWorkspace.Create("workflow-context-tab");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            Control background = window.FindControl<Control>("HomeNavigationButton")!;
            Assert.True(background.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            Dispatcher.UIThread.RunJobs();
            IInputElement? next = window.FocusManager?.GetFocusedElement();
            Assert.NotSame(background, next);
            WorkflowContextSetupModal? retainedModal = null;
            foreach (bool numberVisible in new[] { showNumber, !showNumber })
            {
                Assert.True(background.Focus(NavigationMethod.Tab));
                (numberVisible ? shell.BeginCtrlRamReplaceFromHomeCommand : shell.BeginNormalMergeFromHomeCommand)
                    .Execute(null);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.True(shell.WorkflowSession.IsWorkflowContextModalOpen);
                WorkflowContextSetupModal modal = Assert.Single(window.GetVisualDescendants()
                    .OfType<WorkflowContextSetupModal>());
                if (retainedModal is not null)
                {
                    Assert.Same(retainedModal, modal);
                }
                retainedModal = modal;
                ComboBox ic = Assert.Single(modal.GetVisualDescendants().OfType<ComboBox>(),
                    control => ReferenceEquals(control.ItemsSource, shell.WorkflowSession.WorkflowContextSetup.IcChoices));
                ComboBox number = Assert.Single(modal.GetVisualDescendants().OfType<ComboBox>(),
                    control => ReferenceEquals(control.ItemsSource, shell.WorkflowSession.WorkflowContextSetup.NumberChoices));
                Button cancel = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                    control => ReferenceEquals(control.Command, shell.WorkflowSession.CancelWorkflowContextCommand));
                Button confirm = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                    control => ReferenceEquals(control.Command, shell.WorkflowSession.ConfirmWorkflowContextCommand));

                Assert.Equal(numberVisible, number.IsEffectivelyVisible);
                Assert.True(confirm.IsEffectivelyEnabled);
                Assert.Same(cancel, window.FocusManager?.GetFocusedElement());
                Control[] sequence = numberVisible ? [ic, number, cancel, confirm] : [ic, cancel, confirm];
                int position = Array.IndexOf(sequence, cancel);
                foreach (RawInputModifiers modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
                {
                    for (int step = 0; step < sequence.Length * 2; step++)
                    {
                        window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
                        window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
                        Dispatcher.UIThread.RunJobs();
                        position = (position + (modifiers == RawInputModifiers.Shift ? -1 : 1) + sequence.Length)
                            % sequence.Length;
                        Assert.Same(sequence[position], window.FocusManager?.GetFocusedElement());
                        Assert.False(background.IsFocused);
                    }
                }

                shell.WorkflowSession.CancelWorkflowContextCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.False(modal.IsEffectivelyVisible);
                Assert.Same(background, window.FocusManager?.GetFocusedElement());
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
                Dispatcher.UIThread.RunJobs();
                Control focused = Assert.IsType<Control>(window.FocusManager?.GetFocusedElement(), exactMatch: false);
                Assert.Same(next, focused);
                Assert.True(focused.IsEffectivelyVisible);
                Assert.DoesNotContain(modal, focused.GetVisualAncestors());
            }
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }
}
