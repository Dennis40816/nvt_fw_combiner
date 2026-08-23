using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Dropdown states use shared theme tokens without changing selector geometry.</summary>
    [Fact]
    public void ComboBoxItemStatesOwnOnlyTheApprovedBlueTreatment()
    {
        string styles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        string comboBox = ExtractStyle(styles, "ComboBox");
        string focus = ExtractStyle(styles, "ComboBox:focus-visible");
        string pointerOver = ExtractStyle(
            styles,
            "ComboBoxItem:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string selected = ExtractStyle(
            styles,
            "ComboBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter");
        string selectedPointerOver = ExtractStyle(
            styles,
            "ComboBoxItem:selected:pointerover /template/ ContentPresenter#PART_ContentPresenter");

        Assert.Contains("BorderThickness\" Value=\"2", comboBox, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", focus, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", pointerOver, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", pointerOver, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"Transparent", selected, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", selected, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceSubtleBrush", selectedPointerOver, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", selectedPointerOver, StringComparison.Ordinal);

        foreach (string state in new[] { pointerOver, selected, selectedPointerOver })
        {
            Assert.Contains("BorderBrush\" Value=\"Transparent", state, StringComparison.Ordinal);
            Assert.DoesNotContain("Padding", state, StringComparison.Ordinal);
            Assert.DoesNotContain("MinHeight", state, StringComparison.Ordinal);
            Assert.DoesNotContain("MinWidth", state, StringComparison.Ordinal);
            Assert.DoesNotContain("Width\"", state, StringComparison.Ordinal);
            Assert.DoesNotContain("Height\"", state, StringComparison.Ordinal);
            Assert.DoesNotContain("CornerRadius", state, StringComparison.Ordinal);
            Assert.DoesNotContain("BorderThickness", state, StringComparison.Ordinal);
        }
    }

    /// <summary>Light and Dark resolve Option A hover and selected states through the shared template.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComboBoxItemPointerStatesRenderTheApprovedBlueTreatment(bool useDarkTheme)
    {
        var hoveredItem = new ComboBoxItem
        {
            Content = "NT51950",
            Width = 240,
            Height = 40,
        };
        var selectedItem = new ComboBoxItem
        {
            Content = "NT51990",
            Width = 240,
            Height = 40,
            IsSelected = true,
        };
        var items = new StackPanel
        {
            Margin = new Thickness(20),
            Children = { hoveredItem, selectedItem },
        };
        var host = new Window
        {
            Width = 280,
            Height = 120,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = items,
        };
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });

        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            ContentPresenter hoverPresenter = GetComboBoxItemPresenter(hoveredItem);
            ContentPresenter selectedPresenter = GetComboBoxItemPresenter(selectedItem);
            Rect hoverBounds = hoveredItem.Bounds;
            Rect selectedBounds = selectedItem.Bounds;

            Point hoverPoint = Assert.IsType<Point>(hoveredItem.TranslatePoint(
                new Point(hoveredItem.Bounds.Width / 2, hoveredItem.Bounds.Height / 2),
                host));
            host.MouseMove(hoverPoint, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.True(hoveredItem.IsPointerOver);
            AssertBrushMatchesThemeToken(hoverPresenter.Background, "NfcAccentSurfaceBrush", useDarkTheme);
            AssertBrushMatchesThemeToken(hoverPresenter.Foreground, "NfcAccentStrongBrush", useDarkTheme);
            AssertBrushMatchesThemeToken(selectedPresenter.Foreground, "NfcAccentStrongBrush", useDarkTheme);
            Assert.Equal(Colors.Transparent, Assert.IsType<ISolidColorBrush>(selectedPresenter.Background, exactMatch: false).Color);
            Assert.Equal(hoverBounds, hoveredItem.Bounds);
            Assert.Equal(selectedBounds, selectedItem.Bounds);

            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                using FileStream output = File.Create(
                    Path.Combine(outputDirectory, $"dropdown-option-a-{themeName}.png"));
                frame.Save(output);
            }

            Point selectedPoint = Assert.IsType<Point>(selectedItem.TranslatePoint(
                new Point(selectedItem.Bounds.Width / 2, selectedItem.Bounds.Height / 2),
                host));
            host.MouseMove(selectedPoint, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.True(selectedItem.IsPointerOver);
            AssertBrushMatchesThemeToken(
                selectedPresenter.Background,
                "NfcAccentSurfaceSubtleBrush",
                useDarkTheme);
            AssertBrushMatchesThemeToken(selectedPresenter.Foreground, "NfcAccentStrongBrush", useDarkTheme);
            Assert.Equal(hoverBounds, hoveredItem.Bounds);
            Assert.Equal(selectedBounds, selectedItem.Bounds);
        }
        finally
        {
            host.Close();
        }
    }

    private static ContentPresenter GetComboBoxItemPresenter(ComboBoxItem item)
    {
        return Assert.Single(
            item.GetVisualDescendants().OfType<ContentPresenter>(),
            static candidate => candidate.Name == "PART_ContentPresenter");
    }

    private static void AssertBrushMatchesThemeToken(
        IBrush? actual,
        string token,
        bool useDarkTheme)
    {
        Assert.True(Avalonia.Application.Current!.TryGetResource(
            token,
            useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            out object? expected));
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(expected).Color,
            Assert.IsType<ISolidColorBrush>(actual, exactMatch: false).Color);
    }
}
