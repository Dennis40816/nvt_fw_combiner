using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Standalone bars share page-scrollbar geometry and still drag in both orientations and themes.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StandaloneScrollbarsUseSharedGeometryAndKeepDragging(bool horizontal, bool dark)
    {
        var bar = new ScrollBar
        {
            Orientation = horizontal ? Avalonia.Layout.Orientation.Horizontal : Avalonia.Layout.Orientation.Vertical,
            Minimum = 0,
            Maximum = 100,
            ViewportSize = 25,
        };
        var window = new Window
        {
            Width = 360,
            Height = 300,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Border { Padding = new Thickness(16), Child = bar },
        };
        try
        {
            window.Show();
            RenderScrollbar();
            Thumb thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            Assert.False(bar.AllowAutoHide);
            Assert.Equal(14, horizontal ? bar.Bounds.Height : bar.Bounds.Width);
            Assert.Equal(6, horizontal ? thumb.Bounds.Height : thumb.Bounds.Width);
            Point start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseMove(start);
            RenderScrollbar();
            Assert.Equal(6, horizontal ? thumb.Bounds.Height : thumb.Bounds.Width);
            Assert.Equal(thumb.FindResource(thumb.ActualThemeVariant, "NfcTextMutedBrush"), thumb.Background);
            Point end = start + (horizontal ? new Vector(40, 0) : new Vector(0, 40));
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            RenderScrollbar();
            Assert.InRange(bar.Value, double.Epsilon, bar.Maximum);
            Assert.Equal(6, horizontal ? thumb.Bounds.Height : thumb.Bounds.Width);
        }
        finally { window.Close(); }

        static void RenderScrollbar()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Existing viewport policies remain intact while scrollbar appearance no longer requires an opt-in class.</summary>
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

        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string surface = ExtractStyle(styles, "ScrollViewer");
        string verticalBar = ExtractStyle(
            styles,
            "ScrollBar:vertical");
        string thumb = ExtractStyle(
            styles,
            "ScrollBar:vertical /template/ Thumb");
        string track = ExtractStyle(
            styles,
            "ScrollBar:vertical /template/ Rectangle#TrackRect");
        string buttons = ExtractStyle(
            styles,
            "ScrollBar:vertical /template/ RepeatButton");
        string hoverThumb = ExtractStyle(
            styles,
            "ScrollBar:vertical:pointerover /template/ Thumb");
        string hoverThumbSurface = ExtractStyle(
            styles,
            "ScrollBar:vertical /template/ Thumb:pointerover /template/ Border");

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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SharedScrollSurfaceReservesTracklessGutterWithoutChangingThumbWidth(bool useDarkTheme, bool explicitClass)
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
        if (explicitClass) { viewport.Classes.Add("contentScrollSurface"); }
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
                    Path.Combine(outputDirectory, $"scroll-surface-{themeName}-class-{explicitClass}.png"));
                frame.Save(output);
            }
        }
        finally
        {
            host.Close();
        }
    }
}
