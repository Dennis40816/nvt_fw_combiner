using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>Loaded Message Center spacing retains each purpose's existing geometry.</summary>
public sealed class MessageCenterSpacingRoleTests
{
    /// <summary>Both pages retain their gaps and fit in each localized theme and viewport.</summary>
    [AvaloniaTheory]
    [InlineData(1536, 864, false, false)]
    [InlineData(1536, 864, true, false)]
    [InlineData(1536, 864, false, true)]
    [InlineData(1536, 864, true, true)]
    [InlineData(980, 640, false, false)]
    [InlineData(980, 640, true, false)]
    [InlineData(980, 640, false, true)]
    [InlineData(980, 640, true, true)]
    public async Task LoadedPagesPreservePurposeSpecificSpacingAndLayout(
        int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("message-center-spacing");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = width,
            Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(), "spacing.json");
            shell.MessageCenter.OpenRunReportsCommand.Execute(null);
            Render();

            MessageCenterModal modal = Assert.Single(window.GetVisualDescendants().OfType<MessageCenterModal>());
            Border surface = modal.FindControl<Border>("MessageCenterSurface")!;
            Border navigation = modal.FindControl<Border>("MessageCenterNavigationRail")!;
            Grid content = modal.FindControl<Grid>("MessageCenterContent")!;
            AssertInside(surface, window);
            Assert.InRange(surface.Bounds.Width, width - (width < 1200 ? 28 : 64) - 0.5,
                width - (width < 1200 ? 28 : 64) + 0.5);
            Assert.InRange(surface.Bounds.Height, height - (width < 1200 ? 22 : 48) - 0.5,
                height - (width < 1200 ? 22 : 48) + 0.5);
            Assert.Equal(width < 1200 ? 200 : 324, navigation.Bounds.Width);
            Assert.Equal(width < 1200 ? new Thickness(24, 24, 24, 20) : new Thickness(44, 30, 50, 24),
                content.Margin);

            StackPanel section = Assert.IsType<StackPanel>(navigation.Child);
            Assert.Contains("messageCenterSection", section.Classes);
            Assert.Equal(12, section.Spacing);
            AssertGap(section.Children[1], section.Children[2], section, 12);
            // The rail label also retains its separate bottom margin.
            Assert.Equal(new Thickness(12, 0, 0, 8), section.Children[0].Margin);
            AssertGap(section.Children[0], section.Children[1], section, 20);
            foreach (Control item in section.Children)
            {
                AssertInside(item, navigation);
            }

            TextBlock modalTitle = Assert.Single(modal.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Classes.Contains("modalTitle"));
            Assert.Equal(chinese ? "訊息中心" : "Message Center", modalTitle.Text);
            AssertTitleDetail(Assert.IsType<StackPanel>(modalTitle.Parent));

            RunReportsTable table = modal.FindControl<RunReportsTable>("RunReportsList")!;
            Grid reports = Assert.IsType<Grid>(table.Parent);
            Assert.True(reports.IsEffectivelyVisible);
            Assert.Contains("messageCenterPageGap", reports.Classes);
            Assert.Contains("runReportsPageGap", reports.Classes);
            Assert.Equal(20, reports.RowSpacing);
            AssertPageGaps(reports, 20);
            AssertTitleDetail(Assert.Single(reports.Children[0].GetVisualDescendants().OfType<StackPanel>(),
                panel => panel.Orientation == Avalonia.Layout.Orientation.Vertical));
            AssertInside(table, content);
            AssertInside(reports.Children[2], content);
            AssertInside(modal.FindControl<Button>("LoadRunReportButton")!, content);
            _ = Assert.Single(table.GetVisualDescendants().OfType<Button>(),
                button => button.Classes.Contains("reportListRow") && button.IsEffectivelyVisible);
            AssertRendered(window, width, height, dark);

            shell.MessageCenter.ShowSystemInformationCommand.Execute(null);
            shell.MessageCenter.ToggleDebugActivityCommand.Execute(null);
            Render();
            Grid activity = modal.FindControl<Grid>("SystemActivityRoot")!;
            Assert.True(activity.IsEffectivelyVisible);
            Assert.Contains("messageCenterPageGap", activity.Classes);
            Assert.DoesNotContain("runReportsPageGap", activity.Classes);
            Assert.False(reports.IsEffectivelyVisible);
            Assert.Equal(16, activity.RowSpacing);
            AssertPageGaps(activity, 16);
            AssertTitleDetail(Assert.Single(activity.Children[0].GetVisualDescendants().OfType<StackPanel>()));
            ScrollViewer timeline = modal.FindControl<ScrollViewer>("SystemActivityTimelineViewport")!;
            AssertInside(timeline, content);
            Assert.True(timeline.Bounds.Height > (width < 1200 ? 220 : 420));
            Border[] rows = [.. timeline.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("activityRow"))];
            Assert.NotEmpty(rows);
            foreach (Border row in rows)
            {
                Grid rowLayout = Assert.IsType<Grid>(row.Child);
                AssertTitleDetail(Assert.Single(rowLayout.Children.OfType<StackPanel>(),
                    panel => Grid.GetColumn(panel) == 2));
            }
            foreach (Button button in activity.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && button.Classes.Contains("semanticAction")))
            {
                AssertInside(button, content);
            }
            AssertInside(activity.Children[4], content);
            AssertRendered(window, width, height, dark);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertTitleDetail(StackPanel panel)
    {
        Assert.Contains("messageCenterTitleDetail", panel.Classes);
        Assert.Equal(4, panel.Spacing);
        Assert.Equal(2, panel.Children.Count);
        AssertGap(panel.Children[0], panel.Children[1], panel, 4);
    }

    private static void AssertPageGaps(Grid page, double expected)
    {
        for (int index = 1; index < page.Children.Count; index++)
        {
            AssertGap(page.Children[index - 1], page.Children[index], page, expected);
        }
    }

    private static void AssertGap(Control before, Control after, Visual ancestor, double expected)
    {
        Point end = before.TranslatePoint(new Point(0, before.Bounds.Height), ancestor)!.Value;
        Point start = after.TranslatePoint(default, ancestor)!.Value;
        Assert.InRange(start.Y - end.Y, expected - 0.5, expected + 0.5);
    }

    private static void AssertInside(Control control, Visual ancestor)
    {
        Point origin = control.TranslatePoint(default, ancestor)!.Value;
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
        Assert.InRange(origin.X, -0.5, ancestor.Bounds.Width - control.Bounds.Width + 0.5);
        Assert.InRange(origin.Y, -0.5, ancestor.Bounds.Height - control.Bounds.Height + 0.5);
    }

    private static void AssertRendered(Window window, int width, int height, bool dark)
    {
        using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
        Assert.Equal(new PixelSize(width, height), frame.PixelSize);
    }
}
