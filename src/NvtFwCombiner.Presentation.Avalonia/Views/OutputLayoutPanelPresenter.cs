// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Owns only the standalone panel's UI-thread interaction and visual lifetimes.</summary>
internal sealed class OutputLayoutPanelPresenter
{
    private readonly OutputLayoutPanel _owner;
    private readonly Canvas _bar;
    private readonly Canvas _pins;
    private readonly Dictionary<EntryKey, Entry> _entries = [];
    private readonly Dictionary<OutputLayoutGroupKind, OutputLayoutDisclosure> _groups = [];
    private readonly RelayCommand<KeyEventArgs> _navigate;
    private EntryKey? _hovered;

    internal OutputLayoutPanelPresenter(OutputLayoutPanel owner)
    {
        _owner = owner;
        _navigate = new(Navigate);
        _owner.Focusable = true;
        KeyboardNavigation.SetIsTabStop(_owner, false);
        _bar = Find<Canvas>("LayoutBar");
        _pins = Find<Canvas>("LayoutPins");
        _bar.SizeChanged += (_, _) => ArrangeMap();
        _pins.SizeChanged += (_, _) => ArrangeMap();
        _owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        _owner.GotFocus += (_, _) => RefreshHighlight();
        _owner.LostFocus += (_, _) => RefreshHighlight();
        _owner.Loaded += (_, _) => ArrangeMap();
        Rebuild();
    }

    private OutputLayoutPanelText Text => new((_owner.Labels as ShellTextResources) ?? _owner.Model?.Text ?? ShellTextResources.For(ShellLanguage.English));
    private T Find<T>(string name) where T : Control
    {
        return _owner.FindControl<T>(name)!;
    }

    private Control? Focused => _owner.IsKeyboardFocusWithin
        ? TopLevel.GetTopLevel(_owner)?.FocusManager?.GetFocusedElement() as Control : null;

    internal void Rebuild()
    {
        var focus = Focused?.Tag as FocusAnchor;
        bool hadFocus = _owner.IsKeyboardFocusWithin;
        var previous = _entries.ToDictionary(static pair => pair.Key,
            static pair => (Expanded: pair.Value.Disclosure.IsExpanded, Technical: pair.Value.Technical?.IsExpanded == true));
        var groups = _groups.ToDictionary(static pair => pair.Key, static pair => pair.Value.IsExpanded);
        _entries.Clear();
        _groups.Clear();
        _hovered = null;
        _bar.Children.Clear();
        _pins.Children.Clear();
        Find<StackPanel>("LayoutGroups").Children.Clear();
        Find<TextBlock>("LayoutTitle").Text = Text.Title;
        OutputLayoutPanelModel? model = _owner.Model;
        Find<TextBlock>("LayoutCapacity").Text = model?.CapacityLabel;
        Find<TextBlock>("LayoutStart").Text = model?.StartAddress;
        Find<TextBlock>("LayoutEnd").Text = model?.EndAddress;
        TextBlock issues = Find<TextBlock>("LayoutIssues");
        issues.Text = Text.Issues(model?.WarningCount ?? 0, model?.ErrorCount ?? 0);
        issues.IsVisible = model is { WarningCount: > 0 } or { ErrorCount: > 0 };
        issues.Classes.Set("error", model is { ErrorCount: > 0 });
        StackPanel inputErrors = Find<StackPanel>("LayoutInputErrors");
        inputErrors.Children.Clear();
        foreach (string reason in model?.InputErrors ?? [])
        {
            inputErrors.Children.Add(OutputLayoutVisuals.Text(reason, "captionText"));
        }
        inputErrors.IsVisible = inputErrors.Children.Count > 0;
        TextBlock hint = Find<TextBlock>("LayoutSharedHint");
        hint.Text = model?.SharedInputHint;
        hint.IsVisible = !string.IsNullOrEmpty(hint.Text);
        TextBlock summary = Find<TextBlock>("LayoutSummary");
        summary.Text = model?.SummaryText;
        summary.IsVisible = !string.IsNullOrEmpty(summary.Text);
        TextBlock range = Find<TextBlock>("LayoutRange");
        range.Text = model?.RangeLabel;
        range.IsVisible = model is { Notice: null } && !string.IsNullOrEmpty(range.Text);
        Expander context = Find<Expander>("LayoutContext");
        context.Header = Text.Shell.MemoryProcessingDetailsLabel;
        context.IsVisible = summary.IsVisible || range.IsVisible;
        BuildNotice(model?.Notice);
        Find<StackPanel>("LayoutMap").IsVisible = model is { Notice: null, Segments.Count: > 0 };
        if (model is { Notice: null }) { BuildGroups(model, previous, groups); BuildMap(model); }
        ArrangeMap();
        RefreshMotion();
        if (hadFocus)
        {
            Control? replacement = focus is null ? null : _owner.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(control => Equals(control.Tag, focus) && control.Focusable);
            if (replacement is null && focus?.Key is { } key && _entries.TryGetValue(key, out Entry? surviving))
            {
                replacement = surviving.Disclosure.Header;
            }
            if (replacement is not null) { RevealParents(replacement); _ = replacement.Focus(NavigationMethod.Tab); }
            else { _ = _owner.Focus(); }
        }
    }

    private void BuildNotice(OutputLayoutNotice? notice)
    {
        StackPanel target = Find<StackPanel>("LayoutNotice");
        target.Children.Clear();
        target.IsVisible = notice is not null;
        if (notice is null) { return; }
        target.Children.Add(OutputLayoutVisuals.Text(notice.Title, "bodyEmphasisText"));
        StackPanel reason = OutputLayoutVisuals.Stack();
        reason.Children.Add(OutputLayoutVisuals.Text(notice.Reason, "captionText"));
        var frame = new Panel { MinHeight = 34, Children = { OutputLayoutVisuals.Inset(reason, "NfcSpace8") } };
        var outline = new global::Avalonia.Controls.Shapes.Rectangle
        {
            IsHitTestVisible = false,
            StrokeThickness = 1,
            StrokeDashArray = notice.Kind == OutputLayoutNoticeKind.Unavailable ? null : [3, 3],
        };
        OutputLayoutVisuals.Resource(outline, global::Avalonia.Controls.Shapes.Shape.StrokeProperty,
            notice.Kind switch
            {
                OutputLayoutNoticeKind.Blocked => "NfcCriticalBorderBrush",
                OutputLayoutNoticeKind.Unavailable => "NfcWarningAccentStrongBrush",
                OutputLayoutNoticeKind.Waiting => "NfcMemoryPanelBorderBrush",
                _ => throw new ArgumentOutOfRangeException(nameof(notice)),
            });
        frame.Children.Insert(0, outline);
        target.Children.Add(frame);
    }

    private void BuildGroups(OutputLayoutPanelModel model, Dictionary<EntryKey, (bool Expanded, bool Technical)> previous,
        Dictionary<OutputLayoutGroupKind, bool> groups)
    {
        foreach (OutputLayoutGroupKind kind in Enum.GetValues<OutputLayoutGroupKind>())
        {
            OutputLayoutPanelRow[] rows = [.. model.Rows.Where(row => row.Group == kind)];
            if (rows.Length == 0) { continue; }
            StackPanel body = OutputLayoutVisuals.Stack("NfcSpace4");
            foreach (OutputLayoutPanelRow row in rows) { body.Children.Add(BuildRow(row, previous).Disclosure.Root); }
            Grid header = OutputLayoutVisuals.Grid("Auto,*,Auto");
            Control pattern = kind == OutputLayoutGroupKind.Kept
                ? OutputLayoutVisuals.Swatch(_owner, rows[0].Members[0]) : new Border { Width = 16, Height = 10 };
            if (pattern is Border && kind != OutputLayoutGroupKind.Kept)
            {
                OutputLayoutVisuals.Resource(pattern, Border.BackgroundProperty, "NfcMemoryNeutralFillBrush");
            }
            header.Children.Add(pattern);
            TextBlock title = OutputLayoutVisuals.Text(Text.Group(kind, model.WriteDisposition), "bodyEmphasisText");
            Grid.SetColumn(title, 1);
            header.Children.Add(title);
            TextBlock count = OutputLayoutVisuals.Text(FormattableString.Invariant($"{rows.Sum(static row => row.Members.Count)}"), "captionText");
            Grid.SetColumn(count, 2);
            header.Children.Add(count);
            var group = new OutputLayoutDisclosure(header, body, Text.Group(kind, model.WriteDisposition), $"OutputLayout.Group.{kind}",
                groups.GetValueOrDefault(kind, kind != OutputLayoutGroupKind.NotWritten), chevronFirst: true);
            group.Header.Tag = new FocusAnchor(null, kind, "group");
            _groups.Add(kind, group);
            StackPanel container = OutputLayoutVisuals.Stack();
            container.Children.Add(new Border { Classes = { "outputLayoutDivider" } });
            container.Children.Add(group.Root);
            Find<StackPanel>("LayoutGroups").Children.Add(container);
        }
    }

    private Entry BuildRow(OutputLayoutPanelRow row, Dictionary<EntryKey, (bool Expanded, bool Technical)> previous)
    {
        EntryKey key = RowKey(row);
        MemoryCoverageSegmentViewModel segment = row.Members[0];
        StackPanel body = OutputLayoutVisuals.Stack();
        OutputLayoutDisclosure? technical = null;
        if (row.Members.Count > 1)
        {
            for (int index = 0; index < row.Members.Count; index++)
            {
                body.Children.Add(OutputLayoutVisuals.Indent(BuildRow(new(row.FirstPin + index, [row.Members[index]], row.Group), previous).Disclosure.Root));
            }
        }
        else
        {
            var details = new OutputLayoutDetails(_owner, Text, key, segment, previous.GetValueOrDefault(key).Technical);
            body.Children.Add(details.Root);
            technical = details.Technical;
        }
        Grid header = OutputLayoutVisuals.Grid("Auto,Auto,*,Auto");
        header.Children.Add(OutputLayoutVisuals.Pin(row.PinLabel));
        Control swatch = OutputLayoutVisuals.Swatch(_owner, segment);
        Grid.SetColumn(swatch, 1);
        header.Children.Add(swatch);
        StackPanel labels = OutputLayoutVisuals.Stack("NfcSpace2");
        labels.Children.Add(OutputLayoutVisuals.Text(row.Members.Count == 1 ? segment.DisplayTitle : Text.SmallBlocks(row.Members.Count), "bodyEmphasisText"));
        labels.Children.Add(OutputLayoutVisuals.Text(Address(_owner.Model!, row), "technicalValue"));
        Grid.SetColumn(labels, 2);
        header.Children.Add(labels);
        string states = string.Join(" · ", row.Members.Select(Text.Badge).Where(static state => state.Length > 0).Distinct());
        TextBlock badge = OutputLayoutVisuals.Text(states, "outputLayoutBadge");
        MemoryDiagnosticSeverity severity = row.Members.Max(static member => member.DiagnosticSeverity);
        badge.Classes.Set("warning", severity == MemoryDiagnosticSeverity.Warning);
        badge.Classes.Set("error", severity == MemoryDiagnosticSeverity.Error);
        badge.IsVisible = !string.IsNullOrEmpty(badge.Text);
        Grid.SetColumn(badge, 3);
        header.Children.Add(badge);
        var highlight = new Border { Classes = { "outputLayoutRow" }, Child = header };
        string accessible = row.Members.Count == 1 ? segment.AccessibleDetail :
            $"{Text.SmallBlocks(row.Members.Count)}. {string.Join(". ", row.Members.Select(static member => member.AccessibleDetail))}";
        var disclosure = new OutputLayoutDisclosure(highlight, body, $"{row.PinLabel}. {accessible}",
            $"OutputLayout.Row.{row.PinLabel}", previous.GetValueOrDefault(key).Expanded);
        disclosure.Header.DataContext = row;
        disclosure.Header.Tag = new FocusAnchor(key, null, "row");
        var entry = new Entry(key, row, disclosure, technical, highlight);
        _entries.Add(key, entry);
        WireHover(disclosure.Root, key);
        return entry;
    }

    private static string Address(OutputLayoutPanelModel model, OutputLayoutPanelRow row)
    {
        MemoryCoverageSegmentViewModel head = row.Members[0];
        MemoryCoverageSegmentViewModel tail = row.Members[^1];
        OutputLayoutDisplayRange? first = model.RangeFor(head);
        OutputLayoutDisplayRange? last = model.RangeFor(tail);
        return row.Members.Count == 1 ? head.AddressRangeLabel
            : first is not null && last is not null && string.Equals(first.Space, last.Space, StringComparison.Ordinal)
                ? FormattableString.Invariant($"0x{first.Start:X5}-0x{last.EndExclusive - 1:X5}")
                : $"{head.AddressRangeLabel} / {tail.AddressRangeLabel}";
    }

    private void BuildMap(OutputLayoutPanelModel model)
    {
        OutputLayoutVisuals.Name(_bar, $"{Text.Title}. {model.RangeLabel}", "OutputLayout.Bar");
        AutomationProperties.SetAccessibilityView(_bar, AccessibilityView.Content);
        foreach (MemoryCoverageSegmentViewModel segment in model.Segments)
        {
            if (model.RangeFor(segment) is null) { continue; }
            // Supporting regions such as headers keep their place on the scale but stay blank, as on the old rail.
            if (!segment.IsPrimaryContent) { _bar.Children.Add(new Border { DataContext = segment, Height = 34 }); continue; }
            Panel content = new()
            {
                Children = { OutputLayoutVisuals.Fill(_owner, segment) },
            };
            if (OutputLayoutPanelRow.GroupFor(segment) != OutputLayoutGroupKind.NotWritten)
            {
                TextBlock label = OutputLayoutVisuals.Text(segment.DisplayTitle, "bodyEmphasisText");
                label.TextWrapping = TextWrapping.NoWrap;
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                OutputLayoutVisuals.Resource(label, TextBlock.ForegroundProperty,
                    segment.FillRole is MemoryCoverageFillRole.Neutral or MemoryCoverageFillRole.Kept ? "NfcTextStrongBrush" : "NfcSurfaceBrush");
                content.Children.Add(label);
            }
            var block = new Border
            {
                Classes = { "outputLayoutBlock", "memoryCoverageLinkedRow" },
                Child = content,
                DataContext = segment,
                ClipToBounds = true,
                Focusable = true,
                Height = 34,
            };
            Entry? entry = _entries.Values.FirstOrDefault(item => item.Row.Members.Count == 1 &&
                Parts(item.Row.Members[0]).Contains(segment));
            if (entry is not null) { WireMapTarget(block, entry.Key, "block"); }
            else { block.Focusable = false; }
            OutputLayoutVisuals.Name(block, segment.AccessibleDetail, $"OutputLayout.Block.{_bar.Children.Count}");
            _bar.Children.Add(block);
        }
        foreach (OutputLayoutPanelRow row in model.Rows)
        {
            Border pin = OutputLayoutVisuals.Pin(row.PinLabel);
            pin.DataContext = row;
            pin.Focusable = true;
            pin.Classes.Add("memoryCoverageLinkedRow");
            WireMapTarget(pin, RowKey(row), "pin");
            OutputLayoutVisuals.Name(pin, $"{row.PinLabel}. {string.Join(". ", row.Members.Select(static item => item.AccessibleDetail))}",
                $"OutputLayout.Pin.{row.PinLabel}");
            _pins.Children.Add(pin);
        }
    }

    private static IEnumerable<MemoryCoverageSegmentViewModel> Parts(MemoryCoverageSegmentViewModel segment)
    {
        return segment.DisplayParts.Count == 0 ? [segment] : segment.DisplayParts;
    }

    private void WireMapTarget(Control target, EntryKey key, string role)
    {
        target.Tag = new FocusAnchor(key, null, role);
        AutomationProperties.SetAccessibilityView(target, AccessibilityView.Content);
        AutomationProperties.SetControlTypeOverride(target, AutomationControlType.Button);
        WireHover(target, key);
        var open = new RelayCommand(() => Open(key));
        target.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(target).Properties.IsLeftButtonPressed) { open.Execute(null); e.Handled = true; }
        };
        target.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { open.Execute(null); }
            e.Handled |= e.Key is Key.Enter or Key.Space;
        };
        // A button toggles on Space release, so opening on press would move focus there and close the row again.
        target.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Space) { open.Execute(null); e.Handled = true; }
        };
    }

    private void Open(EntryKey key)
    {
        Entry entry = _entries[key];
        RevealParents(entry.Disclosure.Header);
        entry.Disclosure.SetExpanded(true);
        entry.Disclosure.Focus();
        RefreshHighlight();
    }

    private void RevealParents(Control target)
    {
        foreach (OutputLayoutDisclosure disclosure in _groups.Values.Concat(_entries.Values.Select(static entry => entry.Disclosure)))
        {
            if (!ReferenceEquals(target, disclosure.Header) && disclosure.Owns(target)) { disclosure.SetExpanded(true); }
        }
    }

    private void WireHover(Control target, EntryKey key)
    {
        target.PointerEntered += (_, _) => { _hovered = key; RefreshHighlight(); };
        target.PointerExited += (_, _) =>
        {
            // Leaving a child row keeps its parent lit while the pointer is still inside the parent.
            if (Equals(_hovered, key))
            {
                _hovered = _entries.Values.FirstOrDefault(entry => !Equals(entry.Key, key) && entry.Disclosure.Root.IsPointerOver)?.Key;
            }
            RefreshHighlight();
        };
    }

    private void RefreshHighlight()
    {
        EntryKey? focus = (Focused?.Tag as FocusAnchor)?.Key;
        foreach (Entry entry in _entries.Values)
        {
            entry.Highlight.Classes.Set("active", Matches(entry.Key, _hovered) || Matches(entry.Key, focus));
        }
        foreach (Control target in _bar.Children.Concat(_pins.Children))
        {
            EntryKey? key = (target.Tag as FocusAnchor)?.Key;
            target.Classes.Set("active", key is not null && (Matches(key, _hovered) || Matches(key, focus)));
        }
    }

    private bool Matches(EntryKey key, EntryKey? active)
    {
        return active is not null
            && _entries.TryGetValue(key, out Entry? entry) && _entries.TryGetValue(active, out Entry? activeEntry)
            && entry.Row.Members.Select(OutputLayoutRowIdentity.For)
                .Intersect(activeEntry.Row.Members.Select(OutputLayoutRowIdentity.For)).Any();
    }

    internal void RefreshMotion()
    {
        foreach (Control control in _owner.GetVisualDescendants().OfType<Control>())
        {
            control.Classes.Set("reducedMotion", _owner.ReducedMotion);
        }
    }

    private void ArrangeMap()
    {
        if (_owner.Model is not { Notice: null } model) { return; }
        OutputLayoutDisplayRange[] ranges = [.. model.Segments.Select(model.RangeFor).OfType<OutputLayoutDisplayRange>()];
        bool hasScale = ranges.Length > 0 && ranges.Select(static range => range.Space).Distinct(StringComparer.Ordinal).Count() == 1;
        _bar.IsVisible = hasScale;
        _pins.IsVisible = hasScale;
        if (!hasScale) { return; }
        double start = ranges.Min(static range => range.Start);
        double length = ranges.Max(static range => range.EndExclusive) - start;
        foreach (Control block in _bar.Children)
        {
            var segment = (MemoryCoverageSegmentViewModel)block.DataContext!;
            OutputLayoutDisplayRange range = model.RangeFor(segment)!;
            Canvas.SetLeft(block, _bar.Bounds.Width * (range.Start - start) / length);
            block.Width = _bar.Bounds.Width * (range.EndExclusive - range.Start) / length;
            if (block is Border { Child: Panel content } && content.Children[^1] is TextBlock label)
            {
                label.IsVisible = block.Width >= Math.Max(80, _bar.Bounds.Width * 0.12);
            }
        }
        foreach (Control pin in _pins.Children)
        {
            var row = (OutputLayoutPanelRow)pin.DataContext!;
            if (model.RangeFor(row.Members[0]) is not { } first || model.RangeFor(row.Members[^1]) is not { } last) { pin.IsVisible = false; continue; }
            long left = first.Start;
            long right = last.EndExclusive;
            pin.Measure(Size.Infinity);
            double width = Math.Max(18, pin.DesiredSize.Width);
            pin.Width = width;
            Canvas.SetLeft(pin, Math.Clamp((_pins.Bounds.Width * ((left + ((right - left) / 2d) - start) / length)) - (width / 2),
                0, Math.Max(0, _pins.Bounds.Width - width)));
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) { return; }
        _navigate.Execute(e);
    }

    private void Navigate(KeyEventArgs? e)
    {
        if (e is null) { return; }
        Control? focused = Focused;
        Entry? entry = _entries.Values.FirstOrDefault(item => item.Disclosure.Owns(focused));
        OutputLayoutDisclosure? disclosure = entry?.Technical?.Owns(focused) == true ? entry.Technical : entry?.Disclosure;
        disclosure ??= _groups.Values.FirstOrDefault(group => ReferenceEquals(group.Header, focused));
        if ((e.Key is Key.Enter or Key.Space) && disclosure is not null)
        {
            disclosure.Toggle.Execute(null); e.Handled = true;
        }
        else if ((e.Key is Key.Right or Key.Left or Key.Escape) && disclosure is not null)
        {
            bool expand = e.Key == Key.Right;
            if (disclosure.IsExpanded != expand)
            {
                disclosure.SetExpanded(expand);
                // Collapsing from inside the body would otherwise leave focus on a hidden control.
                if (!expand) { disclosure.Focus(); }
                e.Handled = true;
            }
            else if (expand && disclosure.Body.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(static target => target.IsEffectivelyVisible) is { } child)
            {
                _ = child.Focus(NavigationMethod.Directional); e.Handled = true;
            }
            else if (e.Key == Key.Left && entry is not null)
            {
                OutputLayoutDisclosure? parent = ReferenceEquals(disclosure, entry.Technical) ? entry.Disclosure
                    : _entries.Values.FirstOrDefault(item => !ReferenceEquals(item, entry) &&
                        item.Disclosure.Owns(entry.Disclosure.Header))?.Disclosure;
                (parent ?? _groups[entry.Row.Group]).Focus();
                e.Handled = true;
            }
        }
        else if (e.Key is Key.Up or Key.Down or Key.Home or Key.End)
        {
            Control[] targets = [.. _owner.GetVisualDescendants().OfType<Button>()
                .Where(static target => target.IsEffectivelyVisible && target.Tag is FocusAnchor)];
            int current = Array.IndexOf(targets, focused);
            int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? targets.Length - 1 :
                Math.Clamp(current + (e.Key == Key.Down ? 1 : -1), 0, Math.Max(0, targets.Length - 1));
            if (targets.Length > 0) { _ = targets[next].Focus(NavigationMethod.Directional); e.Handled = true; }
        }
    }

    private static EntryKey RowKey(OutputLayoutPanelRow row)
    {
        return new(OutputLayoutRowIdentity.For(row.Members[0]), OutputLayoutRowIdentity.For(row.Members[^1]), row.Members.Count);
    }

    internal sealed record EntryKey(OutputLayoutRowIdentity First, OutputLayoutRowIdentity Last, int Count);
    internal sealed record FocusAnchor(EntryKey? Key, OutputLayoutGroupKind? Group, string Role);
    private sealed record Entry(EntryKey Key, OutputLayoutPanelRow Row, OutputLayoutDisclosure Disclosure,
        OutputLayoutDisclosure? Technical, Border Highlight);
}
