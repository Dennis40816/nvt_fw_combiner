using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Actual Message Center list and navigation regressions.</summary>
public sealed partial class RunReportsListTests
{
    /// <summary>First import stays reachable and navigation uses the complete rail.</summary>
    [AvaloniaTheory]
    [InlineData(1635, 962, false, false)]
    [InlineData(1024, 768, true, true)]
    public async Task RunReportsShowsDirectListAndFullWidthNavigation(int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("run-reports-list");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Width = width;
        window.Height = height;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            if (chinese)
            {
                shell.SelectedLanguage = "Traditional Chinese";
            }
            shell.MessageCenter.OpenCommand.Execute(null);
            shell.MessageCenter.ShowRunReportsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "LoadRunReportButton" && b.IsEffectivelyVisible && b.IsEnabled);
            Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Name == "RunReportsTableSurface" && b.IsEffectivelyVisible);
            foreach (Avalonia.Controls.Primitives.ToggleButton button in window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(b => b.Classes.Contains("messageCenterNavigationItem")))
            {
                Assert.Equal(Avalonia.Layout.HorizontalAlignment.Stretch, button.HorizontalAlignment);
            }
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("messageCenterReportCard"));
            string? visualInput = Environment.GetEnvironmentVariable("NFC_REPORT_VISUAL_INPUT");
            shell.Reports.LoadReportJson(string.IsNullOrWhiteSpace(visualInput)
                ? ReportJsonSamples.Succeeded(icId: "NT51929", startedAtUtc: "2026-09-06T01:54:46Z")
                : File.ReadAllText(visualInput), "control.json");
            Render();
            Grid headers = window.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "ReportColumnHeaders");
            foreach (Avalonia.Controls.Primitives.ToggleButton navigation in window.GetVisualDescendants()
                .OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(button => button.Classes.Contains("messageCenterNavigationItem")))
            {
                TextBlock label = navigation.GetVisualDescendants().OfType<TextBlock>().Single();
                Assert.Equal(FontWeight.ExtraBold, label.FontWeight);
                double textCenter = label.TranslatePoint(new Point(0, label.TextLayout.Height / 2), navigation)!.Value.Y;
                global::Avalonia.Controls.Shapes.Path icon = navigation.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single();
                double iconCenter = icon.TranslatePoint(new Point(0, icon.Bounds.Height / 2), navigation)!.Value.Y;
                Assert.InRange(Math.Abs(textCenter - (navigation.Bounds.Height / 2)), 0, 1);
                Assert.InRange(Math.Abs(iconCenter - (navigation.Bounds.Height / 2)), 0, 1);
            }
            Button row = window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("reportListRow") && button.IsEffectivelyVisible);
            ReportTableText[] cells = [.. row.GetVisualDescendants().OfType<ReportTableText>()];
            ReportTableText[] labels = [.. headers.Children.OfType<ReportTableText>()];
            Assert.Equal(5, cells.Length);
            Assert.Equal(5, labels.Length);
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.Equal(FontWeight.ExtraBold, labels[i].FontWeight);
                var headerTypeface = new Typeface(labels[i].FontFamily, labels[i].FontStyle, labels[i].FontWeight);
                Assert.Equal("Inter", headerTypeface.GlyphTypeface.FamilyName);
                Assert.NotEqual(FontWeight.Bold, cells[i].FontWeight);
                Assert.Equal(TextAlignment.Center, cells[i].TextAlignment);
                double center = cells[i].TranslatePoint(new Point(cells[i].Bounds.Width / 2, 0), window)!.Value.X;
                double headerCenter = labels[i].TranslatePoint(new Point(labels[i].Bounds.Width / 2, 0), window)!.Value.X;
                Assert.InRange(Math.Abs(center - headerCenter), 0, 1);
                Assert.InRange(cells[i].Bounds.Width, 0, 280);
            }
            Assert.Equal("NT51929", cells[1].Text);
            Assert.Equal("Standard Merge", cells[2].Text);
            Assert.Equal("0", cells[4].Text);
            Assert.DoesNotContain(row.GetVisualDescendants().OfType<Border>(), border => border.BorderThickness.Left > 0 && !border.Classes.Contains("reportResult"));
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, $"run-reports-{width}-{height}-{(dark ? "dark" : "light")}-{(chinese ? "zh" : "en")}.png"));
            }
            ReportHistoryEntryViewModel entry = Assert.Single(shell.Reports.ReportHistoryEntries);
            _ = row.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            await shell.Reports.OpenReportHistoryEntryAsyncCommand.ExecutionTask!;
            Assert.Equal(entry.ReportJson, shell.Reports.LoadedReportJson);
            Assert.True(shell.Reports.IsReportModalOpen);
            shell.MessageCenter.OpenRunReportsCommand.Execute(null);
            Render();
            Assert.True(shell.MessageCenter.IsRunReportsSelected);
            Assert.True(shell.MessageCenter.IsOpen);
            Assert.False(shell.Reports.IsReportModalOpen);
            Assert.True(row.IsFocused);
            Button trash = window.GetVisualDescendants().OfType<Button>().Single(button =>
                button.IsEffectivelyVisible && ReferenceEquals(button.DataContext, entry) && button.Classes.Contains("danger"));
            _ = trash.Focus(NavigationMethod.Tab);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Render();
            Assert.Same(entry, shell.Reports.PendingHistoryDeletion);
            shell.Reports.CancelReportHistoryDeletionCommand.Execute(null);
            Render();
            Assert.True(trash.IsFocused);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            shell.Reports.ConfirmReportHistoryDeletionCommand.Execute(null);
            Render();
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            Assert.True(window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "LoadRunReportButton").IsFocused);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Bounded history scrolls inside the compact modal and returns to the same list position.</summary>
    [AvaloniaFact]
    public async Task CompactHistoryRetainsScrollAcrossDetail()
    {
        using var workspace = TempWorkspace.Create("run-report-scroll");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Width = 980;
        window.Height = 640;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            for (int index = 0; index < 12; index++)
            {
                shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: $"row-{index}"), $"row-{index}.json");
            }
            shell.MessageCenter.OpenRunReportsCommand.Execute(null);
            Render();
            RunReportsTable table = window.GetVisualDescendants().OfType<RunReportsTable>().Single();
            ScrollViewer scroll = table.FindControl<ScrollViewer>("ReportListScroll")!;
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            TextBlock footer = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == shell.Text.RunReportsSelectHint);
            Point footerEnd = footer.TranslatePoint(new Point(footer.Bounds.Width, footer.Bounds.Height), window)!.Value;
            Assert.InRange(footerEnd.X, 0, 980);
            Assert.InRange(footerEnd.Y, 0, 640);
            scroll.Offset = new Vector(0, 148);
            Render();
            ReportHistoryEntryViewModel entry = shell.Reports.RunReportEntries[3];
            Button row = table.GetVisualDescendants().OfType<Button>().Single(button =>
                button.Classes.Contains("reportListRow") && ReferenceEquals(button.DataContext, entry));
            _ = row.Focus(NavigationMethod.Tab);
            Render();
            Vector before = scroll.Offset;
            IReadOnlyList<ReportHistoryEntryViewModel> entriesBefore = shell.Reports.RunReportEntries;
            await shell.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(entry);
            Render();
            Assert.Same(entriesBefore, shell.Reports.RunReportEntries);
            Assert.Equal(before, scroll.Offset);
            shell.MessageCenter.OpenRunReportsCommand.Execute(null);
            Render();
            Assert.Equal(before, scroll.Offset);
            Assert.True(row.IsFocused);
            Button trash = table.GetVisualDescendants().OfType<Button>().Single(button =>
                button.Classes.Contains("danger") && ReferenceEquals(button.DataContext, entry));
            _ = trash.Focus(NavigationMethod.Tab);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Render();
            Assert.Same(entry, shell.Reports.PendingHistoryDeletion);
            shell.Reports.ConfirmReportHistoryDeletionCommand.Execute(null);
            Render();
            Assert.Equal(11, shell.Reports.ReportHistoryCount);
            Button remaining = Assert.IsType<Button>(window.FocusManager!.GetFocusedElement());
            Assert.Contains("reportListRow", remaining.Classes);
            Assert.NotSame(entry, remaining.DataContext);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>Dates sort by execution time, not import order; missing metadata stays unknown.</summary>
    [Fact]
    public void RunReportsUsesDeclaredMetadataAndExecutionOrdering()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(icId: "NT51929", startedAtUtc: "2026-09-06T01:54:46Z"), "NT99999.json");
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(icId: "NT51927", startedAtUtc: "2026-09-05T01:54:46Z"), "new-import.json");
        Assert.Equal("NT51929", shell.Reports.RunReportEntries[0].Ic);
        Assert.Equal("0", shell.Reports.RunReportEntries[0].Issues);
        Assert.True(shell.Reports.RunReportEntries[0].IsSuccess);
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(icId: "", experienceId: "", modeId: "", compositionKind: "", startedAtUtc: ""), "NT51929-standard-merge.json");
        Assert.Equal("—", shell.Reports.ReportHistoryEntries[0].Ic);
        Assert.Equal("—", shell.Reports.ReportHistoryEntries[0].RunType);
        Assert.Equal("—", shell.Reports.ReportHistoryEntries[0].RunDate);
    }

    /// <summary>The actual renderer, not character count, controls overflow tooltip availability.</summary>
    [AvaloniaFact]
    public void ReportCellTooltipAppearsOnlyForRenderedOverflow()
    {
        var cell = new ReportTableText { Text = "Long report type that cannot fit", Width = 70 };
        var window = new Window { Width = 400, Height = 180, Content = cell };
        window.Show();
        try
        {
            Render();
            Assert.Equal(cell.Text, ToolTip.GetTip(cell));
            cell.Width = 350;
            Render();
            Assert.Null(ToolTip.GetTip(cell));
            cell.Text = "短";
            cell.Width = 70;
            Render();
            Assert.Null(ToolTip.GetTip(cell));
        }
        finally { window.Close(); }
    }
}
