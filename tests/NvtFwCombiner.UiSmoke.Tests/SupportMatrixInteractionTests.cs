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
        PresentationHostServices services = await CreateServicesAsync(workspace);
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
            RadioButton[] navigationItems = [.. surface.GetVisualDescendants().OfType<RadioButton>().Where(b => b.Classes.Contains("settingsNavItem"))];
            // Config is the fifth approved Settings navigation entry.
            Assert.Equal(5, navigationItems.Length);
            foreach (RadioButton item in navigationItems)
            {
                Grid content = Assert.IsType<Grid>(item.Content);
                TextBlock label = Assert.Single(content.Children.OfType<TextBlock>());
                Assert.Equal(global::Avalonia.Layout.HorizontalAlignment.Left, label.HorizontalAlignment);
                Assert.InRange(Math.Abs(Bounds(label, window).Left - Bounds(content, window).Left - 40), 0, 0.5);
            }
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
                Assert.Null(cell.FocusAdorner);
                Assert.True(cell.IsSet(Control.FocusAdornerProperty));
                Assert.True(cell.BorderThickness == new Thickness(2), $"Focus border={cell.BorderThickness}; classes={string.Join(',', cell.Classes)}");
                Assert.Equal(cell.FindResource(cell.ActualThemeVariant, "NfcAccentBrush"), cell.BorderBrush);
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
                    if (width < 1000)
                    {
                        global::Avalonia.Controls.Primitives.ScrollBar horizontalBar = Assert.Single(horizontal.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ScrollBar>(), b => b.Orientation == global::Avalonia.Layout.Orientation.Horizontal);
                        global::Avalonia.Controls.Primitives.ScrollBar verticalBar = Assert.Single(vertical.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ScrollBar>(), b => b.Orientation == global::Avalonia.Layout.Orientation.Vertical && b.GetVisualAncestors().OfType<ScrollViewer>().First() == vertical);
                        Assert.True(horizontalBar.IsEffectivelyVisible && verticalBar.IsEffectivelyVisible);
                        global::Avalonia.Controls.Primitives.Thumb horizontalThumb = Assert.Single(horizontalBar.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Thumb>());
                        global::Avalonia.Controls.Primitives.Thumb verticalThumb = Assert.Single(verticalBar.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Thumb>());
                        Assert.Equal(6, horizontalThumb.Bounds.Height);
                        Assert.Equal(verticalThumb.Bounds.Width, horizontalThumb.Bounds.Height);
                        Assert.Equal(verticalThumb.Background, horizontalThumb.Background);
                        Assert.Equal(verticalThumb.CornerRadius, horizontalThumb.CornerRadius);
                        Assert.All(horizontalBar.GetVisualDescendants().OfType<RepeatButton>(), button => Assert.Equal(0, button.Opacity));
                        Assert.All(horizontalBar.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Rectangle>().Where(r => r.Name == "TrackRect"), track => Assert.Equal(0, track.Opacity));
                        Assert.True(Bounds(horizontalBar, window).Bottom <= viewport.Bottom + 0.5);
                        Assert.True(Bounds(horizontalBar, window).Top - cellBounds.Bottom >= 8, "Horizontal scrollbar must have its own lane, at least8px below the last row.");
                        Assert.True(Bounds(verticalBar, window).Left - cellBounds.Right >= 12, "Vertical scrollbar must have at least12px clearance from the table.");
                    }
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
            if (width < 1000)
            {
                global::Avalonia.Controls.Primitives.Thumb thumb = Assert.Single(horizontal.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Thumb>());
                thumb.BringIntoView();
                Render();
                Point start = Bounds(thumb, window).Center;
                window.MouseMove(start);
                Render();
                Assert.Equal(6, thumb.Bounds.Height);
                Assert.Equal(thumb.FindResource(thumb.ActualThemeVariant, "NfcTextMutedBrush"), thumb.Background);
                double beforeDrag = horizontal.Offset.X;
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + new Vector(40, 0), RawInputModifiers.LeftMouseButton);
                Render();
                Assert.Equal(6, thumb.Bounds.Height);
                window.MouseUp(start + new Vector(40, 0), MouseButton.Left);
                Render();
                Assert.True(horizontal.Offset.X > beforeDrag);
                double beforePageClick = horizontal.Offset.X;
                Point page = new(Bounds(thumb, window).Left - 8, Bounds(thumb, window).Center.Y);
                window.MouseDown(page, MouseButton.Left);
                window.MouseUp(page, MouseButton.Left);
                Render();
                Assert.True(horizontal.Offset.X < beforePageClick);
                window.MouseMove(new Point(10, 10));
                Render();
                Assert.Equal(thumb.FindResource(thumb.ActualThemeVariant, "NfcTextDisabledBrush"), thumb.Background);
            }
            details.BringIntoView();
            Render();
            global::Avalonia.Controls.Primitives.ToggleButton disclosure = Assert.Single(details.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ToggleButton>(), b => b.Name == "ExpanderHeader");
            Assert.True(disclosure.Focus(NavigationMethod.Tab));
            Render();
            Assert.Null(disclosure.FocusAdorner);
            Assert.True(disclosure.IsSet(Control.FocusAdornerProperty));
            Assert.Equal(new Thickness(2), disclosure.BorderThickness);
            Assert.Equal(disclosure.FindResource(disclosure.ActualThemeVariant, "NfcAccentStrongBrush"), disclosure.BorderBrush);
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
