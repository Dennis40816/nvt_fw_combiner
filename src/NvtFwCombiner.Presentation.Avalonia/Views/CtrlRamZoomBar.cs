// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using static NvtFwCombiner.Presentation.Avalonia.Views.OutputLayoutPartVisuals;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>An inline CtrlRAM lane with exact geometry; host rows own the selected block details.</summary>
public sealed class CtrlRamZoomBar : UserControl
{
    /// <summary>Defines the immutable lane display snapshot supplied by the presentation adapter.</summary>
    public static readonly StyledProperty<CtrlRamZoomData?> DataProperty =
        AvaloniaProperty.Register<CtrlRamZoomBar, CtrlRamZoomData?>(nameof(Data));
    /// <summary>Defines the selected physical slice index; -1 clears selection.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CtrlRamZoomBar, int>(nameof(SelectedIndex), -1,
            defaultBindingMode: BindingMode.TwoWay, validate: static index => index >= -1);
    /// <summary>Defines hover emphasis, independently of selection.</summary>
    public static readonly StyledProperty<int> HoveredIndexProperty =
        AvaloniaProperty.Register<CtrlRamZoomBar, int>(nameof(HoveredIndex), -1,
            defaultBindingMode: BindingMode.TwoWay, validate: static index => index >= -1);
    /// <summary>Defines the shell or component resource bundle.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<CtrlRamZoomBar, object?>(nameof(Labels));

    // UI thread only. The control holds visual elements and commands, never workflow state.
    private readonly RelayCommand<int> _select;
    private readonly RelayCommand<int> _hover;
    private readonly Parts? _parts;

    /// <summary>Creates an inline view with immediate, reduced-motion-safe state changes.</summary>
    public CtrlRamZoomBar()
    {
        _select = new RelayCommand<int>(index =>
        {
            if (CanSelect(index)) { SetCurrentValue(SelectedIndexProperty, index); }
        }, CanSelect);
        _hover = new RelayCommand<int>(index => SetCurrentValue(HoveredIndexProperty, index));
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
        var position = new Button
        {
            Classes = { "layoutPartPosition" },
            TabIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Command = new RelayCommand(() =>
            {
                _select.Execute(0);
                Button[] targets = NavigationTargets();
                if (targets.Length > 0) { _ = targets[0].Focus(NavigationMethod.Directional); }
            }, () => Data is { Slices.Count: > 0 } && IsEffectivelyEnabled),
        };
        var rail = new Canvas { Name = "CtrlRamZoomRail", Height = 34, ClipToBounds = true };
        var tiny = new WrapPanel { Name = "CtrlRamTinyTargets" };
        Resource(tiny, WrapPanel.ItemSpacingProperty, "NfcSpace8");
        Resource(tiny, WrapPanel.LineSpacingProperty, "NfcSpace4");
        StackPanel root = Stack();
        var addresses = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        TextBlock start = Text(string.Empty, "layoutPartAddress");
        TextBlock end = Text(string.Empty, "layoutPartAddress");
        end.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(end, 1);
        addresses.Children.Add(start);
        addresses.Children.Add(end);
        root.Children.Add(position);
        root.Children.Add(new Border { Name = "CtrlRamPositionUnderline", Height = 2, Classes = { "layoutPartUnderline" } });
        root.Children.Add(new Border { Child = rail, Classes = { "layoutPartTrack" }, ClipToBounds = true });
        root.Children.Add(addresses);
        root.Children.Add(tiny);
        _parts = new(root, position, rail, tiny, start, end);
        Content = root;
        rail.SizeChanged += (_, _) => LayoutSlices();
        Rebuild();
    }

    /// <summary>Gets or sets the published lane snapshot; null collapses the inner content.</summary>
    public CtrlRamZoomData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <inheritdoc cref="SelectedIndexProperty" />
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    /// <inheritdoc cref="HoveredIndexProperty" />
    public int HoveredIndex { get => GetValue(HoveredIndexProperty); set => SetValue(HoveredIndexProperty, value); }
    /// <inheritdoc cref="LabelsProperty" />
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    private OutputLayoutPartsTextResources Texts => OutputLayoutPartsTextResources.From(Labels);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_parts is null) { return; }
        if (change.Property == DataProperty)
        {
            SetCurrentValue(HoveredIndexProperty, -1);
            Rebuild();
        }
        else if (change.Property == LabelsProperty) { RefreshText(); }
        else if (change.Property == SelectedIndexProperty) { RefreshTargets(); }
        else if (change.Property == HoveredIndexProperty) { RefreshTargets(); }
        else if (change.Property == IsEffectivelyEnabledProperty || change.Property == IsVisibleProperty)
        {
            if (!IsEffectivelyEnabled || !IsEffectivelyVisible) { _hover.Execute(-1); }
            _select.NotifyCanExecuteChanged();
            (_parts.Position.Command as IRelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SetCurrentValue(HoveredIndexProperty, -1);
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        if (_parts is not { } parts) { return; }
        int focusedIndex = FocusedIndex();
        parts.Rail.Children.Clear();
        parts.Tiny.Children.Clear();
        parts.Root.IsVisible = Data is { Slices.Count: > 0 };
        if (Data is { } data)
        {
            for (int index = 0; index < data.Slices.Count; index++)
            {
                CtrlRamZoomSlice slice = data.Slices[index];
                var fills = new Canvas { IsHitTestVisible = false };
                foreach (CtrlRamZoomFill fill in slice.Fills)
                {
                    var block = new Border { DataContext = fill };
                    Resource(block, Border.BackgroundProperty, fill.ResourceKey);
                    if (fill.IsKept)
                    {
                        var hatch = new Border();
                        Resource(hatch, Border.BackgroundProperty, "LayoutPartKeptHatch");
                        block.Child = hatch;
                    }
                    fills.Children.Add(block);
                }
                TextBlock label = Text(slice.Label, "layoutPartFillLabel");
                var content = new Grid { Children = { fills, label }, ClipToBounds = true };
                Button button = Target(index, content, "Slice");
                button.Classes.Add("layoutPartSlice");
                parts.Rail.Children.Add(button);
                Button tiny = Target(index, Text(slice.Label, "layoutPartBody"), "Tiny");
                tiny.Classes.Add("layoutPartTiny");
                parts.Tiny.Children.Add(tiny);
            }
        }
        RefreshText();
        LayoutSlices();
        _select.NotifyCanExecuteChanged();
        (parts.Position.Command as IRelayCommand)?.NotifyCanExecuteChanged();
        RestoreFocus(focusedIndex);
    }

    private Button Target(int index, Control content, string kind)
    {
        var button = new Button
        {
            Content = content,
            Command = _select,
            CommandParameter = index,
            TabIndex = index + 1,
            Classes = { "layoutPartTarget" },
        };
        Identify(button, $"CtrlRamZoom.{kind}.{index}", Data!.Slices[index].AccessibleName);
        button.PointerEntered += (_, _) => _hover.Execute(index);
        button.PointerExited += (_, _) => { if (HoveredIndex == index) { _hover.Execute(-1); } };
        return button;
    }

    private void LayoutSlices()
    {
        if (_parts is not { } parts || Data is not { } data) { return; }
        int focusedIndex = FocusedIndex();
        double width = parts.Rail.Bounds.Width;
        double length = data.EndExclusive - data.Start;
        for (int index = 0; index < data.Slices.Count; index++)
        {
            CtrlRamZoomSlice slice = data.Slices[index];
            var button = (Button)parts.Rail.Children[index];
            double left = width * (slice.Start - data.Start) / length;
            double right = width * (slice.EndExclusive - data.Start) / length;
            Canvas.SetLeft(button, left);
            button.Width = Math.Max(0, right - left);
            button.Height = parts.Rail.Height;
            var content = (Grid)button.Content!;
            var fills = (Canvas)content.Children[0];
            foreach (Border block in fills.Children.Cast<Border>())
            {
                var fill = (CtrlRamZoomFill)block.DataContext!;
                Canvas.SetLeft(block, width * (fill.Start - slice.Start) / length);
                block.Width = width * (fill.EndExclusive - fill.Start) / length;
                block.Height = parts.Rail.Height;
            }
            var label = (TextBlock)content.Children[1];
            label.IsVisible = true;
            label.Measure(Size.Infinity);
            bool fits = label.DesiredSize.Width <= button.Width && button.Width >= 24;
            label.IsVisible = fits;
            button.Focusable = fits;
            AutomationProperties.SetAccessibilityView(button, fits ? AccessibilityView.Control : AccessibilityView.Raw);
            parts.Tiny.Children[index].IsVisible = !fits;
        }
        parts.Tiny.IsVisible = parts.Tiny.Children.Any(static child => child.IsVisible);
        RestoreFocus(focusedIndex);
    }

    private void RefreshText()
    {
        if (_parts is not { } parts || Data is not { } data) { return; }
        parts.Position.Content = Text($"{data.PositionLabel} · {Texts.Zoom} · {data.SizeLabel} · {data.AddressSpace}");
        Identify(parts.Position, "CtrlRamZoom.Position", $"{data.PositionLabel} · {Texts.Zoom} · {data.AddressSpace} · {data.RangeLabel}");
        global::Avalonia.Automation.AutomationProperties.SetHelpText(parts.Position, Texts.PositionHint);
        parts.Start.Text = FormattableString.Invariant($"0x{data.Start:X5}");
        parts.End.Text = FormattableString.Invariant($"0x{data.EndExclusive - 1:X5}");
        RefreshTargets();
    }

    private void RefreshTargets()
    {
        if (_parts is not { } parts) { return; }
        foreach (Button target in parts.Rail.Children.Concat(parts.Tiny.Children).Cast<Button>())
        {
            int index = (int)target.CommandParameter!;
            target.Classes.Set("selected", index == SelectedIndex);
            target.Classes.Set("highlighted", index == HoveredIndex);
        }
    }

    private bool CanSelect(int index)
    {
        return IsEffectivelyEnabled && (index == -1 || (Data is { } data && index >= 0 && index < data.Slices.Count));
    }

    private int FocusedIndex()
    {
        if (_parts is not { } parts) { return -1; }
        Button? focused = parts.Rail.Children.Concat(parts.Tiny.Children).Cast<Button>()
            .FirstOrDefault(static target => target.IsFocused);
        return focused is null ? -1 : (int)focused.CommandParameter!;
    }

    private void RestoreFocus(int index)
    {
        if (index < 0 || _parts is not { } parts) { return; }
        Button[] targets = NavigationTargets();
        Button target = index < targets.Length ? targets[index] : parts.Position;
        if (!target.IsFocused) { _ = target.Focus(NavigationMethod.Directional); }
    }

    private Button[] NavigationTargets()
    {
        return _parts is not { } parts
            ? []
            : [.. parts.Rail.Children.Select((child, index) =>
                (Button)(parts.Tiny.Children[index].IsVisible ? parts.Tiny.Children[index] : child))];
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (!IsEffectivelyEnabled || _parts is null) { return; }
        if (e.Key == Key.Escape && Data is { } data && SelectedIndex >= 0 && SelectedIndex < data.Slices.Count)
        {
            _select.Execute(-1);
            _hover.Execute(-1);
            _ = _parts.Position.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            Button[] targets = NavigationTargets();
            if (targets.Length == 0) { return; }
            int current = Array.FindIndex(targets, static target => target.IsFocused);
            int next = e.Key == Key.Home ? 0
                : e.Key == Key.End ? targets.Length - 1
                : e.Key is Key.Left or Key.Up ? Math.Max(0, current - 1)
                : Math.Min(targets.Length - 1, current + 1);
            _ = targets[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    private sealed record Parts(StackPanel Root, Button Position, Canvas Rail, WrapPanel Tiny, TextBlock Start, TextBlock End);
}
