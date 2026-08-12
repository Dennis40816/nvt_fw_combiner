using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Serializes process-wide Avalonia application resource initialization.</summary>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed class AvaloniaApplicationResourceTests
{
    /// <summary>Loads the application resource tree and resolves every shared visual token.</summary>
    [Fact]
    public void ThemeTokensResolveFromTheApplicationResourceTree()
    {
        var app = new App();
        app.Initialize();

        foreach (string key in XamlControlStyleContractTests.ReadThemeTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value)
                     .Distinct(StringComparer.Ordinal))
        {
            foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                Assert.True(
                    app.TryGetResource(key, theme, out object? resource),
                    $"{theme} theme token '{key}' was not available from Application.Resources.");
                _ = Assert.IsType<SolidColorBrush>(resource);
            }
        }

        foreach (string key in XamlControlStyleContractTests.ReadThemeShadowTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value)
                     .Distinct(StringComparer.Ordinal))
        {
            foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                Assert.True(
                    app.TryGetResource(key, theme, out object? resource),
                    $"{theme} theme token '{key}' was not available from Application.Resources.");
                _ = Assert.IsType<BoxShadows>(resource);
            }
        }

        foreach (string key in XamlControlStyleContractTests.ReadThemeCornerRadiusTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<CornerRadius>(resource);
        }

        foreach (string key in XamlControlStyleContractTests.ReadThemeSpacingTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<double>(resource);
        }

        foreach (string key in XamlControlStyleContractTests.ReadThemeFontSizeTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<double>(resource);
        }

        foreach (string key in XamlControlStyleContractTests.ReadThemeFontFamilyTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<FontFamily>(resource);
        }
    }
}
