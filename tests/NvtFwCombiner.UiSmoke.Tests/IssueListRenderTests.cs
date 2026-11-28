// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Synthetic direction-A card captures; the surrounding screen remains unwired.</summary>
public sealed class IssueListRenderTests
{
    /// <summary>Direction a card renders without clipped text.</summary>
    [AvaloniaTheory]
    [InlineData(280, false, false)]
    [InlineData(360, false, false)]
    [InlineData(280, true, false)]
    [InlineData(360, true, false)]
    [InlineData(280, false, true)]
    [InlineData(360, false, true)]
    [InlineData(280, true, true)]
    [InlineData(360, true, true)]
    public void DirectionACardRendersWithoutClippedText(int width, bool dark, bool chinese)
    {
        using var host = new IssueListTestHost(width: width, dark: dark, chinese: chinese, selectedKey: "past-end");

        Assert.InRange(Math.Abs(host.List.Bounds.Width - width), 0, 1);
        AssertTextFits(host);
        AssertReferenceAnchors(host);
        Assert.True(host.Details("past-end").IsEffectivelyVisible);
        Assert.DoesNotContain(host.List.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Selected · shown on the layout · details open");
        Capture(host, $"issue-list-{(dark ? "dark" : "light")}-{width}-{(chinese ? "zh-TW" : "en")}-selected");

        host.List.SelectedKey = null;
        host.Layout();

        AssertTextFits(host);
        Assert.False(host.Details("past-end").IsEffectivelyVisible);
        Capture(host, $"issue-list-{(dark ? "dark" : "light")}-{width}-{(chinese ? "zh-TW" : "en")}-collapsed");
    }

    /// <summary>Long supplied sentences chips and details wrap.</summary>
    [AvaloniaTheory]
    [InlineData(280, false)]
    [InlineData(360, false)]
    [InlineData(280, true)]
    [InlineData(360, true)]
    public void LongSuppliedSentencesChipsAndDetailsWrap(int width, bool dark)
    {
        var item = new IssueListItem("long", IssueListSeverity.Error,
            "這是 host 提供的完整句子，控制項必須在窄欄位中換行並保留所有資訊。 A long supplied sentence remains fully readable.",
            [new string('L', 120), "location supplied without parsing or firmware interpretation"], 12,
            [new string('D', 120), "Detail lines stay visible only for the selected row."]);
        using var host = new IssueListTestHost(new IssueListModel([item]), width: width, dark: dark, selectedKey: "long");

        AssertTextFits(host);
        Assert.True(Assert.Single(host.Row("long").GetVisualDescendants().OfType<TextBlock>(),
            text => text.Classes.Contains("issueListSentence")).TextLayout.TextLines.Count > 1);
        Assert.Contains(host.List.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == item.LocationChips[0]);
        Assert.Contains(host.List.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == item.DetailLines[0]);
        Capture(host, $"issue-list-{(dark ? "dark" : "light")}-{width}-long-text");
    }

    /// <summary>Many rows scroll inside the card and arrows reach the last row.</summary>
    [AvaloniaFact]
    public void ManyRowsScrollInsideTheCardAndArrowsReachTheLastRow()
    {
        IssueListItem[] items = [.. Enumerable.Range(0, 40).Select(index =>
            IssueListTestHost.Item(index.ToString(CultureInfo.InvariantCulture)))];
        using var host = new IssueListTestHost(new IssueListModel(items), width: 280);
        Assert.True(host.Row("0").Focus(NavigationMethod.Tab));

        string[] visited = WalkDown(host, items.Length);

        Assert.Equal(items.Select(item => item.Key), visited);
        Assert.True(host.Scroll.Extent.Height > host.Scroll.Viewport.Height);
        Assert.True(host.Scroll.Offset.Y > 0);
        Assert.InRange(host.Bounds(host.Row("39")).Bottom, host.Bounds(host.Scroll).Top, host.Bounds(host.Scroll).Bottom + 1);
        Assert.InRange(host.List.Bounds.Height, 1, 480);
        host.Press(Key.Enter, PhysicalKey.Enter);
        Assert.Equal("39", host.List.SelectedKey);
    }

    /// <summary>Keyboard focus paints a visible two pixel outline with three to one contrast.</summary>
    [AvaloniaTheory]
    [InlineData(false, "past-end")]
    [InlineData(true, "past-end")]
    [InlineData(false, "overlap")]
    [InlineData(true, "overlap")]
    public void KeyboardFocusPaintsAVisibleTwoPixelOutlineWithThreeToOneContrast(bool dark, string key)
    {
        using var host = new IssueListTestHost(dark: dark);
        Button row = host.Row(key);
        Assert.True(row.Focus(NavigationMethod.Tab));
        host.Layout();

        Border outline = Assert.Single(row.GetVisualDescendants().OfType<Border>(), border => border.Name == "FocusOutline");
        Assert.True(outline.IsEffectivelyVisible);
        Assert.Equal(new Thickness(2), outline.BorderThickness);
        Color focus = Solid(outline.BorderBrush);
        AssertContrast(focus, Solid(row.Background), 3);
        Rect bounds = host.Bounds(outline);
        Color painted = ReadPixel(host.Window, new Point(bounds.Center.X, bounds.Top + 1));
        Assert.InRange(Math.Abs(painted.R - focus.R), 0, 2);
        Assert.InRange(Math.Abs(painted.G - focus.G), 0, 2);
        Assert.InRange(Math.Abs(painted.B - focus.B), 0, 2);
        Capture(host, $"issue-list-{(dark ? "dark" : "light")}-360-focus-{key}");
    }

    /// <summary>Severity uses distinct shapes and error tint and bar.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeverityUsesDistinctShapesAndErrorTintAndBar(bool dark)
    {
        using var host = new IssueListTestHost(dark: dark);
        Button error = host.Row("past-end");
        Button warning = host.Row("overlap");
        ShapePath errorIcon = Assert.Single(error.GetVisualDescendants().OfType<ShapePath>());
        ShapePath warningIcon = Assert.Single(warning.GetVisualDescendants().OfType<ShapePath>());
        Border bar = Assert.Single(error.GetVisualDescendants().OfType<Border>(), border => border.Name == "ErrorBar");

        // The circle and the triangle differ in outline, so their geometry bounds differ.
        Assert.NotEqual(errorIcon.Data!.Bounds, warningIcon.Data!.Bounds);
        Assert.True(bar.IsEffectivelyVisible);
        Assert.Equal(2, bar.Bounds.Width);
        Assert.False(Assert.Single(warning.GetVisualDescendants().OfType<Border>(), border => border.Name == "ErrorBar").IsEffectivelyVisible);
        Assert.True(error.TryFindResource("NfcDangerSurfaceBrush", error.ActualThemeVariant, out object? tint));
        Assert.Same(tint, error.Background);
        AssertContrast(Solid(error.Foreground), Solid(error.Background), 4.5);
        AssertContrast(Solid(warning.Foreground), Solid(warning.Background), 4.5);
    }

    internal static void AssertTextFits(IssueListTestHost host)
    {
        Rect card = host.Bounds(host.List);
        Assert.True(card.Width > 0);
        Assert.True(card.Height > 0);
        Assert.All(host.List.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible), text =>
        {
            Rect bounds = host.Bounds(text);
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
            Assert.NotEmpty(text.TextLayout.TextLines);
            Assert.InRange(bounds.Left, card.Left - 1, card.Right + 1);
            Assert.InRange(bounds.Right, card.Left - 1, card.Right + 1);
            Assert.InRange(text.TextLayout.Width, 0, text.Bounds.Width + 1);
            Assert.InRange(text.TextLayout.Height, 0, text.Bounds.Height + 1);
            Assert.All(text.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
        });
    }

    private static void AssertReferenceAnchors(IssueListTestHost host)
    {
        TextBlock title = host.List.FindControl<TextBlock>("IssueListTitle")!;
        Button error = host.Row("past-end");
        ShapePath icon = Assert.Single(error.GetVisualDescendants().OfType<ShapePath>());
        TextBlock sentence = Assert.Single(error.GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("issueListSentence"));
        Assert.InRange(Math.Abs(host.Bounds(title).Left - host.Bounds(host.List).Left - 16), 0, 1);
        Assert.InRange(Math.Abs(host.Bounds(icon).Left - host.Bounds(title).Left), 0, 1);
        Assert.InRange(Math.Abs(host.Bounds(sentence).Left - host.Bounds(icon).Right - 12), 0, 1);
        Assert.InRange(Math.Abs(host.Bounds(host.Details("past-end")).Left - host.Bounds(sentence).Left), 0, 1);
        Assert.True(host.Bounds(title).Bottom < host.Bounds(error).Top);
    }

    private static string[] WalkDown(IssueListTestHost host, int count)
    {
        var visited = new List<string>();
        for (int index = 0; index < count; index++)
        {
            visited.Add(Assert.IsType<string>(Assert.Single(host.Rows, row => row.IsFocused).CommandParameter));
            host.Press(Key.Down, PhysicalKey.ArrowDown);
        }
        return [.. visited];
    }

    private static Color Solid(IBrush? brush)
    {
        return Assert.IsType<ISolidColorBrush>(brush, exactMatch: false).Color;
    }

    private static void AssertContrast(Color foreground, Color background, double minimum)
    {
        double first = Luminance(foreground);
        double second = Luminance(background);
        double ratio = (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        Assert.True(ratio >= minimum, $"{foreground} on {background}: {ratio:F2}:1, required {minimum:F1}:1.");
    }

    private static double Luminance(Color color)
    {
        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }

    private static double Linear(byte component)
    {
        double value = component / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static Color ReadPixel(Window window, Point point)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(global::Avalonia.Platform.PixelFormat.Rgba8888, frame.Format);
        var region = new PixelRect((int)Math.Floor(point.X * window.RenderScaling),
            (int)Math.Floor(point.Y * window.RenderScaling), 1, 1);
        byte[] pixel = new byte[4];
        GCHandle handle = GCHandle.Alloc(pixel, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(region, handle.AddrOfPinnedObject(), pixel.Length, pixel.Length);
        }
        finally
        {
            handle.Free();
        }
        return Color.FromArgb(pixel[3], pixel[0], pixel[1], pixel[2]);
    }

    private static void Capture(IssueListTestHost host, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        _ = Directory.CreateDirectory(directory);
        using WriteableBitmap? frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.SavePng(Path.Combine(directory, name + ".png"));
    }
}
