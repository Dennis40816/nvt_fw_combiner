using Avalonia.Media;
using Avalonia.Styling;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps the dark shell palette distinct and readable instead of reusing light surfaces.</summary>
    [Fact]
    public void DarkThemeUsesDistinctReadableSemanticPalette()
    {
        var app = new App();
        app.Initialize();

        SolidColorBrush lightBackground = ResolveBrush(app, "NfcAppBackgroundBrush", ThemeVariant.Light);
        SolidColorBrush darkBackground = ResolveBrush(app, "NfcAppBackgroundBrush", ThemeVariant.Dark);

        Assert.NotEqual(lightBackground.Color, darkBackground.Color);
        Assert.True(RelativeLuminance(lightBackground.Color) > RelativeLuminance(darkBackground.Color));

        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            AssertContrast(app, theme, "NfcTextStrongBrush", "NfcAppBackgroundBrush", 7.0);
            AssertContrast(app, theme, "NfcTextBrush", "NfcSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcTextMutedBrush", "NfcSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcAccentStrongBrush", "NfcAccentSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcSuccessTextBrush", "NfcSuccessSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcWarningTextBrush", "NfcWarningSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcDangerTextBrush", "NfcDangerSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcTextBrush", "NfcSurfaceSubtleBrush", 4.5);
            AssertContrast(app, theme, "NfcTextSecondaryBrush", "NfcSurfaceSubtleBrush", 4.5);
            AssertContrast(app, theme, "NfcAccentStrongBrush", "NfcAccentSurfaceSubtleBrush", 4.5);
            AssertContrast(app, theme, "NfcAccentStrongBrush", "NfcInfoSurfaceStrongBrush", 4.5);
            AssertContrast(app, theme, "NfcCautionTextBrush", "NfcCautionSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcReferenceInputTextBrush", "NfcReferenceInputSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcControllerInputTextBrush", "NfcControllerInputSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcControllerInputTextBrush", "NfcReferenceInputSurfaceBrush", 4.5);
            AssertContrast(app, theme, "NfcPrimaryActionHoverTextBrush", "NfcPrimaryActionHoverBrush", 4.5);
            AssertContrast(app, theme, "NfcPrimaryActionPressedTextBrush", "NfcPrimaryActionPressedBrush", 4.5);
            Assert.True(
                RelativeLuminance(ResolveBrush(app, "NfcPrimaryActionPressedBrush", theme).Color) <
                RelativeLuminance(ResolveBrush(app, "NfcPrimaryActionHoverBrush", theme).Color),
                $"{theme} primary pressed state must settle below its deep-blue hover.");
        }
    }

    private static SolidColorBrush ResolveBrush(App app, string key, ThemeVariant theme)
    {
        Assert.True(app.TryGetResource(key, theme, out object? resource), $"Missing {theme} resource '{key}'.");
        return Assert.IsType<SolidColorBrush>(resource);
    }

    private static void AssertContrast(
        App app,
        ThemeVariant theme,
        string foregroundKey,
        string backgroundKey,
        double minimum)
    {
        Color foreground = ResolveBrush(app, foregroundKey, theme).Color;
        Color background = ResolveBrush(app, backgroundKey, theme).Color;
        double lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        double darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        double ratio = (lighter + 0.05) / (darker + 0.05);

        Assert.True(
            ratio >= minimum,
            $"{theme} {foregroundKey} on {backgroundKey} contrast was {ratio:F2}:1; expected at least {minimum:F1}:1.");
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linearize(byte component)
        {
            double value = component / 255.0;
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearize(color.R)) +
            (0.7152 * Linearize(color.G)) +
            (0.0722 * Linearize(color.B));
    }
}
