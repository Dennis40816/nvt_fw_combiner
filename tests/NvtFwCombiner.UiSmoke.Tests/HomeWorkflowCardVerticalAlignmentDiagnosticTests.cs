using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Production Home workflow-card visual geometry regressions.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class HomeWorkflowCardVerticalAlignmentDiagnosticTests
{
    private static readonly Uri ProductionPagesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml");

    /// <summary>
    /// The production Home template keeps every visible title on the optical center of its
    /// existing Open pill without stretching the title's line box to the taller card row.
    /// </summary>
    [AvaloniaFact]
    public async Task ProductionHomeWorkflowCardTitlesShareTheOpenPillCenter()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateProductViewModel(),
            TestContext.Current.CancellationToken);
        var pages = new ResourceInclude(ProductionPagesUri)
        {
            Source = ProductionPagesUri,
        };
        Assert.True(pages.TryGetResource("HomePageTemplate", ThemeVariant.Light, out object? template));
        var page = new ContentControl
        {
            Content = viewModel,
            ContentTemplate = Assert.IsType<IDataTemplate>(template, exactMatch: false),
        };
        Assert.True(Avalonia.Application.Current!.TryGetResource(
            "NfcUiFontFamily",
            ThemeVariant.Light,
            out object? fontResource));
        FontFamily uiFont = Assert.IsType<FontFamily>(fontResource);
        var window = new Window
        {
            Width = 1180,
            Height = 760,
            RequestedThemeVariant = ThemeVariant.Light,
            FontFamily = uiFont,
            Content = page,
        };
        foreach (string stylePath in new[]
        {
            "Styles/MainWindowStyles.axaml",
            "Styles/MainWindowButtonStyles.axaml",
            "Styles/MainWindowVisualStyles.axaml",
        })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/{stylePath}");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }

        try
        {
            window.Show();
            window.Measure(new Size(1180, 760));
            window.Arrange(new Rect(0, 0, 1180, 760));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border[] cards =
            [
                .. page.GetVisualDescendants().OfType<Border>()
                    .Where(static candidate =>
                        candidate.IsVisible && candidate.Classes.Contains("workflowCard")),
            ];
            Assert.Equal(6, cards.Length);

            foreach (Border card in cards)
            {
                TextBlock title = Assert.Single(
                    card.GetVisualDescendants().OfType<TextBlock>(),
                    static candidate => candidate.Classes.Contains("strongText"));
                Border openPill = Assert.Single(
                    card.GetVisualDescendants().OfType<Border>(),
                    static candidate => candidate.Classes.Contains("workflowOpenPill"));
                Point titleOrigin = Assert.IsType<Point>(title.TranslatePoint(default, card));
                Point pillOrigin = Assert.IsType<Point>(openPill.TranslatePoint(default, card));
                double titleCenter = titleOrigin.Y + (title.Bounds.Height / 2);
                double pillCenter = pillOrigin.Y + (openPill.Bounds.Height / 2);

                Assert.Equal(uiFont, title.FontFamily);
                Assert.Equal(VerticalAlignment.Center, title.VerticalAlignment);
                Assert.InRange(
                    Math.Abs(title.Bounds.Height - title.DesiredSize.Height),
                    0,
                    0.5);
                Assert.True(
                    Math.Abs(titleCenter - pillCenter) <= 0.5,
                    $"{title.Text}: title center {titleCenter:F3}, Open center {pillCenter:F3}, " +
                    $"title={title.Bounds}, desired={title.DesiredSize}, card={card.Bounds}.");
            }

            Assert.All(cards, static card => Assert.InRange(card.Bounds.Height, 49.5, 50.5));
        }
        finally
        {
            window.Close();
        }
    }
}
