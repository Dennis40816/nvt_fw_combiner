using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the Hex Editor data outline's enabled and disabled theme roles.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class HexEditorBorderRoleTests
{
    /// <summary>Only disabling the actual panel selects the disabled brush, without changing geometry.</summary>
    [AvaloniaTheory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task DataOutlineUsesItsStateRoleAndRetainsGeometry(bool dark, bool enabled)
    {
        using var workspace = TempWorkspace.Create("hex-border-role");
        string source = workspace.Write("source.bin", [0x10, 0x20]);
        var view = new HexEditorWorkspaceViewModel(
            ShellTextResources.For(ShellLanguage.English),
            new RawBinaryEditorFileSessionFactory());
        await view.LoadAsync(source, TestContext.Current.CancellationToken);
        Assert.True(view.HasDocument);
        var panel = new HexEditorPanel { DataContext = view };
        ThemeVariant theme = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = new Window
        {
            Width = 1100,
            Height = 700,
            RequestedThemeVariant = theme,
            Content = panel,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Grid document = Assert.IsType<Grid>(panel.FindControl<Grid>("HexDocumentSurface"));
            Border outline = document.GetVisualAncestors().OfType<Border>().First();
            Rect outlineBounds = outline.Bounds;
            Rect documentBounds = document.Bounds;
            Assert.True(outlineBounds is { Width: > 0, Height: > 0 });

            panel.IsEnabled = enabled;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(theme, outline.ActualThemeVariant);
            Assert.Equal(enabled, outline.IsEffectivelyEnabled);
            Assert.Same(outline.FindResource(theme, enabled ? "NfcBorderBrush" : "NfcTextDisabledBrush"),
                outline.BorderBrush);
            Assert.Equal(new Thickness(2), outline.BorderThickness);
            Assert.Equal(new Thickness(8), outline.Padding);
            Assert.Equal(new CornerRadius(6), outline.CornerRadius);
            Assert.Equal(outlineBounds, outline.Bounds);
            Assert.Equal(documentBounds, document.Bounds);

            panel.IsEnabled = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(outline.IsEffectivelyEnabled);
            Assert.Same(outline.FindResource(theme, "NfcBorderBrush"), outline.BorderBrush);
            Assert.Equal(outlineBounds, outline.Bounds);
            Assert.Equal(documentBounds, document.Bounds);
        }
        finally
        {
            window.Close();
        }
    }
}
