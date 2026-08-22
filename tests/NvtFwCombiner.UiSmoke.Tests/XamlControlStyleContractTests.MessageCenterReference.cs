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
        window.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });
        window.Styles.Add(new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        });
        window.Styles.Add(new StyleInclude(ProductionVisualStylesUri)
        {
            Source = ProductionVisualStylesUri,
        });
        Uri messageCenterStylesUri = new(
            "avares://NvtFwCombiner.Presentation.Avalonia/Styles/MessageCenterStyles.axaml");
        window.Styles.Add(new StyleInclude(messageCenterStylesUri)
        {
            Source = messageCenterStylesUri,
        });
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
}
