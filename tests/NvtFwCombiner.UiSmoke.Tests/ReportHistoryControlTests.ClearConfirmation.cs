using Avalonia.Automation;
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

public sealed partial class ReportHistoryControlTests
{
    /// <summary>Other modals disable clear all and notify its consumers; Report alone does not.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearAllAvailabilityTracksOtherModals(bool settings)
    {
        using var workspace = TempWorkspace.Create("history-clear-modal-availability");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            var availabilityChanges = new List<bool>();
            shell.Reports.ClearReportHistoryCommand.CanExecuteChanged += (_, _) =>
                availabilityChanges.Add(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
            Assert.True(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
            if (settings)
            {
                shell.OpenSettingsCommand.Execute(null);
                Assert.True(shell.IsSettingsModalOpen);
            }
            else
            {
                shell.MessageCenter.OpenCommand.Execute(null);
                Assert.True(shell.MessageCenter.IsOpen);
            }
            Assert.False(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
            Assert.Contains(false, availabilityChanges);
            availabilityChanges.Clear();
            if (settings)
            {
                shell.CloseSettingsCommand.Execute(null);
                Assert.False(shell.IsSettingsModalOpen);
            }
            else
            {
                shell.MessageCenter.CloseCommand.Execute(null);
                Assert.False(shell.MessageCenter.IsOpen);
            }
            Assert.True(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
            Assert.Contains(true, availabilityChanges);
            shell.Reports.ShowReportHistoryCommand.Execute(null);
            Assert.True(shell.Reports.IsReportModalOpen);
            Assert.True(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
            // A Report modal must not hide another open modal from the availability check.
            shell.MessageCenter.OpenCommand.Execute(null);
            Assert.True(shell.Reports.IsReportModalOpen);
            Assert.True(shell.MessageCenter.IsOpen);
            Assert.False(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Every confirmation exit restores the shell control that opened it by shortcut.</summary>
    [AvaloniaTheory]
    [InlineData("Cancel")]
    [InlineData("Escape")]
    [InlineData("Clear all")]
    public async Task ClearHistoryShortcutRestoresShellFocus(string exit)
    {
        using var workspace = TempWorkspace.Create("history-clear-shortcut-focus");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            IReadOnlyList<ReportHistorySnapshot> expected = shell.Reports.ExportReportHistory();
            Button trigger = window.FindControl<Button>("HomeNavigationButton")!;
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Assert.True(trigger.IsFocused);
            PressClearShortcut(window);
            AssertClearAllConfirmation(window, shell.Reports, chinese: false);
            Assert.True(ConfirmationButton(window, "Cancel").IsFocused);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
            if (exit == "Escape")
            {
                PressEscape(window);
            }
            else
            {
                Activate(window, ConfirmationButton(window, exit), "Space");
            }
            Assert.False(shell.Reports.IsHistoryDeleteConfirmationOpen);
            if (exit == "Clear all")
            {
                Assert.Empty(shell.Reports.ReportHistoryEntries);
            }
            else
            {
                Assert.Equal(expected, shell.Reports.ExportReportHistory());
            }
            Assert.True(trigger.IsFocused);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Cancel and Escape preserve complete history, its persistence, and the current report.</summary>
    [AvaloniaTheory]
    [InlineData(false, "button", false)]
    [InlineData(true, "button", false)]
    [InlineData(false, "Escape", false)]
    [InlineData(true, "Escape", false)]
    [InlineData(false, "button", true)]
    [InlineData(true, "button", true)]
    [InlineData(false, "Escape", true)]
    [InlineData(true, "Escape", true)]
    public async Task ClearAllCancellationKeepsHistoryAndRestoresFocus(bool chinese, string cancellation, bool warningEntry)
    {
        using var workspace = TempWorkspace.Create("history-clear-cancel");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        window.Show();
        IReadOnlyList<ReportHistorySnapshot> expected;
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            if (warningEntry)
            {
                shell.Reports.LoadReportJson(
                    ReportJsonSamples.ReplaceWithManyOutputDifferences(count: 5_000, sectionCount: 40),
                    "large-history.json");
                Assert.True(shell.Reports.HasReportHistoryStorageWarning);
            }
            expected = shell.Reports.ExportReportHistory();
            string current = shell.Reports.LoadedReportJson;
            shell.Reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Button clear = warningEntry
                ? Assert.Single(window.GetVisualDescendants().OfType<Button>(), button =>
                    button.IsEffectivelyVisible && ReferenceEquals(button.Command, shell.Reports.ClearReportHistoryCommand) &&
                    Equals(button.Content, shell.Text.ClearHistoryLabel))
                : ClearHistoryButton(window, shell.Reports);
            Activate(window, clear, "mouse");
            AssertClearAllConfirmation(window, shell.Reports, chinese);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
            Assert.True(ConfirmationButton(window, chinese ? "取消" : "Cancel").IsFocused);
            shell.Reports.RequestReportHistoryDeletionCommand.Execute(shell.Reports.ReportHistoryEntries[0]);
            Assert.Null(shell.Reports.PendingHistoryDeletion);
            if (cancellation == "Escape")
            {
                PressEscape(window);
            }
            else
            {
                Activate(window, ConfirmationButton(window, chinese ? "取消" : "Cancel"), "Space");
            }
            Assert.False(shell.Reports.IsHistoryDeleteConfirmationOpen);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
            Assert.Equal(current, shell.Reports.LoadedReportJson);
            Assert.True(shell.Reports.IsReportHistoryViewOpen);
            Assert.True(clear.IsFocused);
            shell.Reports.ConfirmReportHistoryDeletionCommand.Execute(null);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
        IReadOnlyList<ReportHistorySnapshot> persisted = await ReportHistoryFileStore.LoadAsync(
            services.LocalFiles, ReportHistoryFileStore.PathIn(services.LocalStateDirectory), TestContext.Current.CancellationToken);
        Assert.Equal(expected.Select(entry => (entry.SourceName, entry.ReportJson, entry.OutputArtifactPath, entry.Metadata)),
            persisted.Select(entry => (entry.SourceName, entry.ReportJson, entry.OutputArtifactPath, entry.Metadata)));
    }

    /// <summary>Only confirmation clears all persisted entries; the loaded report remains available.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearAllConfirmationClearsHistoryAndPersists(bool chinese)
    {
        using var workspace = TempWorkspace.Create("history-clear-confirm");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            string current = shell.Reports.LoadedReportJson;
            shell.Reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Activate(window, ClearHistoryButton(window, shell.Reports), "Enter");
            AssertClearAllConfirmation(window, shell.Reports, chinese);
            Assert.Equal(2, shell.Reports.ReportHistoryCount);
            Button confirm = ConfirmationButton(window, chinese ? "全部清除" : "Clear all");
            Assert.Contains("danger", confirm.Classes);
            Activate(window, confirm, "Enter");
            Assert.False(shell.Reports.IsHistoryDeleteConfirmationOpen);
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            Assert.Equal(current, shell.Reports.LoadedReportJson);
            Assert.True(shell.Reports.HasLoadedReport);
            Assert.False(shell.Reports.ClearReportHistoryCommand.CanExecute(null));
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
        Assert.Empty(await ReportHistoryFileStore.LoadAsync(
            services.LocalFiles, ReportHistoryFileStore.PathIn(services.LocalStateDirectory), TestContext.Current.CancellationToken));
    }

    /// <summary>The real shortcut opens the same visible confirmation from the shell or history.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearHistoryShortcutOpensConfirmationInsteadOfClearing(bool historyOpen)
    {
        using var workspace = TempWorkspace.Create("history-clear-shortcut");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            IReadOnlyList<ReportHistorySnapshot> expected = shell.Reports.ExportReportHistory();
            if (historyOpen)
            {
                shell.Reports.ShowReportHistoryCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(ClearHistoryButton(window, shell.Reports).Focus(NavigationMethod.Tab));
            }
            else
            {
                Assert.True(window.FindControl<Grid>("ShellInteractionHost")!.Focus());
            }
            PressClearShortcut(window);
            AssertClearAllConfirmation(window, shell.Reports, chinese: false);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
            Assert.True(ConfirmationButton(window, "Cancel").IsFocused);
            PressClearShortcut(window);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            Assert.True(ConfirmationButton(window, "Clear all").IsFocused);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            Assert.True(ConfirmationButton(window, "Cancel").IsFocused);
            PressEscape(window);
            Assert.False(shell.Reports.IsHistoryDeleteConfirmationOpen);
            Assert.Equal(expected, shell.Reports.ExportReportHistory());
            PressClearShortcut(window);
            AssertClearAllConfirmation(window, shell.Reports, chinese: false);
            Activate(window, ConfirmationButton(window, "Clear all"), "Space");
            Assert.Empty(shell.Reports.ReportHistoryEntries);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Changing language refreshes the open confirmation without changing its pending action.</summary>
    [AvaloniaFact]
    public async Task ClearAllConfirmationRelocalizesWhileOpen()
    {
        using var workspace = TempWorkspace.Create("history-clear-language");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            shell.Reports.ClearReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            AssertClearAllConfirmation(window, shell.Reports, chinese: false);
            shell.SelectedLanguage = "Traditional Chinese";
            Dispatcher.UIThread.RunJobs();
            AssertClearAllConfirmation(window, shell.Reports, chinese: true);
            shell.SelectedLanguage = "English";
            Dispatcher.UIThread.RunJobs();
            AssertClearAllConfirmation(window, shell.Reports, chinese: false);
            PressEscape(window);
            Assert.Equal(2, shell.Reports.ReportHistoryCount);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static Button ClearHistoryButton(Window window, ReportPresentationViewModel reports)
    {
        return Assert.Single(window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && ReferenceEquals(button.Command, reports.ClearReportHistoryCommand));
    }

    private static void AssertClearAllConfirmation(Window window, ReportPresentationViewModel reports, bool chinese)
    {
        Assert.True(reports.IsReportModalOpen);
        Assert.True(reports.IsHistoryDeleteConfirmationOpen);
        ReportHistoryDeleteConfirmationModal modal = Assert.Single(window.GetVisualDescendants().OfType<ReportHistoryDeleteConfirmationModal>(),
            control => control.IsEffectivelyVisible);
        string title = chinese ? "清除全部報告？" : "Clear all reports?";
        string detail = chinese ? "只移除全部歷史紀錄，不會刪除報告檔或輸出檔案。"
            : "This removes all history entries. Report files and output files will not be deleted.";
        Assert.Equal(title, AutomationProperties.GetName(modal));
        Assert.Contains(modal.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == title);
        Assert.Contains(modal.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == detail);
        Assert.DoesNotContain(modal.GetVisualDescendants().OfType<Border>(), border => border.IsEffectivelyVisible && border.Classes.Contains("insetSurface"));
    }

    private static void PressClearShortcut(Window window)
    {
        const RawInputModifiers modifiers = RawInputModifiers.Control | RawInputModifiers.Shift;
        window.KeyPress(Key.Delete, modifiers, PhysicalKey.Delete, "");
        window.KeyRelease(Key.Delete, modifiers, PhysicalKey.Delete, "");
        Dispatcher.UIThread.RunJobs();
    }

    private static void PressEscape(Window window)
    {
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
        Dispatcher.UIThread.RunJobs();
    }
}
