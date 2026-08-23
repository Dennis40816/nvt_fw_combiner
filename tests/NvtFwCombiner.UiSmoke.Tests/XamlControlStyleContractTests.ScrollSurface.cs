using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Every ordinary vertical content viewport uses one shared scroll-surface owner.</summary>
    [Fact]
    public void OrdinaryVerticalContentViewportsUseTheSharedScrollSurface()
    {
        string[] files =
        [
            "MainWindow.axaml",
            "Resources/MainWindowPageTemplates.axaml",
            "Resources/MainWindowReportAuditTemplates.axaml",
            "Resources/MainWindowReportPanels.axaml",
            "Views/HexEditorPanel.axaml",
            "Views/MessageCenterModal.axaml",
            "Views/OutputDeliveryConfirmationModal.axaml",
            "Views/ReplaceSelectionModal.axaml",
        ];

        int sharedSurfaceCount = 0;
        foreach (string file in files)
        {
            XDocument document = XDocument.Parse(ReadPresentationFile(file));
            XElement[] verticalViewports =
            [
                .. document.Descendants()
                    .Where(element => element.Name.LocalName == "ScrollViewer")
                    .Where(element =>
                        !string.Equals(
                            (string?)element.Attribute("VerticalScrollBarVisibility"),
                            "Disabled",
                            StringComparison.Ordinal)),
            ];

            Assert.All(
                verticalViewports,
                viewport => Assert.Contains(
                    "contentScrollSurface",
                    ((string?)viewport.Attribute("Classes") ?? string.Empty)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.Ordinal));
            sharedSurfaceCount += verticalViewports.Length;
        }

        Assert.Equal(11, sharedSurfaceCount);

        string styles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        string surface = ExtractStyle(styles, "ScrollViewer.contentScrollSurface");
        string verticalBar = ExtractStyle(
            styles,
            "ScrollViewer.contentScrollSurface /template/ ScrollBar:vertical");
        string thumb = ExtractStyle(
            styles,
            "ScrollViewer.contentScrollSurface /template/ ScrollBar:vertical /template/ Thumb");
        string track = ExtractStyle(
            styles,
            "ScrollViewer.contentScrollSurface /template/ ScrollBar:vertical /template/ Rectangle#TrackRect");
        string buttons = ExtractStyle(
            styles,
            "ScrollViewer.contentScrollSurface /template/ ScrollBar:vertical /template/ RepeatButton");
        string hoverThumb = ExtractStyle(
            styles,
            "ScrollViewer.contentScrollSurface /template/ ScrollBar:vertical:pointerover /template/ Thumb");
        string hoverThumbSurface = ExtractStyle(
            styles,
            "ScrollViewer.contentScrollSurface /template/ ScrollBar:vertical /template/ Thumb:pointerover /template/ Border");

        Assert.Contains("AllowAutoHide\" Value=\"False", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("Padding", surface, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"14", verticalBar, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"Transparent", verticalBar, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"6", thumb, StringComparison.Ordinal);
        Assert.Contains("NfcTextDisabledBrush", thumb, StringComparison.Ordinal);
        Assert.Contains("RenderTransform\" Value=\"none", thumb, StringComparison.Ordinal);
        Assert.Contains("Opacity\" Value=\"0", track, StringComparison.Ordinal);
        Assert.Contains("Opacity\" Value=\"0", buttons, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"6", hoverThumb, StringComparison.Ordinal);
        Assert.Contains("NfcTextMutedBrush", hoverThumb, StringComparison.Ordinal);
        Assert.Contains("RenderTransform\" Value=\"none", hoverThumb, StringComparison.Ordinal);
        Assert.Contains("NfcTextMutedBrush", hoverThumbSurface, StringComparison.Ordinal);

        foreach (string specializedViewport in
            new[] { "Views/HexEditorPanel.axaml", "Views/BinInspectorPanel.axaml" })
        {
            XDocument document = XDocument.Parse(ReadPresentationFile(specializedViewport));
            XElement[] manualScrollBars =
            [
                .. document.Descendants()
                    .Where(element => element.Name.LocalName == "ScrollBar"),
            ];
            Assert.NotEmpty(manualScrollBars);
            Assert.All(
                manualScrollBars,
                scrollBar => Assert.DoesNotContain(
                    "contentScrollSurface",
                    ((string?)scrollBar.Attribute("Classes") ?? string.Empty)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.Ordinal));
        }
    }

    /// <summary>The shared gutter keeps content left of one stable 14 px hit target in both themes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedScrollSurfaceReservesTracklessGutterWithoutChangingThumbWidth(bool useDarkTheme)
    {
        var content = new Border
        {
            Height = 520,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "System activity", FontSize = 18 },
                    new Border { Height = 58 },
                    new Border { Height = 58 },
                    new Border { Height = 58 },
                    new Border { Height = 58 },
                },
            },
        };
        var viewport = new ScrollViewer
        {
            Width = 320,
            Height = 220,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };
        viewport.Classes.Add("contentScrollSurface");
        var host = new Window
        {
            Width = 360,
            Height = 260,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = viewport,
        };
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });

        try
        {
            host.Show();
            host.Measure(new Size(360, 260));
            host.Arrange(new Rect(0, 0, 360, 260));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            ScrollBar verticalBar = Assert.Single(
                viewport.GetVisualDescendants().OfType<ScrollBar>(),
                candidate => candidate.Orientation == Avalonia.Layout.Orientation.Vertical &&
                    candidate.Bounds.Height > 0);
            Thumb thumb = Assert.Single(verticalBar.GetVisualDescendants().OfType<Thumb>());
            Point barOrigin = Assert.IsType<Point>(verticalBar.TranslatePoint(default, viewport));
            Point contentOrigin = Assert.IsType<Point>(content.TranslatePoint(default, viewport));

            Assert.False(viewport.AllowAutoHide);
            Assert.Equal(new Thickness(0), viewport.Padding);
            Assert.Equal(14, verticalBar.Bounds.Width);
            Assert.Equal(6, thumb.Bounds.Width);
            Assert.True(contentOrigin.X + content.Bounds.Width <= barOrigin.X + 0.5);

            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                using FileStream output = File.Create(
                    Path.Combine(outputDirectory, $"scroll-surface-option-a-{themeName}.png"));
                frame.Save(output);
            }
        }
        finally
        {
            host.Close();
        }
    }
}
