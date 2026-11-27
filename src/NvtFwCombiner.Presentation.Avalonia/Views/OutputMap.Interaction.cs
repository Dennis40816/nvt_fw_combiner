// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class OutputMap
{
    private const string StripTargetClass = "outputMapStripTarget";

    private Button Target(string key, object? content, string automationId, string description, bool previews = true)
    {
        var button = new Button { Content = content, Command = _pin, CommandParameter = key };
        button.Classes.Add("outputMapTarget");
        UpdateTarget(button);
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, description);
        ToolTip.SetTip(button, description);
        // As in the reference, the zoom strip never previews itself: that would redraw it under the pointer or focus.
        if (previews)
        {
            button.PointerEntered += (_, _) => _preview.Execute(key);
            button.PointerExited += (_, _) => ClearPreview(key);
            button.GotFocus += (_, _) => _preview.Execute(key);
            button.LostFocus += (_, _) => ClearPreview(key);
        }
        else { button.Classes.Add(StripTargetClass); }
        button.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Pointer.Type == PointerType.Touch) { _pin.Execute(key); }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        return button;
    }

    private void ClearPreview(string key)
    {
        if (_zoomRebuildDepth == 0 && string.Equals(HoveredKey, key, StringComparison.Ordinal)) { _preview.Execute(FocusedKey); }
    }

    private string? FocusedKey => this.GetVisualDescendants().OfType<Button>()
        .FirstOrDefault(b => b.IsFocused && !b.Classes.Contains(StripTargetClass))?.CommandParameter as string;

    private void RefreshInteraction()
    {
        if (_parts is null) { return; }
        foreach (Button button in this.GetVisualDescendants().OfType<Button>())
        {
            UpdateTarget(button);
        }
        RenderZoom();
    }

    private void UpdateTarget(Button button)
    {
        string? key = button.CommandParameter as string;
        button.Classes.Set("pinned", key is not null && string.Equals(SelectedKey, key, StringComparison.Ordinal));
        button.Classes.Set("preview", key is not null && string.Equals(HoveredKey, key, StringComparison.Ordinal));
        button.Classes.Set("reducedMotion", ReducedMotion);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_zoomRebuildDepth == 0 && !IsPointerOver) { _preview.Execute(FocusedKey); }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _clear.Execute(null);
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            Button[] targets = [.. this.GetVisualDescendants().OfType<Button>().Where(b => b.Focusable && b.IsEffectivelyVisible)];
            int index = Array.FindIndex(targets, b => b.IsFocused);
            if (targets.Length == 0 || index < 0) { return; }
            int step = e.Key is Key.Left or Key.Up ? targets.Length - 1 : 1;
            int next = (index + step) % targets.Length;
            if (e.Key == Key.Home) { next = 0; }
            else if (e.Key == Key.End) { next = targets.Length - 1; }
            _ = targets[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }
}
