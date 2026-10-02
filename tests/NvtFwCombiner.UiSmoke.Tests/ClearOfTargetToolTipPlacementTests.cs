using Avalonia;
using Avalonia.Controls.Primitives.PopupPositioning;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>A tall tooltip is never placed over its target, whichever side has room.</summary>
public sealed class ClearOfTargetToolTipPlacementTests
{
    private const double Gap = 20;
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);
    private static readonly Size Cell = new(160, 46);

    /// <summary>The first side that holds the whole tooltip is used, in the order below, above, right, left.</summary>
    /// <param name="left">Left edge of the target.</param>
    /// <param name="top">Top edge of the target.</param>
    /// <param name="popupWidth">Tooltip width.</param>
    /// <param name="popupHeight">Tooltip height.</param>
    /// <param name="anchor">Expected target edge.</param>
    /// <param name="gravity">Expected growth direction.</param>
    /// <param name="adjustment">Expected allowed adjustment.</param>
    [Theory]
    [InlineData(400, 100, 560, 300, PopupAnchor.BottomLeft, PopupGravity.BottomRight, PopupPositionerConstraintAdjustment.SlideX)]
    [InlineData(400, 900, 560, 300, PopupAnchor.TopLeft, PopupGravity.TopRight, PopupPositionerConstraintAdjustment.SlideX)]
    [InlineData(400, 500, 560, 760, PopupAnchor.TopRight, PopupGravity.BottomRight, PopupPositionerConstraintAdjustment.SlideY)]
    [InlineData(1700, 500, 560, 760, PopupAnchor.TopLeft, PopupGravity.BottomLeft, PopupPositionerConstraintAdjustment.SlideY)]
    public void TheFirstSideThatHoldsTheTooltipIsChosen(
        double left,
        double top,
        double popupWidth,
        double popupHeight,
        PopupAnchor anchor,
        PopupGravity gravity,
        PopupPositionerConstraintAdjustment adjustment)
    {
        var target = new Rect(new Point(left, top), Cell);

        ToolTipPlacementChoice choice =
            ClearOfTargetToolTipPlacement.Choose(target, new Size(popupWidth, popupHeight), WorkArea, Gap);

        Assert.Equal(anchor, choice.Anchor);
        Assert.Equal(gravity, choice.Gravity);
        Assert.Equal(adjustment, choice.ConstraintAdjustment);
    }

    /// <summary>
    /// For every cell position and tooltip size, the placed tooltip stays clear of the target, so the pointer on
    /// the target is never covered; this is what the stock sliding placement did not guarantee.
    /// </summary>
    [Fact]
    public void ThePlacedTooltipNeverCoversItsTarget()
    {
        double[] heights = [120, 300, 520, 760, 1000, 1400];
        double[] widths = [240, 560, 1900];
        for (double left = 0; left <= WorkArea.Width - Cell.Width; left += 80)
        {
            for (double top = 0; top <= WorkArea.Height - Cell.Height; top += 46)
            {
                var target = new Rect(new Point(left, top), Cell);
                foreach (double height in heights)
                {
                    foreach (double width in widths)
                    {
                        var popup = new Size(width, height);
                        ToolTipPlacementChoice choice = ClearOfTargetToolTipPlacement.Choose(target, popup, WorkArea, Gap);
                        Rect placed = Position(target, popup, choice);

                        Assert.False(
                            placed.Intersects(target),
                            $"target={target}; popup={popup}; choice={choice}; placed={placed}");
                        Assert.True(placed.Height > 0 || height > WorkArea.Height, $"No room left for {popup} at {target}.");
                    }
                }
            }
        }
    }

    /// <summary>A tooltip that fits nowhere stays on the roomier vertical side and never moves onto the target.</summary>
    [Fact]
    public void ATooltipThatFitsNowhereStaysBesideTheTarget()
    {
        var popup = new Size(1900, 1000);

        // More room below: the positioner shortens the tooltip at the bottom of the screen.
        var upper = new Rect(new Point(880, 300), Cell);
        ToolTipPlacementChoice below = ClearOfTargetToolTipPlacement.Choose(upper, popup, WorkArea, Gap);
        Assert.Equal(PopupAnchor.BottomLeft, below.Anchor);
        Assert.Equal(
            PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.ResizeY,
            below.ConstraintAdjustment);
        Rect placedBelow = Position(upper, popup, below);
        Assert.False(placedBelow.Intersects(upper));
        Assert.Equal(WorkArea.Bottom, placedBelow.Bottom);

        // More room above: resizing would move the top edge down onto the target, so only sliding along X is allowed.
        var lower = new Rect(new Point(880, 736), Cell);
        ToolTipPlacementChoice above = ClearOfTargetToolTipPlacement.Choose(lower, popup, WorkArea, Gap);
        Assert.Equal(PopupAnchor.TopLeft, above.Anchor);
        Assert.Equal(PopupPositionerConstraintAdjustment.SlideX, above.ConstraintAdjustment);
        Rect placedAbove = Position(lower, popup, above);
        Assert.False(placedAbove.Intersects(lower));
        Assert.Equal(lower.Top - Gap, placedAbove.Bottom);
    }

    /// <summary>
    /// The rectangle Avalonia's managed positioner produces for a choice. It follows that positioner's order:
    /// attach, slide along X, slide along Y, then resize along Y (which first moves a top edge above the screen
    /// down to the screen edge and only then shortens the bottom).
    /// </summary>
    private static Rect Position(Rect target, Size popup, ToolTipPlacementChoice choice)
    {
        double anchorX = choice.Anchor.HasFlag(PopupAnchor.Right) ? target.Right : target.Left;
        double anchorY = choice.Anchor.HasFlag(PopupAnchor.Bottom) ? target.Bottom : target.Top;
        double x = (choice.Gravity.HasFlag(PopupGravity.Left) ? anchorX - popup.Width : anchorX) + choice.Offset.X;
        double y = (choice.Gravity.HasFlag(PopupGravity.Top) ? anchorY - popup.Height : anchorY) + choice.Offset.Y;
        double height = popup.Height;
        if (choice.ConstraintAdjustment.HasFlag(PopupPositionerConstraintAdjustment.SlideX))
        {
            x = Math.Max(x, WorkArea.Left);
            if (x + popup.Width > WorkArea.Right)
            {
                x = WorkArea.Right - popup.Width;
            }
        }

        if (choice.ConstraintAdjustment.HasFlag(PopupPositionerConstraintAdjustment.SlideY))
        {
            y = Math.Max(y, WorkArea.Top);
            if (y + height > WorkArea.Bottom)
            {
                y = WorkArea.Bottom - height;
            }
        }

        if (choice.ConstraintAdjustment.HasFlag(PopupPositionerConstraintAdjustment.ResizeY))
        {
            double top = y < WorkArea.Top ? WorkArea.Top : y;
            double resized = top + height > WorkArea.Bottom ? WorkArea.Bottom - top : height;
            if (resized > 0)
            {
                y = top;
                height = resized;
            }
        }

        return new Rect(x, y, popup.Width, height);
    }
}
