// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Measured reference anchors and overflow behavior at the minimum supported 960-pixel width.</summary>
public sealed class RecalculationConfirmLayoutTests
{
    /// <summary>Both themes and languages retain the approved width, padding, order and aligned table columns.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExpandedDialogRetainsTheReferenceAnchors(bool dark, bool chinese)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, dark: dark, chinese: chinese);
        Rect surface = host.Bounds(host.Find<Border>("DialogSurface"));
        Rect title = host.Bounds(host.Find<TextBlock>("DialogTitle"));
        Rect badge = host.Bounds(host.Find<Border>("IcBadge"));
        Rect details = host.Bounds(host.Find<Border>("DetailsPanel"));
        Rect footer = host.Bounds(host.Find<Grid>("ButtonRow"));
        Assert.InRange(surface.Width, 599, 601);
        Assert.InRange(surface.Left, 179, 181);
        Assert.InRange(title.Left - surface.Left, 23, 25);
        Assert.InRange(surface.Right - badge.Right, 23, 25);
        Assert.InRange(details.Left - title.Left, -1, 1);
        Assert.InRange(details.Width, 551, 553);
        Assert.InRange(host.Bounds(host.Find<TextBlock>("StorageOffsets")).Left - details.Left, 147, 149);
        Assert.InRange(host.Bounds(host.Find<TextBlock>("NfcVersion")).Left - details.Left, 147, 149);
        Assert.True(host.Bounds(host.Find<TextBlock>("WhyText")).Top < host.Bounds(host.Find<TextBlock>("StorageOffsets")).Top);
        Assert.True(host.Bounds(host.Find<Border>("CoveringSegmentsWarning")).Bottom < host.Bounds(host.Find<TextBlock>("NfcVersion")).Top);
        Assert.True(details.Bottom < host.Bounds(host.Find<TextBlock>("SavedChoiceNote")).Top);
        Assert.True(host.Bounds(host.Find<TextBlock>("SavedChoiceNote")).Bottom < footer.Top);
        Assert.InRange(host.Bounds(host.Find<Button>("RecalculateButton")).Right - badge.Right, -1, 1);
        Assert.InRange(host.Find<TextBlock>("DialogTitle").FontSize, 15.9, 16.1);
        Assert.All(host.VisibleButtons, button => Assert.InRange(button.Bounds.Height, 31, 33));
        Assert.All<Border>([host.Find<Border>("DialogSurface"), host.Find<Border>("DetailsPanel"),
                host.Find<Border>("IcBadge"), host.Find<Border>("CoveringSegmentsWarning")],
            border => Assert.Equal(new Thickness(2), border.BorderThickness));
    }

    /// <summary>The collapsed state retains the reference's compact surface and left/right action groups.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollapsedDialogKeepsTheReferenceHierarchyAndCompactHeight(bool chinese)
    {
        using var host = new RecalculationConfirmTestHost(chinese: chinese, height: 640);
        Rect surface = host.Bounds(host.Find<Border>("DialogSurface"));
        Rect title = host.Bounds(host.Find<TextBlock>("DialogTitle"));
        Rect disclosure = host.Bounds(host.Find<global::Avalonia.Controls.Primitives.ToggleButton>("DetailsButton"));
        Rect note = host.Bounds(host.Find<TextBlock>("SavedChoiceNote"));
        Rect footer = host.Bounds(host.Find<Grid>("ButtonRow"));
        Assert.InRange(surface.Height, 195, 210);
        Assert.True(title.Bottom < disclosure.Top);
        Assert.True(disclosure.Bottom < note.Top);
        Assert.True(note.Bottom < footer.Top);
        Assert.True(host.Bounds(host.Find<Button>("CancelButton")).Right < host.Bounds(host.Find<Button>("SkipButton")).Left);
        Assert.True(host.Bounds(host.Find<Button>("SkipButton")).Right < host.Bounds(host.Find<Button>("RecalculateButton")).Left);
    }

    /// <summary>Seven host-supplied offsets wrap completely in the table and the caution sentence.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SevenOffsetsWrapWithoutClippingAtMinimumWidth(bool chinese)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese,
            offsets: [0x23C, 0x24C, 0x26C, 0x27C, 0x28C, 0x29C, 0x2AC]);
        TextBlock storage = host.Find<TextBlock>("StorageOffsets");
        TextBlock warning = Assert.Single(host.Find<Border>("CoveringSegmentsWarning").GetVisualDescendants().OfType<TextBlock>());
        Assert.True(storage.TextLayout.TextLines.Count > 1);
        Assert.True(warning.TextLayout.TextLines.Count > 1);
        RecalculationConfirmTestHost.AssertUnclipped(storage);
        RecalculationConfirmTestHost.AssertUnclipped(warning);
        Assert.Equal(Assert.Single(host.Model.CoveringSegments).WarningText, RecalculationConfirmTestHost.InlineText(warning));
        Assert.InRange(host.Bounds(storage).Right, 0, host.Window.ClientSize.Width);
        Assert.InRange(host.Bounds(warning).Right, 0, host.Window.ClientSize.Width);
    }

    /// <summary>Only the middle scrolls in a short window, leaving the heading and every action available.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TallExpandedContentScrollsBetweenFixedTitleAndButtons(bool chinese)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese, height: 360);
        ScrollViewer middle = host.Find<ScrollViewer>("MiddleScroll");
        Rect title = host.Bounds(host.Find<Grid>("TitleRow"));
        Rect buttons = host.Bounds(host.Find<Grid>("ButtonRow"));
        Assert.True(middle.Extent.Height > middle.Viewport.Height);
        Assert.InRange(title.Top, 0, host.Window.ClientSize.Height);
        Assert.InRange(buttons.Bottom, 0, host.Window.ClientSize.Height);
        Assert.True(title.Bottom < host.Bounds(middle).Top);
        Assert.True(host.Bounds(middle).Bottom < buttons.Top);
        middle.Offset = new Vector(0, middle.Extent.Height);
        host.Layout();
        Assert.Equal(title, host.Bounds(host.Find<Grid>("TitleRow")));
        Assert.Equal(buttons, host.Bounds(host.Find<Grid>("ButtonRow")));
        Assert.True(middle.Offset.Y > 0);
        Assert.All(host.VisibleButtons, button => Assert.True(button.IsEffectivelyEnabled));
    }

}
