using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

internal static class ModalInitialFocus
{
    internal static void Register(UserControl modal, Func<Control?> target)
    {
        Window? owner = null;
        Control? returnFocus = null;
        bool ownsFocus = false;
        int opening = 0;
        Visual[] visibilityHosts = [];
        modal.AttachedToVisualTree += (_, _) =>
        {
            visibilityHosts = [.. modal.GetVisualAncestors()];
            foreach (Visual host in visibilityHosts) { host.PropertyChanged += OnVisibilityChanged; }
            UpdateFocus();
        };
        modal.PropertyChanged += OnVisibilityChanged;
        modal.DetachedFromVisualTree += (_, _) =>
        {
            foreach (Visual host in visibilityHosts) { host.PropertyChanged -= OnVisibilityChanged; }
            visibilityHosts = [];
            RestoreFocus();
        };

        void OnVisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.IsVisibleProperty) { UpdateFocus(); }
        }

        void UpdateFocus()
        {
            if (!modal.IsEffectivelyVisible || !modal.IsAttachedToVisualTree())
            {
                RestoreFocus();
                return;
            }
            if (owner is not null || TopLevel.GetTopLevel(modal) is not Window window) { return; }
            Control? previous = window.FocusManager?.GetFocusedElement() as Control;
            int request = ++opening;
            Dispatcher.UIThread.Post(() =>
            {
                if (request != opening || owner is not null || !modal.IsEffectivelyVisible ||
                    !modal.IsAttachedToVisualTree() || !ReferenceEquals(TopLevel.GetTopLevel(modal), window))
                {
                    return;
                }
                IInputElement? current = window.FocusManager?.GetFocusedElement();
                if (!ReferenceEquals(current, previous) &&
                    (current is not Control focused || (!ReferenceEquals(focused, modal) && !modal.IsVisualAncestorOf(focused)))) { return; }
                if (target()?.Focus(NavigationMethod.Tab) != true) { return; }
                owner = window;
                returnFocus = previous;
                ownsFocus = true;
                window.AddHandler(InputElement.GotFocusEvent, TrackFocus, handledEventsToo: true);
            }, DispatcherPriority.Input);
        }

        void TrackFocus(object? sender, FocusChangedEventArgs e)
        {
            ownsFocus = e.NewFocusedElement is Control control &&
                (ReferenceEquals(control, modal) || modal.IsVisualAncestorOf(control));
        }

        void RestoreFocus()
        {
            if (owner is not { } window) { return; }
            Control? previous = returnFocus;
            bool restore = ownsFocus;
            int request = opening;
            window.RemoveHandler(InputElement.GotFocusEvent, TrackFocus);
            owner = null;
            returnFocus = null;
            ownsFocus = false;
            Dispatcher.UIThread.Post(() =>
            {
                if (!restore || request != opening || (modal.IsEffectivelyVisible && modal.IsAttachedToVisualTree()) ||
                    !window.IsVisible || previous is not { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } ||
                    !previous.IsAttachedToVisualTree() || !ReferenceEquals(TopLevel.GetTopLevel(previous), window))
                {
                    return;
                }
                IInputElement? current = window.FocusManager?.GetFocusedElement();
                if (current is null || (current is Control control &&
                    (ReferenceEquals(control, modal) || modal.IsVisualAncestorOf(control))))
                {
                    _ = previous.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
    }
}
