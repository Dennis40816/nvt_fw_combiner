using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>A proportional rail with a stable local strip and nearby terminal-slice information cards.</summary>
public sealed class MemoryCoverageBar : UserControl
{
    /// <summary>Original typed slices, independent of supporting information rows.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, IEnumerable?>(nameof(ItemsSource));
    /// <summary>Localized shell resources supplied by the owning presentation.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, object?>(nameof(Labels));
    /// <summary>Uses the existing Merge bar template.</summary>
    public static readonly StyledProperty<bool> IsPlainProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(IsPlain));
    /// <summary>Shows compact labels in the existing rail, used by explicit focus views.</summary>
    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(ShowLabels));
    /// <summary>Suppresses the informational-card reveal animation.</summary>
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(ReducedMotion));

    private readonly ItemsControl _main = new() { Height = 34, ClipToBounds = false };
    private readonly Border _track = new() { Height = 34, ClipToBounds = false };
    private readonly Popup _localPopup = new() { ShouldUseOverlayLayer = true, IsLightDismissEnabled = false };
    private readonly Popup _cardPopup = new() { ShouldUseOverlayLayer = true, IsLightDismissEnabled = false };
    private readonly Border _local = new() { Name = "MemoryLocalView" };
    private readonly Border _card = Surface("MemorySliceCard");
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly List<Control> _sliceTargets = [];
    private INotifyCollectionChanged? _collection;
    private MemoryCoverageBarItem? _activeGroup;
    private Control? _groupTarget;
    private Control? _cardTarget;
    private Window? _window;
    private ScrollViewer[] _scrollOwners = [];
    private bool _attached;
    private bool _restoringFocus;
    private bool _localAbove;
    private bool _keyboardFocus;
    private int _revealVersion;

    /// <summary>Constructs the shared rail using existing bar, color, card and interaction owners.</summary>
    public MemoryCoverageBar()
    {
        Height = 34;
        _track.Child = _main;
        Content = new Panel { Children = { _track, _localPopup, _cardPopup } };
        _ = _track.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcMemoryTrackBrush"));
        _ = _local.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
        _track.PointerMoved += (_, e) =>
        {
            if (_main.GetVisualDescendants().OfType<ProportionalStackPanel>().FirstOrDefault() is { } panel &&
                panel.Children.Any(child => child.DataContext is MemoryCoverageBarItem { IsPrimaryContent: false } &&
                    child.Bounds.Contains(e.GetPosition(panel)))) { CloseAll(); }
        };
        MemoryCoverageInteractionBehavior.SetIsEnabled(_card, true);
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            if (!IsInteracting(this) &&
                !IsInteracting(_localPopup.Child) && !IsInteracting(_cardPopup.Child)) { CloseAll(); }
        };
        PointerExited += (_, _) => _closeTimer.Start();
        LostFocus += (_, _) => _closeTimer.Start();
        TrackInputOrigin(this);
        _card.KeyDown += OnCardKeyDown;
    }

    /// <inheritdoc cref="ItemsSourceProperty" />
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <inheritdoc cref="LabelsProperty" />
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <inheritdoc cref="IsPlainProperty" />
    public bool IsPlain { get => GetValue(IsPlainProperty); set => SetValue(IsPlainProperty, value); }
    /// <inheritdoc cref="ShowLabelsProperty" />
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    /// <inheritdoc cref="ReducedMotionProperty" />
    public bool ReducedMotion { get => GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }
    private ShellTextResources Text => Labels as ShellTextResources ?? ShellTextResources.For(ShellLanguage.English);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.Deactivated += OnWindowDeactivated;
            _window.SizeChanged += OnWindowSizeChanged;
        }
        _scrollOwners = [.. this.GetVisualAncestors().OfType<ScrollViewer>()];
        foreach (ScrollViewer scroll in _scrollOwners) { scroll.ScrollChanged += OnScrollChanged; }
        Subscribe();
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _collection?.CollectionChanged -= OnCollectionChanged;
        _collection = null;
        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window.SizeChanged -= OnWindowSizeChanged;
        }
        _window = null;
        foreach (ScrollViewer scroll in _scrollOwners) { scroll.ScrollChanged -= OnScrollChanged; }
        _scrollOwners = [];
        CloseAll();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (!_attached) { return; }
        if (change.Property == ItemsSourceProperty || change.Property == LabelsProperty || change.Property == IsPlainProperty || change.Property == ShowLabelsProperty)
        {
            Subscribe();
            Rebuild();
        }
        else if (change.Property == BoundsProperty || change.Property == IsVisibleProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            CloseAll();
        }
        else if (change.Property == ReducedMotionProperty && ReducedMotion) { FinishReveal(); }
    }

    private static Border Surface(string name)
    {
        var border = new Border { Name = name, Classes = { "surface" }, Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        _ = border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
        _ = border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("NfcBorderMutedBrush"));
        return border;
    }

    private bool IsInteracting(Control? control)
    {
        return control is { IsPointerOver: true } || (_keyboardFocus && control is { IsKeyboardFocusWithin: true });
    }

    private void TrackInputOrigin(Control control)
    {
        control.GotFocus += (_, e) => _keyboardFocus = e is not FocusChangedEventArgs { NavigationMethod: NavigationMethod.Pointer };
        control.AddHandler(PointerPressedEvent, (_, _) => _keyboardFocus = false, RoutingStrategies.Tunnel);
        control.AddHandler(PointerMovedEvent, (_, _) => _keyboardFocus = false, RoutingStrategies.Tunnel);
        control.AddHandler(KeyDownEvent, (_, _) => _keyboardFocus = true, RoutingStrategies.Tunnel);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) { CloseAll(); }
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e) { CloseAll(); }
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (ReferenceEquals(sender, e.Source)) { CloseAll(); }
    }
    private void Subscribe()
    {
        _collection?.CollectionChanged -= OnCollectionChanged;
        _collection = ItemsSource as INotifyCollectionChanged;
        _collection?.CollectionChanged += OnCollectionChanged;
    }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { Rebuild(); }

    private void Rebuild()
    {
        CloseAll();
        _main.ItemContainerTheme = (ControlTheme)this.FindResource("ProportionalContentPresenterTheme")!;
        _main.ItemsPanel = new FuncTemplate<Panel?>(() => new ProportionalStackPanel());
        _main.ItemTemplate = new FuncDataTemplate<MemoryCoverageBarItem>((item, _) =>
        {
            if (item is null) { return null; }
            if (!item.IsPrimaryContent) { return new Border { Name = "MemoryTraceSpacer", Height = 34, IsHitTestVisible = false }; }
            if (!item.IsGroup)
            {
                Control slice = SegmentContent(item.Slices[0]);
                WireSlice(slice, item.Slices[0], local: false);
                return slice;
            }
            var target = new Border
            {
                DataContext = item,
                Focusable = true,
                FocusAdorner = null,
                Height = 34,
                Classes = { "memoryCoverageFill", "memoryCoverageLinkedRow", "memoryExplorerGroup" },
                Child = new TextBlock { Text = "⋮", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            AutomationProperties.SetName(target, GroupSummary(item));
            AutomationProperties.SetHelpText(target, Text.MemoryLocalViewHint);
            target.PointerEntered += (_, _) => OpenLocal(item, target);
            target.GotFocus += (_, _) => { if (!_restoringFocus) { OpenLocal(item, target); } };
            target.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up)
                {
                    OpenLocal(item, target);
                    _ = _sliceTargets.FirstOrDefault()?.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape) { CloseAll(); e.Handled = true; }
            };
            return target;
        });
        _main.ItemsSource = MemoryCoverageBarProjection.Create(ItemsSource?.Cast<MemoryCoverageSegmentViewModel>().ToArray() ?? []);
    }

    private Control SegmentContent(MemoryCoverageSegmentViewModel slice)
    {
        var template = (IDataTemplate)this.FindResource(IsPlain ? "MemoryCoveragePlainSegmentBarTemplate" : "MemoryCoverageSegmentBarTemplate")!;
        Control control = template.Build(slice) ?? throw new InvalidOperationException("The memory bar template must produce a control.");
        control.DataContext = slice;
        control.FocusAdorner = null;
        control.Classes.Add("memoryExplorerSlice");
        if (control is Border border)
        {
            Control? pattern = border.Child;
            border.Child = null;
            var overlay = new Panel();
            if (pattern is not null) { overlay.Children.Add(pattern); }
            if (ShowLabels)
            {
                control.Classes.Add("memoryFocusSlice");
                if (slice.DisplayParts.Count > 0)
                {
                    var parts = new ProportionalStackPanel { IsHitTestVisible = false };
                    foreach (MemoryCoverageSegmentViewModel part in slice.DisplayParts)
                    {
                        Control fill = template.Build(part)!;
                        fill.DataContext = part;
                        fill.IsHitTestVisible = false;
                        _ = fill.Classes.Remove("memoryCoverageBarSegment");
                        _ = fill.Classes.Remove("memoryCoverageLinkedRow");
                        MemoryCoverageInteractionBehavior.SetIsEnabled(fill, false);
                        ProportionalStackPanel.SetWeight(fill, part.BarWidth);
                        parts.Children.Add(fill);
                    }
                    overlay.Children.Add(parts);
                }
                var label = new TextBlock
                {
                    Text = slice.ContentRole == Application.MemoryLayout.MemoryContentRole.CtrlRam
                        ? ShellTextResources.GetMemoryFocusLabel(slice.CtrlRamRegionRole) : slice.DisplayTitle,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(2, 0),
                    IsHitTestVisible = false,
                };
                _ = label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(
                    slice.FillRole is MemoryCoverageFillRole.Neutral or MemoryCoverageFillRole.Kept
                        ? "NfcTextStrongBrush" : "NfcSurfaceBrush"));
                // Context stays neutral and unlabeled on the rail; its exact range remains in the legend/card.
                label.IsVisible = slice.FillRole != MemoryCoverageFillRole.Neutral;
                overlay.Children.Add(label);
            }
            var outline = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), IsHitTestVisible = false };
            _ = outline.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
            _ = outline.Bind(IsVisibleProperty, new Binding("Interaction.IsActive"));
            overlay.Children.Add(outline);
            border.Child = overlay;
        }
        ToolTip.SetTip(control, null);
        return control;
    }

    private void WireSlice(Control target, MemoryCoverageSegmentViewModel slice, bool local)
    {
        target.Focusable = true;
        target.PointerEntered += (_, _) => OpenSlice();
        target.GotFocus += (_, _) => { if (!_restoringFocus) { OpenSlice(); } };
        target.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (_cardPopup.IsOpen) { CloseCard(); RestoreFocus(target); }
                else { Control? parent = _groupTarget; CloseAll(); RestoreFocus(parent); }
                e.Handled = true;
            }
            else if (e.Key is Key.Enter or Key.Space or Key.Up or Key.Down)
            {
                OpenSlice();
                _ = _card.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault()?.Focus(NavigationMethod.Tab);
                e.Handled = true;
            }
            else if (local && e.Key is Key.Left or Key.Right or Key.Home or Key.End)
            {
                int index = _sliceTargets.IndexOf(target);
                int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? _sliceTargets.Count - 1 : Math.Clamp(index + (e.Key == Key.Left ? -1 : 1), 0, _sliceTargets.Count - 1);
                _ = _sliceTargets[next].Focus(NavigationMethod.Directional);
                e.Handled = true;
            }
        };
        void OpenSlice()
        {
            if (!local && _localPopup.IsOpen) { CloseAll(); }
            OpenCard(target, slice, local ? _localAbove : null);
        }
    }

    private string GroupSummary(MemoryCoverageBarItem item)
    {
        return $"{item.AddressSpace} · {item.StartLabel}–{item.EndLabel} · {Text.FormatMemorySliceCount(item.Slices.Count)} · {item.SizeLabel}";
    }

    private void OpenLocal(MemoryCoverageBarItem item, Control target)
    {
        _closeTimer.Stop();
        if (_localPopup.IsOpen && ReferenceEquals(item, _activeGroup)) { return; }
        CloseAll();
        _activeGroup = item;
        _groupTarget = target;
        target.Classes.Add("active");
        _local.Width = Bounds.Width;
        var strip = new ProportionalStackPanel { Name = "MemoryLocalStrip", Height = 34, ClipToBounds = false };
        foreach (MemoryCoverageSegmentViewModel slice in item.Slices)
        {
            Control cell = SegmentContent(slice);
            cell.Classes.Add("memoryLocalSlice");
            WireSlice(cell, slice, local: true);
            ProportionalStackPanel.SetWeight(cell, slice.BarWidth);
            _sliceTargets.Add(cell);
            strip.Children.Add(cell);
        }
        var endpoints = new Grid { Name = "MemoryLocalEndpoints", ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        endpoints.Children.Add(new TextBlock { Text = item.StartLabel, Classes = { "technicalValue" }, HorizontalAlignment = HorizontalAlignment.Left });
        var end = new TextBlock { Text = item.EndLabel, Classes = { "technicalValue" } };
        Grid.SetColumn(end, 1);
        endpoints.Children.Add(end);
        var content = new StackPanel { Spacing = 6 };
        TopLevel top = TopLevel.GetTopLevel(this)!;
        double above = _track.TranslatePoint(default, top)?.Y ?? 0;
        _localAbove = above > top.Bounds.Height - above - _track.Bounds.Height;
        var header = new StackPanel { Name = "MemoryLocalHeader", Spacing = 3 };
        header.Children.Add(new TextBlock { Text = Text.MemoryLocalViewLabel, Classes = { "bodyEmphasisText" }, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left });
        header.Children.Add(new TextBlock { Text = $"{Text.FormatMemorySliceCount(item.Slices.Count)} · {item.SizeLabel} · {item.AddressSpace}", Classes = { "captionText" }, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(header);
        var rail = new Border { Name = "MemoryLocalRail", Child = strip, BorderThickness = new Thickness(1), Padding = new Thickness(2), CornerRadius = new CornerRadius(5) };
        _ = rail.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("NfcBorderMutedBrush"));
        content.Children.Add(rail);
        content.Children.Add(endpoints);
        _local.Child = content;
        var connector = new Canvas { Height = 32 };
        double left = target.TranslatePoint(default, _track)?.X ?? 0;
        foreach ((double localX, double mainX) in new[] { (3d, left), (Bounds.Width - 3, left + target.Bounds.Width) })
        {
            connector.Children.Add(Connector(new Point(localX, _localAbove ? 0 : 32), new Point(mainX, _localAbove ? 32 : 0)));
        }
        StackPanel frame = PopupFrame(_local, connector, _localAbove);
        _ = frame.Bind(Panel.BackgroundProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
        _localPopup.Child = frame;
        _localPopup.Width = Bounds.Width;
        _localPopup.PlacementTarget = _track;
        _localPopup.Placement = _localAbove ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        _localPopup.IsOpen = true;
    }

    private void OpenCard(Control target, MemoryCoverageSegmentViewModel slice, bool? preferredAbove)
    {
        _closeTimer.Stop();
        if (_cardPopup.IsOpen && ReferenceEquals(target, _cardTarget)) { return; }
        CloseCard();
        _cardTarget = target;
        _card.DataContext = slice;
        var details = new ContentControl { Content = slice, HorizontalContentAlignment = HorizontalAlignment.Stretch, ContentTemplate = (IDataTemplate)this.FindResource("MemoryCoverageRegionCardTemplate")! };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(details);
        _card.Child = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        TopLevel top = TopLevel.GetTopLevel(this)!;
        Point origin = target.TranslatePoint(default, top) ?? default;
        double below = top.Bounds.Height - origin.Y - target.Bounds.Height;
        bool above = preferredAbove ?? (origin.Y >= 250 || origin.Y > below);
        // Keep the approved header above the strip, with the card above that header.
        Point localOrigin = _local.TranslatePoint(default, top) ?? default;
        double connectorHeight = preferredAbove.HasValue
            ? Math.Max(20, (above ? origin.Y - localOrigin.Y : localOrigin.Y + _local.Bounds.Height - origin.Y - target.Bounds.Height) + 12)
            : ShowLabels ? 8 : 20;
        double available = (above ? origin.Y : below) - connectorHeight - 8;
        _card.MaxHeight = Math.Max(64, available);
        double trackLeft = _track.TranslatePoint(default, top)?.X ?? 0;
        double columnLeft = Math.Max(8, trackLeft);
        double columnRight = Math.Min(top.Bounds.Width - 8, trackLeft + Bounds.Width);
        double width = Math.Min(Math.Clamp(Bounds.Width * 0.68, 240, 280), Math.Max(1, columnRight - columnLeft));
        _card.Width = width;
        double center = origin.X + (target.Bounds.Width / 2);
        double left = Math.Clamp(center - (width / 2), columnLeft, Math.Max(columnLeft, columnRight - width));
        double anchor = center - left;
        double connectorTop = above ? origin.Y - connectorHeight : origin.Y + target.Bounds.Height;
        Rect[] labels = preferredAbove.HasValue
            ? [.. _local.GetVisualDescendants().OfType<TextBlock>().Where(static block => block.IsEffectivelyVisible)
                .Select(block =>
                {
                    Point point = block.TranslatePoint(default, top) ?? default;
                    return new Rect(point.X - left, point.Y - connectorTop, block.Bounds.Width, block.Bounds.Height);
                })]
            : [];
        Canvas connector = CardConnector(anchor, connectorHeight, above, labels);
        _cardPopup.Child = PopupFrame(_card, connector, above);
        _cardPopup.Width = width;
        _cardPopup.PlacementTarget = target;
        _cardPopup.HorizontalOffset = left - origin.X;
        _cardPopup.Placement = above ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        _cardPopup.IsOpen = true;
        RevealCard(above);
    }

    private static Line Connector(Point start, Point end)
    {
        var line = new Line { StartPoint = start, EndPoint = end, StrokeThickness = 1, IsHitTestVisible = false };
        _ = line.Bind(Shape.StrokeProperty, new DynamicResourceExtension("NfcBorderMutedBrush"));
        return line;
    }

    private static Canvas CardConnector(double anchor, double height, bool above, IReadOnlyList<Rect> labels)
    {
        var canvas = new Canvas { Height = height, ClipToBounds = false };
        double edge = above ? -1 : height + 1;
        double tip = above ? 6 : height - 6;
        double terminal = above ? height : 0;
        var points = new List<Point> { new(anchor - 6, edge), new(anchor, tip), new(anchor + 6, edge) };
        var fill = new Polygon { Name = "MemoryCardNotch", Points = points, IsHitTestVisible = false };
        _ = fill.Bind(Shape.FillProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
        canvas.Children.Add(fill);
        var outline = new Polyline { Points = points, StrokeThickness = 1, IsHitTestVisible = false };
        _ = outline.Bind(Shape.StrokeProperty, new DynamicResourceExtension("NfcBorderMutedBrush"));
        canvas.Children.Add(outline);
        // A left-edge slice may align with local text. Interrupt only the decorative
        // stem behind those glyph bounds; its endpoint still identifies the exact slice.
        double cursor = Math.Min(tip, terminal);
        double limit = Math.Max(tip, terminal);
        foreach (Rect label in labels.Where(rect => anchor >= rect.Left - 2 && anchor <= rect.Right + 2).OrderBy(static rect => rect.Top))
        {
            double start = Math.Clamp(label.Top - 2, cursor, limit);
            if (start > cursor) { canvas.Children.Add(Connector(new Point(anchor, cursor), new Point(anchor, start))); }
            cursor = Math.Clamp(label.Bottom + 2, start, limit);
        }
        if (cursor < limit) { canvas.Children.Add(Connector(new Point(anchor, cursor), new Point(anchor, limit))); }
        var dot = new Ellipse { Name = "MemoryCardAnchor", Width = 4, Height = 4, StrokeThickness = 1, IsHitTestVisible = false };
        _ = dot.Bind(Shape.StrokeProperty, new DynamicResourceExtension("NfcAccentStrongBrush"));
        _ = dot.Bind(Shape.FillProperty, new DynamicResourceExtension("NfcSurfaceBrush"));
        Canvas.SetLeft(dot, anchor - 2);
        Canvas.SetTop(dot, terminal - 2);
        canvas.Children.Add(dot);
        return canvas;
    }

    private StackPanel PopupFrame(Control body, Control connector, bool above)
    {
        var frame = new StackPanel { Background = Brushes.Transparent };
        TrackInputOrigin(frame);
        frame.Children.Add(above ? body : connector);
        frame.Children.Add(above ? connector : body);
        frame.PointerEntered += (_, _) => _closeTimer.Stop();
        frame.PointerExited += (_, _) => _closeTimer.Start();
        frame.LostFocus += (_, _) => _closeTimer.Start();
        return frame;
    }

    private void RevealCard(bool above)
    {
        FinishReveal();
        if (ReducedMotion) { return; }
        int version = _revealVersion;
        _card.Opacity = 0;
        _card.RenderTransform = TransformOperations.Parse(above ? "translateY(8px)" : "translateY(-8px)");
        _card.Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseOut() },
        ];
        Dispatcher.UIThread.Post(() =>
        {
            if (version == _revealVersion && _cardPopup.IsOpen)
            {
                _card.Opacity = 1;
                _card.RenderTransform = TransformOperations.Identity;
            }
        }, DispatcherPriority.Render);
    }

    private void FinishReveal()
    {
        _revealVersion++;
        _card.Transitions = null;
        _card.Opacity = 1;
        _card.RenderTransform = TransformOperations.Identity;
    }

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) { return; }
        Control? target = _cardTarget;
        CloseCard();
        RestoreFocus(target);
        e.Handled = true;
    }

    private void RestoreFocus(Control? target)
    {
        _restoringFocus = true;
        _ = target?.Focus(NavigationMethod.Tab);
        _restoringFocus = false;
    }

    private void CloseCard()
    {
        FinishReveal();
        _cardPopup.IsOpen = false;
        if (_cardPopup.Child is Panel frame) { frame.Children.Clear(); }
        _cardPopup.Child = null;
        _card.Child = null;
        _card.DataContext = null;
        _cardTarget = null;
    }

    private void CloseAll()
    {
        _closeTimer.Stop();
        _keyboardFocus = false;
        CloseCard();
        _localPopup.IsOpen = false;
        if (_localPopup.Child is Panel frame) { frame.Children.Clear(); }
        _localPopup.Child = null;
        _local.Child = null;
        _sliceTargets.Clear();
        _activeGroup = null;
        _ = _groupTarget?.Classes.Remove("active");
        _groupTarget = null;
    }
}
