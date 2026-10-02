using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Platform;

namespace NvtFwCombiner.Presentation.Avalonia.Behaviors;

/// <summary>One side of the target chosen for a tooltip, as popup positioner parameters.</summary>
/// <param name="Anchor">Edge of the target the popup is attached to.</param>
/// <param name="Gravity">Direction the popup extends from that edge.</param>
/// <param name="Offset">Gap between the target and the popup.</param>
/// <param name="ConstraintAdjustment">Adjustments that keep the popup clear of the target.</param>
internal readonly record struct ToolTipPlacementChoice(
    PopupAnchor Anchor,
    PopupGravity Gravity,
    Point Offset,
    PopupPositionerConstraintAdjustment ConstraintAdjustment);

/// <summary>
/// Places a tall tooltip on a side of its target where it fits, so that it never covers the target.
/// </summary>
/// <remarks>
/// A tooltip that opens over the pointer makes the owning window lose the pointer, which closes the
/// tooltip; the pointer is then over the target again and the tooltip reopens at once. The stock
/// placement slides a tooltip that fits neither below nor above its target until it covers the target,
/// which starts that loop. This placement only slides along the edge it is attached to.
/// </remarks>
public static class ClearOfTargetToolTipPlacement
{
    /// <summary>Placement callback for <c>ToolTip.CustomPopupPlacementCallback</c> with <c>Placement="Custom"</c>.</summary>
    public static CustomPopupPlacementCallback Callback { get; } = Place;

    /// <summary>Chooses below, above, right or left of the target, in that order, by where the popup fits.</summary>
    /// <param name="target">Target bounds.</param>
    /// <param name="popup">Popup size.</param>
    /// <param name="workArea">Usable screen area, in the coordinates of <paramref name="target"/>.</param>
    /// <param name="gap">Distance kept between the target and the popup.</param>
    /// <returns>The side and the adjustments that cannot move the popup over the target.</returns>
    internal static ToolTipPlacementChoice Choose(Rect target, Size popup, Rect workArea, double gap)
    {
        double below = workArea.Bottom - target.Bottom - gap;
        double above = target.Top - workArea.Top - gap;
        if (popup.Height <= below)
        {
            return Below(gap, PopupPositionerConstraintAdjustment.SlideX);
        }

        if (popup.Height <= above)
        {
            return Above(gap, PopupPositionerConstraintAdjustment.SlideX);
        }

        if (popup.Width <= workArea.Right - target.Right - gap)
        {
            return new(PopupAnchor.TopRight, PopupGravity.BottomRight, new Point(gap, 0), PopupPositionerConstraintAdjustment.SlideY);
        }

        if (popup.Width <= target.Left - workArea.Left - gap)
        {
            return new(PopupAnchor.TopLeft, PopupGravity.BottomLeft, new Point(-gap, 0), PopupPositionerConstraintAdjustment.SlideY);
        }

        // No side holds the whole popup: keep it on the roomier vertical side and let it shrink there.
        const PopupPositionerConstraintAdjustment Shrink =
            PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.ResizeY;
        return below >= above ? Below(gap, Shrink) : Above(gap, Shrink);
    }

    private static ToolTipPlacementChoice Below(double gap, PopupPositionerConstraintAdjustment adjustment)
    {
        return new(PopupAnchor.BottomLeft, PopupGravity.BottomRight, new Point(0, gap), adjustment);
    }

    private static ToolTipPlacementChoice Above(double gap, PopupPositionerConstraintAdjustment adjustment)
    {
        return new(PopupAnchor.TopLeft, PopupGravity.TopRight, new Point(0, -gap), adjustment);
    }

    private static void Place(CustomPopupPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        double gap = Math.Max(0, placement.Offset.Y);
        Rect workArea = WorkArea(placement.Target) ?? new Rect(
            double.MinValue / 4, double.MinValue / 4, double.MaxValue / 2, double.MaxValue / 2);
        ToolTipPlacementChoice choice = Choose(placement.AnchorRectangle, placement.PopupSize, workArea, gap);
        placement.Anchor = choice.Anchor;
        placement.Gravity = choice.Gravity;
        placement.Offset = choice.Offset;
        placement.ConstraintAdjustment = choice.ConstraintAdjustment;
    }

    /// <summary>The target screen's working area in the client coordinates of the target's top level.</summary>
    private static Rect? WorkArea(Visual target)
    {
        if (TopLevel.GetTopLevel(target) is not { } topLevel ||
            topLevel.Screens?.ScreenFromVisual(target) is not Screen screen)
        {
            return null;
        }

        PixelRect area = screen.WorkingArea;
        return new Rect(topLevel.PointToClient(area.TopLeft), topLevel.PointToClient(area.BottomRight));
    }
}
