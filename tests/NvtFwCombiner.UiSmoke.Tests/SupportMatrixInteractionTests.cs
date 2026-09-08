using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the actual Settings matrix scroll and keyboard disclosure surfaces.</summary>
public sealed class SupportMatrixInteractionTests
{
    /// <summary>Last row/column and first cell remain reachable without changing workflow state.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, false)]
    [InlineData(1440, 900, true)]
    [InlineData(980, 640, false)]
    [InlineData(980, 640, true)]
    public async Task MatrixExtremesAndKeyboardDetailsRemainReachable(int width, int height, bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("support-matrix-interactions");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = width,
            Height = height,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            shell.SelectedLanguage = darkChinese ? "Traditional Chinese" : "English";
            shell.SelectedTheme = darkChinese ? "Dark" : "Light";
            string ic = shell.WorkflowSession.SelectedIc;
            string navigation = shell.Navigation.NavigationPath;
            shell.OpenSettingsCommand.Execute(null);
            shell.Settings.SelectSectionCommand.Execute(SettingsSection.SupportMatrix);
            Render();
            Border surface = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Name == "SettingsSurface");
            Border[] cells = [.. surface.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("supportMatrixCell"))];
            Assert.NotEmpty(cells);
            Assert.Equal(shell.Settings.SupportMatrix.IcRows.Sum(row => row.Cells.Count), cells.Length);
            Border first = cells[0];
            Border last = cells[^1];
            ScrollViewer[] scrolls = [.. last.GetVisualAncestors().OfType<ScrollViewer>()];
            Assert.Equal(2, scrolls.Length);
            ScrollViewer horizontal = scrolls[0];
            ScrollViewer vertical = scrolls[1];
            Assert.Equal(width < 1000, horizontal.Extent.Width > horizontal.Viewport.Width + 0.5);
            Border[] icHeaders = [.. surface.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("supportMatrixIcHeader") && b.DataContext is SupportMatrixIcRowViewModel)];
            Assert.Equal(shell.Settings.SupportMatrix.IcRows.Count, icHeaders.Length);
            double fixedIcLeft = Bounds(icHeaders[0], window).Left;
            Assert.All(cells, cell =>
            {
                Assert.Equal(46, cell.Bounds.Height);
                Assert.Equal(new Thickness(0, 0, 0, 1), cell.BorderThickness);
                Assert.Equal(cells[0].Background, cell.Background);
                Assert.InRange(Math.Abs(cell.Bounds.Width - cells[0].Bounds.Width), 0, 1);
            });
            Border[] workflowHeaders = [.. surface.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("supportMatrixWorkflowHeader") && b.DataContext is SupportMatrixWorkflowColumnViewModel)];
            Assert.Equal(shell.Settings.SupportMatrix.WorkflowColumns.Count, workflowHeaders.Length);
            for (int column = 0; column < workflowHeaders.Length; column++)
            {
                Assert.InRange(Math.Abs(Bounds(workflowHeaders[column], window).Left - Bounds(cells[column], window).Left), 0, 0.5);
                Assert.InRange(Math.Abs(workflowHeaders[column].Bounds.Width - cells[column].Bounds.Width), 0, 0.5);
                Assert.Equal(46, workflowHeaders[column].Bounds.Height);
            }
            foreach ((Border header, int index) in icHeaders.Select((header, index) => (header, index)))
            {
                Assert.Equal(new Thickness(0, 0, 0, 1), header.BorderThickness);
                Assert.Equal(46, header.Bounds.Height);
                Assert.InRange(Math.Abs(Bounds(header, window).Top - Bounds(cells[index * shell.Settings.SupportMatrix.WorkflowColumns.Count], window).Top), 0, 0.5);
            }
            Expander details = Assert.Single(surface.GetVisualDescendants().OfType<Expander>(), e => e.Name == "SupportMatrixCatalogDetails");
            Assert.False(details.IsExpanded);
            Capture(window, $"{width}-{height}-{(darkChinese ? "dark-zh" : "light-en")}-layout");
            foreach (Border cell in new[] { last, first })
            {
                cell.BringIntoView();
                Render();
                Assert.True(cell.Focus(NavigationMethod.Tab));
                Render();
                Assert.True(ToolTip.GetIsOpen(cell));
                SupportMatrixCellViewModel model = Assert.IsType<SupportMatrixCellViewModel>(cell.DataContext);
                Assert.Equal(model.AccessibleLabel, AutomationProperties.GetName(cell));
                Assert.Equal(model.AccessibleDetail, AutomationProperties.GetHelpText(cell));
                ToolTip tooltip = Assert.IsType<ToolTip>(ToolTip.GetTip(cell));
                Assert.False(tooltip.IsHitTestVisible);
                Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == model.IcId);
                Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == model.WorkflowLabel);
                Rect cellBounds = Bounds(cell, window);
                Rect viewport = Bounds(vertical, window);
                Assert.True(cellBounds.Left >= viewport.Left - 0.5 && cellBounds.Right <= viewport.Right + 0.5,
                    $"Cell escapes horizontal viewport: {cellBounds}; viewport={viewport}");
                Assert.True(cellBounds.Top >= viewport.Top - 0.5 && cellBounds.Bottom <= viewport.Bottom + 0.5,
                    $"Cell escapes vertical viewport: {cellBounds}; viewport={viewport}");
                if (cell == last)
                {
                    Assert.Equal(width < 1000, horizontal.Offset.X > 0.5);
                    Assert.InRange(Math.Abs(Bounds(icHeaders[^1], window).Left - fixedIcLeft), 0, 0.5);
                    Assert.InRange(Math.Abs(Bounds(icHeaders[^1], window).Top - cellBounds.Top), 0, 0.5);
                    if (vertical.Extent.Height > vertical.Viewport.Height)
                    {
                        Assert.True(vertical.Offset.Y > 0);
                    }
                }
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Render();
                Assert.False(ToolTip.GetIsOpen(cell));
                Assert.True(shell.IsSettingsModalOpen);
                Assert.True(cell.IsFocused);
                Assert.False(ToolTip.GetServiceEnabled(cell));
                Capture(window, $"{width}-{height}-{(darkChinese ? "dark-zh" : "light-en")}-{(cell == last ? "last" : "first")}");
            }
            Assert.True(ToolTip.GetServiceEnabled(last));
            details.BringIntoView();
            Render();
            global::Avalonia.Controls.Primitives.ToggleButton disclosure = Assert.Single(details.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ToggleButton>(), b => b.Name == "ExpanderHeader");
            Assert.True(disclosure.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Render();
            Assert.True(details.IsExpanded);
            foreach (string value in new[] { shell.Settings.SupportMatrix.SourceHash, shell.Settings.SupportMatrix.ResolutionToken })
            {
                TextBlock text = Assert.Single(details.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == value);
                text.BringIntoView();
                Render();
                Rect textBounds = Bounds(text, window);
                Rect viewport = Bounds(vertical, window);
                Assert.True(text.IsEffectivelyVisible);
                Assert.Equal(global::Avalonia.Media.TextTrimming.None, text.TextTrimming);
                Assert.True(textBounds.Left >= viewport.Left - 0.5 && textBounds.Right <= viewport.Right + 0.5);
                Assert.True(textBounds.Top >= viewport.Top - 0.5 && textBounds.Bottom <= viewport.Bottom + 0.5);
                Assert.True(text.TextLayout.Height <= text.Bounds.Height + 0.5);
                Assert.All(text.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
            }
            Capture(window, $"{width}-{height}-{(darkChinese ? "dark-zh" : "light-en")}-catalog");
            foreach (int resizedWidth in new[] { 980, 1440, width })
            {
                window.Width = resizedWidth;
                Render();
                Assert.Equal(resizedWidth < 1000, horizontal.Extent.Width > horizontal.Viewport.Width + 0.5);
                Assert.InRange(Math.Abs(Bounds(icHeaders[0], window).Left - fixedIcLeft), 0, 0.5);
                Assert.InRange(Math.Abs(Bounds(workflowHeaders[0], window).Left - Bounds(cells[0], window).Left), 0, 0.5);
            }
            Assert.Equal(ic, shell.WorkflowSession.SelectedIc);
            Assert.Equal(navigation, shell.Navigation.NavigationPath);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render();
            Assert.False(shell.IsSettingsModalOpen);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static Rect Bounds(Control control, Window window)
    {
        return new Rect(Assert.IsType<Point>(control.TranslatePoint(default, window)), control.Bounds.Size);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Capture(Window window, string state)
    {
        using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, $"support-matrix-{state}.png"));
        }
    }
}
