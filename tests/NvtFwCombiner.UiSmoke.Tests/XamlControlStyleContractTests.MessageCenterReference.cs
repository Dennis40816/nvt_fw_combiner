using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The System Activity surface stays inside the minimum window in both themes and languages.</summary>
    [AvaloniaFact]
    public async Task MessageCenterActivityFitsCompactLocalizedWindow()
    {
        foreach ((bool useDarkTheme, ShellLanguage language) in new[]
        {
            (false, ShellLanguage.English),
            (true, ShellLanguage.English),
            (false, ShellLanguage.ChineseTraditional),
            (true, ShellLanguage.ChineseTraditional),
        })
        {
            MainWindowViewModel viewModel = await Task.Run(
                () => PresentationTestHost.CreateViewModel(language),
                TestContext.Current.CancellationToken);
            viewModel.MessageCenter.OpenCommand.Execute(null);
            viewModel.MessageCenter.ToggleDebugActivityCommand.Execute(null);
            var modal = new MessageCenterModal { DataContext = viewModel.MessageCenter };
            var window = new Window
            {
                Width = 980,
                Height = 640,
                RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = modal,
            };
            AddMessageCenterStyles(window);
            try
            {
                window.Show();
                window.Measure(new Size(980, 640));
                window.Arrange(new Rect(0, 0, 980, 640));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border surface = Assert.IsType<Border>(modal.FindControl<Control>("MessageCenterSurface"));
                Grid activity = Assert.IsType<Grid>(modal.FindControl<Control>("SystemActivityRoot"));
                ScrollViewer timeline = Assert.IsType<ScrollViewer>(
                    modal.FindControl<Control>("SystemActivityTimelineViewport"));
                Point surfaceOrigin = Assert.IsType<Point>(surface.TranslatePoint(new Point(), modal));
                Assert.True(surfaceOrigin.X >= 0 && surfaceOrigin.Y >= 0);
                Assert.True(surfaceOrigin.X + surface.Bounds.Width <= 980);
                Assert.True(surfaceOrigin.Y + surface.Bounds.Height <= 640);
                Assert.True(activity.Bounds.Width > 560);
                Assert.True(timeline.Bounds.Height > 220);
                Assert.All(
                    activity.GetVisualDescendants().OfType<Button>().Where(static button => button.IsVisible),
                    button => AssertControlFits(button, activity));
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    _ = Directory.CreateDirectory(outputDirectory);
                    string themeName = useDarkTheme ? "dark" : "light";
                    string languageName = language == ShellLanguage.English ? "en" : "zh-tw";
                    await using FileStream output = File.Create(Path.Combine(
                        outputDirectory,
                        $"system-activity-compact-980x640-{themeName}-{languageName}.png"));
                    frame.Save(output);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    /// <summary>The approved 1536x864 activity-history reference has identical Light/Dark geometry.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MessageCenterActivityReferenceGeometryMatchesInBothThemes(bool useDarkTheme)
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.MessageCenter.OpenCommand.Execute(null);
        viewModel.MessageCenter.ToggleDebugActivityCommand.Execute(null);
        var modal = new MessageCenterModal
        {
            DataContext = viewModel.MessageCenter,
        };
        var window = new Window
        {
            Width = 1536,
            Height = 864,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = modal,
        };
        AddMessageCenterStyles(window);
        try
        {
            window.Show();
            window.Measure(new Size(1536, 864));
            window.Arrange(new Rect(0, 0, 1536, 864));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border surface = Assert.IsType<Border>(modal.FindControl<Control>("MessageCenterSurface"));
            Grid body = Assert.IsType<Grid>(modal.FindControl<Control>("MessageCenterBody"));
            Border navigation = Assert.IsType<Border>(modal.FindControl<Control>("MessageCenterNavigationRail"));
            Grid activity = Assert.IsType<Grid>(modal.FindControl<Control>("SystemActivityRoot"));
            ScrollViewer timeline = Assert.IsType<ScrollViewer>(modal.FindControl<Control>("SystemActivityTimelineViewport"));
            Button close = Assert.IsType<Button>(modal.FindControl<Control>("CloseButton"));

            Assert.InRange(surface.Bounds.Width, 1419.5, 1420.5);
            Assert.InRange(surface.Bounds.Height, 823.5, 824.5);
            Assert.InRange(body.Bounds.Height, 739.5, 740.5);
            Assert.InRange(navigation.Bounds.Width, 237.5, 238.5);
            Assert.Equal(40, close.Bounds.Width);
            Assert.Equal(close.Bounds.Width, close.Bounds.Height);
            Assert.True(activity.Bounds.Width > 1080);
            Assert.True(timeline.Bounds.Height > 420);
            Assert.True(modal.GetVisualDescendants().OfType<Border>()
                .Count(control => control.Classes.Contains("activityRow")) >= 4);

            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                string outputPath = Path.Combine(
                    outputDirectory,
                    $"system-activity-reference-1536x864-{themeName}.png");
                await using FileStream output = File.Create(outputPath);
                frame.Save(output);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void AddMessageCenterStyles(Window window)
    {
        foreach (Uri source in new[]
        {
            ProductionMainWindowStylesUri,
            ProductionButtonStylesUri,
            ProductionVisualStylesUri,
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MessageCenterStyles.axaml"),
        })
        {
            window.Styles.Add(new StyleInclude(source) { Source = source });
        }
    }

    private static void AssertControlFits(Control control, Visual ancestor)
    {
        Point origin = Assert.IsType<Point>(control.TranslatePoint(new Point(), ancestor));
        Assert.True(origin.X >= -0.5, $"{control.GetType().Name} starts at x={origin.X:F1}.");
        Assert.True(
            origin.X + control.Bounds.Width <= ancestor.Bounds.Width + 0.5,
            $"{control.GetType().Name} ends at x={origin.X + control.Bounds.Width:F1} " +
            $"outside {ancestor.Bounds.Width:F1}.");
    }
}
