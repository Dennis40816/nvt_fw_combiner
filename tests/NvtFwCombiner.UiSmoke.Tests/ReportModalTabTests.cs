using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Tab follows the active Report surface and leaves nested confirmation to its owner.</summary>
public sealed class ReportModalTabTests
{
    /// <summary>Return focus respects opener lifetime and later focus owners, including detachment.</summary>
    [AvaloniaTheory]
    [InlineData("detach-modal", true)]
    [InlineData("hidden-opener", false)]
    [InlineData("disabled-opener", false)]
    [InlineData("detached-opener", false)]
    [InlineData("different-window", false)]
    [InlineData("other-control", false)]
    [InlineData("other-modal", false)]
    [InlineData("cleared-other-focus", false)]
    [InlineData("closed-window", false)]
    public void ClosingOrDetachingOnlyRestoresAnAvailableOpenerWhenFocusIsStillOwned(string change, bool restores)
    {
        ReportPresentationViewModel reports = CreateReports();
        reports.LoadReportJson(ReportJsonSamples.Succeeded(), "report.json");
        var modal = new ReportModal { DataContext = reports };
        var modalHost = new ContentControl { Content = modal, DataContext = reports };
        using IDisposable visibility = modalHost.Bind(UserControl.IsVisibleProperty,
            new Binding(nameof(reports.IsReportModalOpen)));
        var before = new Button { Content = "Before Report" };
        var after = new Button { Content = "After Report" };
        var host = new StackPanel { Children = { before, modalHost, after } };
        var window = new Window
        {
            Width = 1600,
            Height = 1000,
            DataContext = new { Reports = reports, reports.Text, MessageCenter = new { OpenRunReportsCommand = reports.CloseReportCommand } },
            Content = host,
        };
        var otherWindow = new Window();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.True(before.Focus(NavigationMethod.Tab));
            int returns = 0;
            before.GotFocus += (_, _) => returns++;
            reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(modal.IsKeyboardFocusWithin);
            Control? laterFocus = null;
            switch (change)
            {
                case "detach-modal": Assert.True(host.Children.Remove(modalHost)); break;
                case "hidden-opener": before.IsVisible = false; break;
                case "disabled-opener": before.IsEnabled = false; break;
                case "detached-opener": Assert.True(host.Children.Remove(before)); break;
                case "different-window":
                    Assert.True(host.Children.Remove(before));
                    otherWindow.Content = before;
                    otherWindow.Show();
                    break;
                case "other-control":
                case "cleared-other-focus":
                    Assert.True(after.Focus(NavigationMethod.Tab));
                    if (change == "cleared-other-focus") { Assert.True(host.Children.Remove(after)); }
                    else { laterFocus = after; }
                    break;
                case "other-modal":
                    ReportPresentationViewModel otherReports = CreateReports();
                    otherReports.LoadReportJson(ReportJsonSamples.Succeeded(), "other-report.json");
                    var otherModal = new ReportModal { DataContext = otherReports };
                    host.Children.Add(otherModal);
                    otherReports.ShowReportHistoryCommand.Execute(null);
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(otherModal.IsKeyboardFocusWithin);
                    laterFocus = Assert.IsType<Control>(window.FocusManager?.GetFocusedElement(), exactMatch: false);
                    break;
                case "closed-window": window.Close(); break;
                default: throw new ArgumentOutOfRangeException(nameof(change), change, "Unknown focus scenario.");
            }
            if (change != "detach-modal") { reports.CloseReportCommand.Execute(null); }
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(restores ? 1 : 0, returns);
            if (restores) { Assert.Same(before, window.FocusManager?.GetFocusedElement()); }
            else { Assert.NotSame(before, window.FocusManager?.GetFocusedElement()); }
            if (laterFocus is not null) { Assert.Same(laterFocus, window.FocusManager?.GetFocusedElement()); }
        }
        finally
        {
            otherWindow.Close();
            window.Close();
        }
    }

    /// <summary>Opening Report from the background enters safely and Close resumes its Tab sequence.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpeningAndClosingRestoresBackgroundTabSequence(bool history)
    {
        ReportPresentationViewModel reports = CreateReports();
        reports.LoadReportJson(ReportJsonSamples.Succeeded(), "report.json");
        var modal = new ReportModal { DataContext = reports };
        var modalHost = new ContentControl { Content = modal, DataContext = reports };
        using IDisposable visibility = modalHost.Bind(UserControl.IsVisibleProperty,
            new Binding(nameof(reports.IsReportModalOpen)));
        var before = new Button { Content = "Before Report" };
        var after = new Button { Content = "After Report" };
        var window = new Window
        {
            Width = 1600,
            Height = 1000,
            DataContext = new { Reports = reports, reports.Text, MessageCenter = new { OpenRunReportsCommand = reports.CloseReportCommand } },
            Content = new StackPanel { Children = { before, modalHost, after } },
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            for (int opening = 0; opening < 2; opening++)
            {
                Assert.True(before.Focus(NavigationMethod.Tab));
                (history ? reports.ShowReportHistoryCommand : reports.ShowReportCommand).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(FooterButtons(modal, reports)[0], window.FocusManager?.GetFocusedElement());
                PressTab(window, shift: false);
                Assert.True(modal.IsKeyboardFocusWithin);
                reports.CloseReportCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(before, window.FocusManager?.GetFocusedElement());
                PressTab(window, shift: false);
                Assert.Same(after, window.FocusManager?.GetFocusedElement());
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Each existing review tab exposes only its current content between the header and footer.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ReviewTabCyclesThroughVisibleContentInVisualOrder(int selectedTab)
    {
        ReportPresentationViewModel reports = CreateReports();
        string json = selectedTab switch
        {
            0 => ReportJsonSamples.CtrlRamInputs(),
            2 or 3 => ReportJsonSamples.CtrlRamCommandTrace(2),
            4 => ReportJsonSamples.CtrlRamWarning(),
            _ => ReportJsonSamples.Succeeded(),
        };
        reports.LoadReportJson(json, "tab-report.json");
        reports.ShowReportCommand.Execute(null);
        var modal = new ReportModal { DataContext = reports };
        Window window = ShowReport(modal, reports);
        try
        {
            TabControl tabs = Assert.Single(modal.GetVisualDescendants().OfType<TabControl>());
            Assert.Equal(6, tabs.Items.Count);
            tabs.SelectedIndex = selectedTab;
            Dispatcher.UIThread.RunJobs();
            TabItem selected = Assert.IsType<TabItem>(tabs.SelectedItem);
            Control content = Assert.IsType<Control>(selected.Content, exactMatch: false);
            Assert.True(content.IsEffectivelyVisible);
            string[] headings = [reports.Text.InputsAndHashesTitle, reports.Text.EmptyByteChangesMessage,
                reports.Text.OperationStepsTitle, reports.Text.HeaderRefreshTraceTitle,
                reports.Text.IssuesAndWarningsTitle, reports.Text.RawReportTitle];
            Assert.Contains(content.GetVisualDescendants().OfType<Control>(), control =>
                control.IsEffectivelyVisible && ((control is TextBlock text && text.Text == headings[selectedTab]) ||
                    AutomationProperties.GetName(control) == headings[selectedTab]));

            Button[] header = HeaderButtons(modal);
            ToggleButton summary = Assert.Single(modal.GetVisualDescendants().OfType<Expander>()
                .Where(expander => !expander.GetVisualAncestors().Contains(tabs))
                .SelectMany(expander => expander.GetVisualDescendants().OfType<ToggleButton>()));
            Control[] contentStops = [.. content.GetVisualDescendants().Prepend(content).OfType<Control>()
                .Where(control => control.Focusable && KeyboardNavigation.GetIsTabStop(control) &&
                    control.IsEffectivelyVisible && control.IsEffectivelyEnabled)];
            if (selectedTab == 5)
            {
                Assert.Collection(contentStops,
                    control => Assert.Equal(json, Assert.IsType<TextBox>(control).Text),
                    control => Assert.Equal("RawReportCopyButton", Assert.IsType<Button>(control).Name));
            }
            AssertTabCycle(window, [.. header, summary, selected, .. contentStops, .. FooterButtons(modal, reports)]);
            Assert.Equal(selectedTab, tabs.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>History traverses its visible actions, each existing row and delete action, then the footer.</summary>
    [AvaloniaFact]
    public void HistoryTabCyclesThroughExistingRowsAndFooterInVisualOrder()
    {
        ReportPresentationViewModel reports = CreateReports();
        reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "older"), "older.json");
        reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "newer"), "newer.json");
        reports.ShowReportHistoryCommand.Execute(null);
        var modal = new ReportModal { DataContext = reports };
        Window window = ShowReport(modal, reports);
        try
        {
            Button back = VisibleButton(modal, button => ReferenceEquals(button.Command, reports.CloseReportHistoryCommand));
            Button clear = VisibleButton(modal, button => ReferenceEquals(button.Command, reports.ClearReportHistoryCommand));
            List<Control> sequence = [.. HeaderButtons(modal), back, clear];
            foreach (ReportHistoryEntryViewModel entry in reports.ReportHistoryEntries)
            {
                sequence.Add(VisibleButton(modal, button => ReferenceEquals(button.DataContext, entry) && button.Classes.Contains("reportListRow")));
                sequence.Add(VisibleButton(modal, button => ReferenceEquals(button.DataContext, entry) && button.Classes.Contains("danger")));
            }
            sequence.AddRange(FooterButtons(modal, reports));
            AssertTabCycle(window, [.. sequence]);
            Assert.True(reports.IsReportHistoryViewOpen);
            Assert.Equal(2, reports.ReportHistoryCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Opening the existing delete confirmation makes every parent Report Tab target unavailable.</summary>
    [AvaloniaFact]
    public void DeleteConfirmationPreventsParentReportFromTakingTabFocus()
    {
        ReportPresentationViewModel reports = CreateReports();
        reports.LoadReportJson(ReportJsonSamples.Succeeded(), "report.json");
        reports.ShowReportHistoryCommand.Execute(null);
        var modal = new ReportModal { DataContext = reports };
        Window window = ShowReport(modal, reports);
        try
        {
            Control[] parentStops = [modal, .. modal.GetVisualDescendants().OfType<Control>()
                .Where(control => control.Focusable && KeyboardNavigation.GetIsTabStop(control) &&
                    control.IsEffectivelyVisible && control.IsEffectivelyEnabled)];
            reports.RequestReportHistoryDeletionCommand.Execute(reports.ReportHistoryEntries[0]);
            Dispatcher.UIThread.RunJobs();
            ReportHistoryDeleteConfirmationModal confirmation = Assert.Single(modal.GetVisualDescendants().OfType<ReportHistoryDeleteConfirmationModal>());
            Assert.True(confirmation.IsKeyboardFocusWithin);
            foreach (Control control in parentStops)
            {
                Assert.False(control.Focus(NavigationMethod.Tab));
                Assert.True(confirmation.IsKeyboardFocusWithin);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static ReportPresentationViewModel CreateReports()
    {
        return new(() => ShellTextResources.For(ShellLanguage.English), static () => { });
    }

    private static Window ShowReport(ReportModal modal, ReportPresentationViewModel reports)
    {
        var window = new Window
        {
            Width = 1600,
            Height = 1000,
            DataContext = new { Reports = reports, reports.Text, MessageCenter = new { OpenRunReportsCommand = reports.CloseReportCommand } },
            Content = new Grid { Children = { new Button { Content = "Outside Report" }, modal } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Button[] HeaderButtons(ReportModal modal)
    {
        return [VisibleButton(modal, button => button.Classes.Contains("reportBackLink")),
            VisibleButton(modal, button => button.Classes.Contains("closeButton"))];
    }

    private static Button[] FooterButtons(ReportModal modal, ReportPresentationViewModel reports)
    {
        return [VisibleButton(modal, button => Equals(button.Content, reports.Text.CloseLabel)),
            VisibleButton(modal, button => Equals(button.Content, reports.Text.SaveReportLabel))];
    }

    private static Button VisibleButton(ReportModal modal, Func<Button, bool> predicate)
    {
        return Assert.Single(modal.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && predicate(button));
    }

    private static void AssertTabCycle(Window window, Control[] sequence)
    {
        Assert.True(sequence[0].Focus(NavigationMethod.Tab));
        foreach (Control expected in sequence.Skip(1).Append(sequence[0]))
        {
            PressTab(window, shift: false);
            Assert.True(ReferenceEquals(expected, window.FocusManager?.GetFocusedElement()),
                $"Expected {expected}, got {window.FocusManager?.GetFocusedElement()}.");
        }
        foreach (Control expected in sequence.Reverse())
        {
            PressTab(window, shift: true);
            Assert.Same(expected, window.FocusManager?.GetFocusedElement());
        }
        PressTab(window, shift: true);
        Assert.Same(sequence[^1], window.FocusManager?.GetFocusedElement());
    }

    private static void PressTab(Window window, bool shift)
    {
        RawInputModifiers modifiers = shift ? RawInputModifiers.Shift : RawInputModifiers.None;
        window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
        window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, "\t");
        Dispatcher.UIThread.RunJobs();
    }
}
