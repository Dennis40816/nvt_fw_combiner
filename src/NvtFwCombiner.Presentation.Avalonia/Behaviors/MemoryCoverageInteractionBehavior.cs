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

    internal static readonly AttachedProperty<bool> IsRailProperty =
        AvaloniaProperty.RegisterAttached<MemoryCoverageInteractionBehavior, Control, bool>("IsRail");
    internal static readonly AttachedProperty<bool> RailActiveProperty =
        AvaloniaProperty.RegisterAttached<MemoryCoverageInteractionBehavior, Control, bool>("RailActive");

    private static readonly AttachedProperty<InteractionLease?> LeaseProperty =
        AvaloniaProperty.RegisterAttached<MemoryCoverageInteractionBehavior, Control, InteractionLease?>(
            "Lease");

    static MemoryCoverageInteractionBehavior()
    {
        _ = IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        _ = RailActiveProperty.Changed.AddClassHandler<Control>((control, change) => control.Classes.Set("railActive", change.NewValue is true));
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
        control.RemoveHandler(InputElement.PointerMovedEvent, Control_OnPointerInput);
        control.RemoveHandler(InputElement.PointerPressedEvent, Control_OnPointerInput);
        control.RemoveHandler(InputElement.KeyDownEvent, Control_OnKeyDown);
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
            var lease = new InteractionLease(ResolveStates(control));
            _ = control.SetValue(LeaseProperty, lease);
            control.PointerEntered += Control_OnPointerEntered;
            control.PointerExited += Control_OnPointerExited;
            control.AddHandler(InputElement.PointerMovedEvent, Control_OnPointerInput, RoutingStrategies.Tunnel);
            control.AddHandler(InputElement.PointerPressedEvent, Control_OnPointerInput, RoutingStrategies.Tunnel);
            control.AddHandler(InputElement.KeyDownEvent, Control_OnKeyDown, RoutingStrategies.Tunnel);
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
            lease.SetFocusActive(control, e is not FocusChangedEventArgs { NavigationMethod: NavigationMethod.Pointer });
        }
    }

    private static void Control_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetFocusActive(control, active: false);
        }
    }

    private static void Control_OnPointerInput(object? sender, PointerEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetFocusActive(control, active: false);
        }
    }

    private static void Control_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.SetFocusActive(control, control.IsKeyboardFocusWithin);
        }
    }

    private static void Control_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control && control.GetValue(LeaseProperty) is { } lease)
        {
            lease.MoveTo(control, ResolveStates(control));
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

    private static MemoryCoverageInteractionState[] ResolveStates(Control control)
    {
        return control.DataContext switch
        {
            MemoryCoverageSegmentViewModel segment => [segment.Interaction],
            MemoryCoverageLogicalItemViewModel item => [item.Interaction],
            MemoryCoverageBarItem group => [.. group.Slices.Select(static slice => slice.Interaction).Distinct()],
            MemoryFocusPositionViewModel { Lane: { } lane } => [.. lane.Ranges.Select(static slice => slice.Interaction).Distinct()],
            _ => [],
        };
    }

    private sealed class InteractionLease(MemoryCoverageInteractionState[] states)
    {
        private bool _focusActive;
        private bool _pointerActive;
        private MemoryCoverageInteractionState[] _states = states;

        internal void SetPointerActive(Control owner, bool active)
        {
            MoveTo(owner, ResolveStates(owner));
            _pointerActive = active && owner.IsEffectivelyEnabled;
            Publish(owner);
        }

        internal void SetFocusActive(Control owner, bool active)
        {
            MoveTo(owner, ResolveStates(owner));
            _focusActive = active && owner.IsEffectivelyEnabled;
            Publish(owner);
        }

        internal void MoveTo(Control owner, MemoryCoverageInteractionState[] next)
        {
            if (_states.SequenceEqual(next))
            {
                return;
            }

            foreach (MemoryCoverageInteractionState state in _states)
            {
                state.SetPointerActive(owner, false);
                state.SetFocusActive(owner, false);
                state.SetRailActive(owner, false);
            }
            _states = next;
            Publish(owner);
        }

        private void Publish(Control owner)
        {
            bool terminal = owner.DataContext is MemoryCoverageSegmentViewModel or MemoryCoverageLogicalItemViewModel;
            foreach (MemoryCoverageInteractionState state in _states)
            {
                state.SetPointerActive(owner, terminal && _pointerActive && owner.IsEffectivelyEnabled);
                state.SetFocusActive(owner, terminal && _focusActive && owner.IsEffectivelyEnabled);
                state.SetRailActive(owner, owner.GetValue(IsRailProperty) && owner.IsEffectivelyEnabled && (_pointerActive || _focusActive));
            }
        }

        internal void Clear(Control owner)
        {
            MoveTo(owner, []);
            _pointerActive = false;
            _focusActive = false;
        }
    }

}
