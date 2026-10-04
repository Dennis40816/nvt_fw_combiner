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

/// <summary>Real keyboard input stays in the active history deletion confirmation.</summary>
public sealed class ReportHistoryDeleteConfirmationModalTabTests
{
    /// <summary>Both deletion modes cycle the visible actions and retain safe entry and cancellation focus.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TabCyclesConfirmationActionsAndCancellationRestoresTrigger(bool clearAll, bool reverse)
    {
        using var workspace = TempWorkspace.Create("history-confirmation-tab");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "older"), "older.json");
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "current"), "current.json");
            IReadOnlyList<ReportHistorySnapshot> history = shell.Reports.ExportReportHistory();
            string report = shell.Reports.LoadedReportJson;
            ReportHistoryEntryViewModel entry = shell.Reports.ReportHistoryEntries[0];
            shell.Reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            ReportModal reportModal = Assert.Single(window.GetVisualDescendants().OfType<ReportModal>(),
                candidate => candidate.IsEffectivelyVisible);
            int reportTabEvents = 0;
            reportModal.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Tab && args.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
                {
                    reportTabEvents++;
                }
            };
            Button trigger = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button =>
                button.IsEffectivelyVisible && (clearAll
                    ? ReferenceEquals(button.Command, shell.Reports.ClearReportHistoryCommand)
                    : ReferenceEquals(button.Command, shell.Reports.RequestReportHistoryDeletionCommand) &&
                      ReferenceEquals(button.DataContext, entry)));

            for (int opening = 0; opening < 2; opening++)
            {
                Assert.True(trigger.Focus(NavigationMethod.Tab));
                Press(window, Key.Enter, PhysicalKey.Enter);
                Assert.True(shell.Reports.IsHistoryDeleteConfirmationOpen);
                Assert.Equal(clearAll, shell.Reports.IsClearAllHistoryDeletionPending);
                Assert.Same(clearAll ? null : entry, shell.Reports.PendingHistoryDeletion);
                ReportHistoryDeleteConfirmationModal modal = Assert.Single(
                    window.GetVisualDescendants().OfType<ReportHistoryDeleteConfirmationModal>(),
                    candidate => candidate.IsEffectivelyVisible);
                Button cancel = modal.FindControl<Button>("CancelButton")!;
                Button delete = modal.FindControl<Button>("DeleteButton")!;
                Assert.Same(cancel, window.FocusManager!.GetFocusedElement());

                bool? ctrlTabHandled = null;
                void ObserveCtrlTab(object? sender, KeyEventArgs args)
                {
                    ctrlTabHandled = args.Handled;
                }
                modal.AddHandler(InputElement.KeyDownEvent, ObserveCtrlTab, handledEventsToo: true);
                Press(window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Control);
                modal.RemoveHandler(InputElement.KeyDownEvent, ObserveCtrlTab);
                Assert.False(ctrlTabHandled);
                Assert.True(cancel.Focus(NavigationMethod.Tab));

                for (int step = 0; step < 6; step++)
                {
                    Press(window, Key.Tab, PhysicalKey.Tab, reverse ? RawInputModifiers.Shift : RawInputModifiers.None);
                    Assert.Same(step % 2 == 0 ? delete : cancel, window.FocusManager.GetFocusedElement());
                    Assert.Equal(0, reportTabEvents);
                }
                Press(window, Key.Tab, PhysicalKey.Tab, reverse ? RawInputModifiers.None : RawInputModifiers.Shift);
                Assert.Same(delete, window.FocusManager.GetFocusedElement());
                if (opening == 0)
                {
                    Press(window, Key.Escape, PhysicalKey.Escape);
                }
                else
                {
                    Press(window, Key.Tab, PhysicalKey.Tab);
                    Assert.Same(cancel, window.FocusManager.GetFocusedElement());
                    Press(window, Key.Space, PhysicalKey.Space);
                }

                Assert.False(shell.Reports.IsHistoryDeleteConfirmationOpen);
                Assert.True(shell.Reports.IsReportHistoryViewOpen);
                Assert.Same(trigger, window.FocusManager.GetFocusedElement());
                Assert.Equal(history, shell.Reports.ExportReportHistory());
                Assert.Equal(report, shell.Reports.LoadedReportJson);
            }
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static void Press(Window window, Key key, PhysicalKey physicalKey,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
        Dispatcher.UIThread.RunJobs();
    }
}
