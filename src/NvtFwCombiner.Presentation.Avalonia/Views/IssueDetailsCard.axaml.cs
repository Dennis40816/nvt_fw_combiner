using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Shared, noninteractive tooltip surface for typed issue explanations.</summary>
public sealed partial class IssueDetailsCard : UserControl
{
    private Popup? _popup;
    private Control? _target;
    private bool _pointerUpdatePending;
    private PixelPoint? _anchorPosition;

    /// <summary>Creates the compact issue surface.</summary>
    public IssueDetailsCard()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _anchorPosition = null;
        _popup = this.GetLogicalAncestors().OfType<Popup>().FirstOrDefault();
        if (_popup is not null)
        {
            _target = _popup.PlacementTarget;
            _popup.Opened += QueuePointerUpdate;
            LayoutUpdated += QueuePointerUpdate;
            _target?.EffectiveViewportChanged += QueuePointerUpdate;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= QueuePointerUpdate;
        _target?.EffectiveViewportChanged -= QueuePointerUpdate;
        _target = null;
        _popup?.Opened -= QueuePointerUpdate;
        _popup = null;
        _anchorPosition = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void QueuePointerUpdate(object? sender, EventArgs e)
    {
        if (_pointerUpdatePending)
        {
            return;
        }
        _pointerUpdatePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _pointerUpdatePending = false;
            UpdatePointer();
        }, DispatcherPriority.Render);
    }

    private void UpdatePointer()
    {
        if (_popup?.PlacementTarget is not Control target || CardLayout.Bounds.Width <= 0 ||
            TopLevel.GetTopLevel(target) is null || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }

        PixelPoint screenAnchor = target.PointToScreen(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2));
        if (_anchorPosition is { } previous && previous != screenAnchor)
        {
            // ToolTip does not reposition an open popup when its target scrolls. Reopen on the next hover/focus.
            ToolTip.SetIsOpen(target, false);
            return;
        }
        _anchorPosition = screenAnchor;
        // Observe the framework's resolved position; never implement a second popup positioner.
        Point anchor = CardLayout.PointToClient(screenAnchor);
        bool above = anchor.Y > CardLayout.Bounds.Height / 2;
        TopPointer.SetCurrentValue(IsVisibleProperty, !above);
        BottomPointer.SetCurrentValue(IsVisibleProperty, above);
        double left = Math.Clamp(anchor.X - (TopPointer.Width / 2), 8, Math.Max(8, CardLayout.Bounds.Width - TopPointer.Width - 8));
        TopPointer.SetCurrentValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        BottomPointer.SetCurrentValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        TopPointer.SetCurrentValue(MarginProperty, new Thickness(left, 0, 0, 0));
        BottomPointer.SetCurrentValue(MarginProperty, new Thickness(left, 0, 0, 0));
    }
}
