using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NvtFwCombiner.Presentation.Avalonia.Behaviors;

/// <summary>Exposes an existing pointer tooltip from keyboard focus without moving focus.</summary>
public sealed class FocusToolTipBehavior : AvaloniaObject
{
    /// <summary>Enables focus and Escape handling for the attached control's existing tooltip.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<FocusToolTipBehavior, Control, bool>("IsEnabled");

    static FocusToolTipBehavior()
    {
        _ = IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    private FocusToolTipBehavior()
    {
    }

    /// <summary>Gets whether focus tooltip behavior is enabled.</summary>
    public static bool GetIsEnabled(AvaloniaObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(IsEnabledProperty);
    }

    /// <summary>Sets whether focus tooltip behavior is enabled.</summary>
    public static void SetIsEnabled(AvaloniaObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.GotFocus -= Control_OnGotFocus;
        control.LostFocus -= Control_OnLostFocus;
        control.KeyDown -= Control_OnKeyDown;
        if (e.NewValue is true)
        {
            control.GotFocus += Control_OnGotFocus;
            control.LostFocus += Control_OnLostFocus;
            control.KeyDown += Control_OnKeyDown;
        }
        else
        {
            ToolTip.SetIsOpen(control, false);
        }
    }

    private static void Control_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            ToolTip.SetIsOpen(control, true);
        }
    }

    private static void Control_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            ToolTip.SetIsOpen(control, false);
        }
    }

    private static void Control_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Control control && e.Key == Key.Escape)
        {
            ToolTip.SetIsOpen(control, false);
        }
    }
}
