using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>History actions run through actual rendered controls and the existing queued file writer.</summary>
public sealed class ReportHistoryControlTests
{
    /// <summary>Message Center owns its deferred styles and retains them across real shell opens.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MessageCenterStylesLoadWithModalAndSurviveReopen(bool dark, bool chinese)
    {
        Assert.DoesNotContain(global::Avalonia.Application.Current!.Styles.OfType<Styles>()
            .SelectMany(styles => styles.OfType<Style>()),
            style => style.Selector?.ToString() == "Border.messageCenterHeader");
        using var workspace = TempWorkspace.Create("v115-deferred-message-center");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        ContentControl host = window.FindControl<ContentControl>("MessageCenterModalHost")!;
        Assert.Null(host.Content);
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Width = 1536;
        window.Height = 864;
        try
        {
            await AwaitHistoryReadyAsync(window);
            Assert.Null(host.Content);
            for (int opening = 0; opening < 2; opening++)
            {
                shell.MessageCenter.OpenCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                MessageCenterModal modal = Assert.Single(window.GetVisualDescendants().OfType<MessageCenterModal>());
                _ = Assert.Single(modal.Styles.OfType<Styles>().SelectMany(styles => styles.OfType<Style>()),
                    style => style.Selector?.ToString() == "Border.messageCenterHeader");
                Border rail = modal.FindControl<Border>("MessageCenterNavigationRail")!;
                Assert.Equal(new Thickness(18, 30, 36, 30), rail.Padding);
                Assert.Equal(new Thickness(0, 0, 2, 0), rail.BorderThickness);
                Border header = Assert.Single(modal.GetVisualDescendants().OfType<Border>(),
                    border => border.Classes.Contains("messageCenterHeader"));
                Assert.Equal(new Thickness(0, 0, 0, 2), header.BorderThickness);
                Assert.Equal(rail.Background, header.Background);
                Assert.NotNull(header.Background);
                shell.MessageCenter.CloseCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.False(shell.MessageCenter.IsOpen);
            }
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>One pointer/keyboard activation opens the selected report without appending history.</summary>
    [AvaloniaTheory]
    [InlineData("mouse")]
    [InlineData("Enter")]
    [InlineData("Space")]
    public async Task HistoryCardOpensExactEntryWithOneActivation(string activation)
    {
        using var workspace = TempWorkspace.Create("v114-history-open");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            shell.Reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            ReportHistoryEntryViewModel selected = shell.Reports.ReportHistoryEntries[1];
            Button card = EntryButton(window, selected, delete: false);
            Activate(window, card, activation);
            Task? opening = shell.Reports.OpenReportHistoryEntryAsyncCommand.ExecutionTask;
            Assert.NotNull(opening);
            await opening.WaitAsync(TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(selected.ReportJson, shell.Reports.LoadedReportJson);
            Assert.Equal("older.json", shell.Reports.LoadedReport.SourceName);
            Assert.True(shell.Reports.IsReportReviewViewOpen);
            Assert.False(shell.Reports.IsReportHistoryViewOpen);
            Assert.Equal(2, shell.Reports.ReportHistoryCount);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == shell.Reports.LoadedReport.Title);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>The selector-style trash deletes only its entry, never opens it, and survives the real close-flush/reload.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, 1920)]
    [InlineData(true, true, 1920)]
    [InlineData(false, true, 1024)]
    [InlineData(true, false, 1024)]
    public async Task HistoryTrashDeletesOnlyTargetAndPersists(bool dark, bool chinese, double width)
    {
        using var workspace = TempWorkspace.Create("v114-history-delete");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Width = width;
        window.Height = 850;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        ReportHistoryEntryViewModel removed;
        ReportHistoryEntryViewModel retained;
        try
        {
            await AwaitHistoryReadyAsync(window);
            SeedHistory(shell);
            removed = shell.Reports.ReportHistoryEntries[chinese ? 0 : 1];
            retained = shell.Reports.ReportHistoryEntries[chinese ? 1 : 0];
            string current = shell.Reports.LoadedReportJson;
            shell.Reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Button trash = EntryButton(window, removed, delete: true);
            Button card = EntryButton(window, removed, delete: false);
            Assert.Equal(new Size(36, 36), trash.Bounds.Size);
            Point trashCenter = trash.TranslatePoint(new Point(18, 18), card)!.Value;
            Assert.InRange(Math.Abs(trashCenter.Y - (card.Bounds.Height / 2)), 0, 0.5);
            TextBlock issueSummary = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == removed.Issues);
            Point issueCenter = issueSummary.TranslatePoint(new Point(0, issueSummary.Bounds.Height / 2), card)!.Value;
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(outputDirectory, $"history-row-{width}-{dark}-{chinese}.png"));
            }
            Grid rowContent = Assert.IsType<Grid>(card.Content);
            Assert.True(Math.Abs(issueCenter.Y - trashCenter.Y) <= 0.5,
                $"History issue/trash center mismatch: chinese={chinese}, dark={dark}, width={width}; " +
                $"issueCenter={issueCenter.Y}, trashCenter={trashCenter.Y}; " +
                $"issueBounds={issueSummary.Bounds}, issueDesired={issueSummary.DesiredSize}, " +
                $"textLayoutHeight={issueSummary.TextLayout.Height}, font={issueSummary.FontFamily}, " +
                $"fontSize={issueSummary.FontSize}, weight={issueSummary.FontWeight}, " +
                $"layoutRounding={issueSummary.UseLayoutRounding}, scale={window.RenderScaling}; " +
                $"trashBounds={trash.Bounds}, rowBounds={card.Bounds}, contentBounds={rowContent.Bounds}, " +
                $"contentOrigin={rowContent.TranslatePoint(default, card)}, verticalContent={card.VerticalContentAlignment}.");
            Assert.Equal("\uE74D", Assert.IsType<TextBlock>(trash.Content).Text);
            string name = Assert.IsType<string>(AutomationProperties.GetName(trash));
            Assert.Contains(chinese ? "刪除" : "Delete", name, StringComparison.Ordinal);
            Assert.Contains(removed.SequenceLabel, name, StringComparison.Ordinal);
            Assert.Equal(name, ToolTip.GetTip(trash));
            Assert.Contains("slotClearAction", trash.Classes);
            Assert.True(card.Bounds.Width > 300);
            Activate(window, trash, chinese ? "Enter" : "mouse");
            Assert.Equal(2, shell.Reports.ReportHistoryCount);
            Button cancel = ConfirmationButton(window, chinese ? "取消" : "Cancel");
            Assert.True(cancel.IsFocused);
            shell.Reports.RequestReportHistoryDeletionCommand.Execute(retained);
            Assert.Same(removed, shell.Reports.PendingHistoryDeletion);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            Assert.True(ConfirmationButton(window, chinese ? "刪除" : "Delete").IsFocused);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
            Assert.True(cancel.IsFocused);
            Activate(window, cancel, "Space");
            Assert.Equal(2, shell.Reports.ReportHistoryCount);
            Assert.True(trash.IsFocused);
            Activate(window, trash, "Enter");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, shell.Reports.ReportHistoryCount);
            Assert.True(shell.Reports.IsReportHistoryViewOpen);
            Activate(window, trash, "Enter");
            Button confirm = ConfirmationButton(window, chinese ? "刪除" : "Delete");
            Assert.Contains("danger", confirm.Classes);
            Color danger = Assert.IsType<ISolidColorBrush>(confirm.Foreground, exactMatch: false).Color;
            Assert.True(danger.R > danger.G && danger.R > danger.B);
            Border surface = Assert.Single(confirm.GetVisualAncestors().OfType<Border>(), border => border.Classes.Contains("modalSurface"));
            Assert.InRange(surface.Bounds.Width, 539.5, 540.5);
            Point surfaceOrigin = surface.TranslatePoint(default, window)!.Value;
            Assert.InRange(Math.Abs(surfaceOrigin.X + (surface.Bounds.Width / 2) - (window.ClientSize.Width / 2)), 0, 0.5);
            Activate(window, confirm, "Enter");
            shell.Reports.ConfirmReportHistoryDeletionCommand.Execute(null);
            Assert.Same(retained, Assert.Single(shell.Reports.ReportHistoryEntries));
            Assert.Equal(current, shell.Reports.LoadedReportJson);
            Assert.True(shell.Reports.IsReportHistoryViewOpen);
            Assert.Null(shell.Reports.OpenReportHistoryEntryAsyncCommand.ExecutionTask);
            shell.Reports.RequestReportHistoryDeletionCommand.Execute(retained);
            shell.Reports.CloseReportCommand.Execute(null);
            Assert.False(shell.Reports.IsHistoryDeleteConfirmationOpen);
            _ = Assert.Single(shell.Reports.ReportHistoryEntries);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }

        IReadOnlyList<ReportHistorySnapshot> persisted = await ReportHistoryFileStore.LoadAsync(
            services.LocalFiles, ReportHistoryFileStore.DefaultHistoryPath, TestContext.Current.CancellationToken);
        Assert.Equal(retained.SourceName, Assert.Single(persisted).SourceName);
        Assert.DoesNotContain(persisted, entry => entry.SourceName == removed.SourceName);

        using var reopened = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var restored = (MainWindowViewModel)reopened.DataContext!;
        reopened.Show();
        try
        {
            await AwaitHistoryReadyAsync(reopened);
            restored.Reports.ShowReportHistoryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            ReportHistoryEntryViewModel last = Assert.Single(restored.Reports.ReportHistoryEntries);
            string loaded = restored.Reports.LoadedReportJson;
            Activate(reopened, EntryButton(reopened, last, delete: true), "Space");
            _ = Assert.Single(restored.Reports.ReportHistoryEntries);
            Activate(reopened, ConfirmationButton(reopened, "Delete"), "Enter");
            Assert.Empty(restored.Reports.ReportHistoryEntries);
            Assert.False(restored.Reports.IsReportHistoryViewOpen);
            Assert.True(restored.Reports.IsReportReviewViewOpen);
            Assert.Equal(loaded, restored.Reports.LoadedReportJson);
        }
        finally
        {
            await CloseAndFlushAsync(reopened);
        }
        Assert.Empty(await ReportHistoryFileStore.LoadAsync(
            services.LocalFiles, ReportHistoryFileStore.DefaultHistoryPath, TestContext.Current.CancellationToken));
    }

    private static void SeedHistory(MainWindowViewModel shell)
    {
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "older-history"), "older.json");
        shell.Reports.LoadReportJson(ReportJsonSamples.CtrlRamCommandSucceeded(), "current.json");
    }

    private static Button ConfirmationButton(Window window, string label)
    {
        return Assert.Single(window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && Equals(button.Content, label));
    }

    private static Button EntryButton(Window window, ReportHistoryEntryViewModel entry, bool delete)
    {
        return Assert.Single(window.GetVisualDescendants().OfType<Button>(), button =>
            ReferenceEquals(button.DataContext, entry) && button.Classes.Contains("danger") == delete);
    }

    private static void Activate(Window window, Button button, string activation)
    {
        if (activation == "mouse")
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Point center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            var hit = window.InputHitTest(center) as Visual;
            Assert.True(ReferenceEquals(hit, button) || hit?.GetVisualAncestors().Contains(button) == true,
                $"Expected pointer hit on {button}, got {hit} at {center}; button bounds {button.Bounds}.");
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
        }
        else
        {
            Assert.True(button.Focus(NavigationMethod.Tab));
            Key key = activation == "Enter" ? Key.Enter : Key.Space;
            PhysicalKey physical = activation == "Enter" ? PhysicalKey.Enter : PhysicalKey.Space;
            window.KeyPress(key, RawInputModifiers.None, physical, activation == "Enter" ? "\r" : " ");
            window.KeyRelease(key, RawInputModifiers.None, physical, activation == "Enter" ? "\r" : " ");
        }
        Dispatcher.UIThread.RunJobs();
    }

}
