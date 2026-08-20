using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Behaviors;

/// <summary>Links pointer and keyboard emphasis for one memory row and its bar segments.</summary>
public sealed class MemoryCoverageInteractionBehavior : AvaloniaObject
{
    /// <summary>Enables correlated memory-coverage emphasis on the attached control.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<MemoryCoverageInteractionBehavior, Control, bool>("IsEnabled");

    private static readonly AttachedProperty<InteractionLease?> LeaseProperty =
        AvaloniaProperty.RegisterAttached<MemoryCoverageInteractionBehavior, Control, InteractionLease?>(
            "Lease");

    static MemoryCoverageInteractionBehavior()
    {
        _ = IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    private MemoryCoverageInteractionBehavior()
    {
    }

    /// <summary>Gets whether correlated emphasis is enabled.</summary>
    public static bool GetIsEnabled(AvaloniaObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(IsEnabledProperty);
    }

    /// <summary>Sets whether correlated emphasis is enabled.</summary>
    public static void SetIsEnabled(AvaloniaObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.PointerEntered -= Control_OnPointerEntered;
        control.PointerExited -= Control_OnPointerExited;
        control.GotFocus -= Control_OnGotFocus;
        control.LostFocus -= Control_OnLostFocus;
        control.DataContextChanged -= Control_OnDataContextChanged;
        control.DetachedFromVisualTree -= Control_OnDetachedFromVisualTree;
        control.PropertyChanged -= Control_OnPropertyChanged;

        InteractionLease? existing = control.GetValue(LeaseProperty);
        existing?.Clear(control);
        control.ClearValue(LeaseProperty);

        if (e.NewValue is true)
        {
            var lease = new InteractionLease(ResolveState(control));
            _ = control.SetValue(LeaseProperty, lease);
            control.PointerEntered += Control_OnPointerEntered;
            control.PointerExited += Control_OnPointerExited;
            control.GotFocus += Control_OnGotFocus;
            control.LostFocus += Control_OnLostFocus;
            control.DataContextChanged += Control_OnDataContextChanged;
            control.DetachedFromVisualTree += Control_OnDetachedFromVisualTree;
            control.PropertyChanged += Control_OnPropertyChanged;
        }
    }

    private static void Control_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetPointerActive(control, active: true);
        }
    }

    private static void Control_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetPointerActive(control, active: false);
        }
    }

    private static void Control_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetFocusActive(control, active: true);
        }
    }

    private static void Control_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetFocusActive(control, active: false);
        }
    }

    private static void Control_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.MoveTo(control, ResolveState(control));
        }
    }

    private static void Control_OnDetachedFromVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.Clear(control);
        }
    }

    private static void Control_OnPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsEffectivelyEnabledProperty &&
            e.NewValue is false &&
            sender is Control control &&
            control.GetValue(LeaseProperty) is { } lease)
        {
            lease.Clear(control);
        }
    }

    private static MemoryCoverageInteractionState? ResolveState(Control control)
    {
        return control.DataContext switch
        {
            MemoryCoverageSegmentViewModel segment => segment.Interaction,
            MemoryCoverageLogicalItemViewModel item => item.Interaction,
            _ => null,
        };
    }

    private sealed class InteractionLease(MemoryCoverageInteractionState? state)
    {
        private bool _focusActive;
        private bool _pointerActive;
        private MemoryCoverageInteractionState? _state = state;

        internal void SetPointerActive(Control owner, bool active)
        {
            MoveTo(owner, ResolveState(owner));
            _pointerActive = active && owner.IsEffectivelyEnabled;
            _state?.SetPointerActive(owner, _pointerActive);
        }

        internal void SetFocusActive(Control owner, bool active)
        {
            MoveTo(owner, ResolveState(owner));
            _focusActive = active && owner.IsEffectivelyEnabled;
            _state?.SetFocusActive(owner, _focusActive);
        }

        internal void MoveTo(Control owner, MemoryCoverageInteractionState? next)
        {
            if (ReferenceEquals(_state, next))
            {
                return;
            }

            _state?.SetPointerActive(owner, false);
            _state?.SetFocusActive(owner, false);
            _state = next;
            if (owner.IsEffectivelyEnabled)
            {
                _state?.SetPointerActive(owner, _pointerActive);
                _state?.SetFocusActive(owner, _focusActive);
            }
        }

        internal void Clear(Control owner)
        {
            _state?.SetPointerActive(owner, false);
            _state?.SetFocusActive(owner, false);
            _state = null;
            _pointerActive = false;
            _focusActive = false;
        }
    }

}
