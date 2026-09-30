using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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
public sealed partial class MemoryCoverageBar : UserControl
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
    /// <summary>Shows compact legend rows below the rail instead of persistent detail cards.</summary>
    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(ShowLegend));
    /// <summary>Optional exact positions whose contiguous lanes open through the shared local-view overlay.</summary>
    public static readonly StyledProperty<IEnumerable?> FocusPositionsProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, IEnumerable?>(nameof(FocusPositions));
    /// <summary>Suppresses the informational-card reveal animation.</summary>
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(ReducedMotion));

    private readonly ItemsControl _main = new() { Name = "MemoryMainRail", Height = 34, ClipToBounds = false };
    private readonly Border _track = new() { Height = 34, ClipToBounds = false };
    private readonly ItemsControl _positions = new() { Height = 22, Margin = new Thickness(0, 6, 0, 0), ClipToBounds = false };
    private readonly Popup _localPopup = new() { ShouldUseOverlayLayer = true, IsLightDismissEnabled = false };
    private readonly Popup _cardPopup = new() { ShouldUseOverlayLayer = true, IsLightDismissEnabled = false };
    private readonly Border _local = Surface("MemoryLocalView");
    private readonly Border _card = Surface("MemorySliceCard");
    private readonly Func<Action, TimeSpan, IDisposable> _scheduleClose;
    private IDisposable? _pendingClose;
    private long _closeGeneration;
    private readonly List<Control> _sliceTargets = [];
    private IReadOnlyList<MemoryCoverageSegmentViewModel> _displaySegments = [];
    private INotifyCollectionChanged? _collection;
    private INotifyCollectionChanged? _positionCollection;
    private MemoryCoverageBarItem? _activeGroup;
    private Control? _groupTarget;
    private Control? _cardTarget;
    private Window? _window;
    private ScrollViewer[] _scrollOwners = [];
    private bool _attached;
    private bool _restoringFocus;
    private bool _localAbove;
    private bool _keyboardFocus;
    private bool _closingOverlays;
    private bool _closeAllPending;
    private Control? _passiveReopenTarget;
    private Point? _lastPointerPosition;

    /// <summary>Constructs the shared rail using existing bar, color, card and interaction owners.</summary>
    public MemoryCoverageBar()
        : this(static (callback, delay) => DispatcherTimer.RunOnce(callback, delay, DispatcherPriority.Background))
    {
    }

    // The scheduler defers callbacks onto the owning UI thread; it never invokes them inline.
    // Disposing a scheduled callback cancels it; generation also rejects already-queued stale work.
    internal MemoryCoverageBar(Func<Action, TimeSpan, IDisposable> scheduleClose)
    {
        ArgumentNullException.ThrowIfNull(scheduleClose);
        _scheduleClose = scheduleClose;
        Height = 34;
        ClipToBounds = false;
        _track.Child = _main;
        InitializeOverview();
        _footer.Child = new StackPanel { Children = { _markers, _positions, _legend } };
        Content = new Panel { Children = { new StackPanel { Children = { _header, _addresses, _track, _footer } }, _localPopup, _cardPopup } };
        _ = _track.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcMemoryTrackBrush"));
        _track.PointerMoved += (_, e) =>
        {
            if (_main.GetVisualDescendants().OfType<ProportionalStackPanel>().FirstOrDefault() is { } panel &&
                panel.Children.Any(child => child.DataContext is MemoryCoverageBarItem { IsPrimaryContent: false } &&
                    child.Bounds.Contains(e.GetPosition(panel)))) { CloseAll(); }
        };
        // The card belongs to its slice: while the pointer or focus is inside it, the slice's legend row stays lit.
        _ = _card.SetValue(MemoryCoverageInteractionBehavior.IsRailProperty, true);
        MemoryCoverageInteractionBehavior.SetIsEnabled(_card, true);
        _ = _local.SetValue(MemoryCoverageInteractionBehavior.IsRailProperty, true);
        MemoryCoverageInteractionBehavior.SetIsEnabled(_local, true);
        PointerExited += (_, _) => StartCloseTimer();
        LostFocus += (_, _) => StartCloseTimer();
        _card.KeyDown += OnCardKeyDown;
        // Inner scrolling gets first refusal; unconsumed wheel input must not move
        // the page behind an overlay (including cards whose content already fits).
        _card.PointerWheelChanged += (_, e) => e.Handled = true;
    }

    /// <inheritdoc cref="ItemsSourceProperty" />
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <inheritdoc cref="LabelsProperty" />
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <inheritdoc cref="IsPlainProperty" />
    public bool IsPlain { get => GetValue(IsPlainProperty); set => SetValue(IsPlainProperty, value); }
    /// <inheritdoc cref="ShowLabelsProperty" />
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    /// <inheritdoc cref="ShowLegendProperty" />
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    /// <inheritdoc cref="FocusPositionsProperty" />
    public IEnumerable? FocusPositions { get => GetValue(FocusPositionsProperty); set => SetValue(FocusPositionsProperty, value); }
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
        _positionCollection?.CollectionChanged -= OnCollectionChanged;
        _positionCollection = null;
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
        if (change.Property == ItemsSourceProperty || change.Property == LabelsProperty || change.Property == IsPlainProperty || change.Property == ShowLabelsProperty || change.Property == ShowLegendProperty || change.Property == FocusPositionsProperty || change.Property == HeadingProperty || change.Property == CapacityLabelProperty || change.Property == StartAddressProperty || change.Property == EndAddressProperty)
        {
            Subscribe();
            Rebuild();
        }
        else if (change.Property == BoundsProperty) { ClosePassively(); }
        else if (change.Property == IsVisibleProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            CloseAll();
        }
        else if (change.Property == ReducedMotionProperty)
        {
            if (ReducedMotion) { FinishReveal(_card); FinishReveal(_local); }
            foreach (Control target in this.GetVisualDescendants().Concat(_local.GetVisualDescendants()).OfType<Control>().Where(control =>
                control.Classes.Contains("memoryExplorerSlice") || control.Classes.Contains("memoryExplorerGroup")))
            {
                // Disable transitions before the class changes RenderTransform; otherwise
                // the style update can start a final animated return to rest.
                if (ReducedMotion) { target.Transitions = null; }
                target.Classes.Set("reducedMotion", ReducedMotion);
                if (!ReducedMotion) { target.ClearValue(TransitionsProperty); }
            }
        }
    }

    private static Border Surface(string name)
    {
        var border = new Border { Name = name, Classes = { "surface" }, Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        _ = border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcMemoryInteractionSurfaceBrush"));
        _ = border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("NfcAccentBorderBrush"));
        _ = border.Bind(Border.BoxShadowProperty, new DynamicResourceExtension("NfcMemoryRowHoverShadow"));
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
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e) { ClosePassively(); }
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (ReferenceEquals(sender, e.Source) && (e.OffsetDelta != default || e.ViewportDelta != default)) { ClosePassively(); }
    }

    private void ClosePassively()
    {
        Control? target = _groupTarget ?? _cardTarget ?? _passiveReopenTarget;
        CloseAll();
        if (target is { IsPointerOver: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } &&
            ReferenceEquals(TopLevel.GetTopLevel(target), _window)) { _passiveReopenTarget = target; }
    }
    private void Subscribe()
    {
        _collection?.CollectionChanged -= OnCollectionChanged;
        _collection = ItemsSource as INotifyCollectionChanged;
        _collection?.CollectionChanged += OnCollectionChanged;
        _positionCollection?.CollectionChanged -= OnCollectionChanged;
        _positionCollection = FocusPositions as INotifyCollectionChanged;
        _positionCollection?.CollectionChanged += OnCollectionChanged;
    }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { Rebuild(); }

    private void Rebuild()
    {
        CloseAll();
        _displaySegments = MemoryCoverageBarProjection.CoalesceContent(ItemsSource?.Cast<MemoryCoverageSegmentViewModel>().ToArray() ?? [], Text);
        _markerWidth = double.NaN;
        InvalidateMeasure();
        BuildPositions();
        BuildLegend();
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
            KeepLabelUnscaled(target, (TextBlock)target.Child);
            target.Classes.Set("reducedMotion", ReducedMotion);
            WireGroup(target, () => OpenLocal(item, target));
            AutomationProperties.SetName(target, GroupSummary(item));
            AutomationProperties.SetHelpText(target, Text.MemoryLocalViewHint);
            return target;
        });
        _main.ItemsSource = MemoryCoverageBarProjection.Create(_displaySegments);
    }

    private void BuildPositions()
    {
        MemoryFocusPositionViewModel[] positions = FocusPositions?.Cast<MemoryFocusPositionViewModel>().ToArray() ?? [];
        _positions.IsVisible = positions.Length > 0;
        UpdateRailHeight();
        double total = positions.Sum(static position => position.BarWidth);
        _positions.ItemContainerTheme = (ControlTheme)this.FindResource("ProportionalContentPresenterTheme")!;
        _positions.ItemsPanel = new FuncTemplate<Panel?>(() => new ProportionalStackPanel());
        _positions.ItemTemplate = new FuncDataTemplate<MemoryFocusPositionViewModel>((position, scope) =>
        {
            if (position?.Lane is not { } lane) { return new Border { IsHitTestVisible = false }; }
            var marker = new Border { Height = 2, CornerRadius = new CornerRadius(1), Classes = { "memoryPositionUnderline" } };
            _ = marker.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcAccentBrush"));
            var label = new TextBlock
            {
                Text = position.Label,
                Classes = { "memoryPositionLabel" },
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            double startFraction = positions.TakeWhile(candidate => !ReferenceEquals(candidate, position))
                .Sum(static candidate => candidate.BarWidth) / total;
            var target = new Border
            {
                Name = "MemoryFocusPosition",
                DataContext = position,
                Focusable = true,
                FocusAdorner = null,
                RenderTransformOrigin = RelativePoint.Center,
                Classes = { "memoryExplorerGroup", "memoryFocusPosition" },
                Child = new MemoryEndpointPanel(this, startFraction, position.BarWidth / total, marker, label),
            };
            target.Classes.Set("reducedMotion", ReducedMotion);
            var item = new MemoryCoverageBarItem(lane.Ranges);
            _ = target.SetValue(MemoryCoverageInteractionBehavior.RailContextProperty, ContainingStates(lane));
            WireGroup(target, () => OpenLocal(item, target, lane));
            AutomationProperties.SetName(target, $"{lane.Title} · {lane.RangeLabel}");
            AutomationProperties.SetHelpText(target, Text.MemoryLocalViewHint);
            return target;
        });
        _positions.ItemsSource = positions;
    }

    private Control SegmentContent(MemoryCoverageSegmentViewModel slice)
    {
        var template = (IDataTemplate)this.FindResource(IsPlain ? "MemoryCoveragePlainSegmentBarTemplate" : "MemoryCoverageSegmentBarTemplate")!;
        Control control = template.Build(slice) ?? throw new InvalidOperationException("The memory bar template must produce a control.");
        control.DataContext = slice;
        control.FocusAdorner = null;
        control.Classes.Add("memoryExplorerSlice");
        control.Classes.Set("reducedMotion", ReducedMotion);
        if (control is Border border)
        {
            Control? pattern = border.Child;
            border.Child = null;
            var overlay = new Panel();
            if (pattern is not null) { overlay.Children.Add(pattern); }
            if (slice.DisplayParts.Any(part => part.FillRole != slice.FillRole || part.UsesKeptPattern != slice.UsesKeptPattern))
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
            if (ShowLabels)
            {
                control.Classes.Add("memoryFocusSlice");
                var label = new TextBlock
                {
                    Text = slice.ContentRole == Application.MemoryLayout.MemoryContentRole.CtrlRam &&
                        slice.DisplayParts.All(part => part.CtrlRamRegionRole == slice.CtrlRamRegionRole)
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
                KeepLabelUnscaled(control, label);
                overlay.Children.Add(label);
            }
            border.Child = overlay;
        }
        ToolTip.SetTip(control, null);
        return control;
    }

    private static void KeepLabelUnscaled(Control target, TextBlock label)
    {
        var compensation = new ScaleTransform();
        label.RenderTransformOrigin = RelativePoint.Center;
        label.RenderTransform = compensation;
        UpdateScale();
        target.PropertyChanged += (_, change) =>
        {
            if (change.Property == RenderTransformProperty) { UpdateScale(); }
        };
        void UpdateScale()
        {
            // Follow the same animated transform, including Reduced Motion and return to rest.
            compensation.ScaleY = 1 / (target.RenderTransform?.Value.M22 ?? 1);
        }
    }

    private void WireSlice(Control target, MemoryCoverageSegmentViewModel slice, bool local)
    {
        target.Focusable = true;
        WirePointerTarget(target, OpenSlice);
        target.GotFocus += (_, e) => { if (CanExploreFromFocus(e)) { OpenSlice(); } };
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

    private void WirePointerTarget(Control target, Action open)
    {
        _ = target.SetValue(MemoryCoverageInteractionBehavior.IsRailProperty, true);
        MemoryCoverageInteractionBehavior.SetIsEnabled(target, true);
        TrackInputOrigin(target);
        target.PointerEntered += (_, e) =>
        {
            _passiveReopenTarget = null;
            _lastPointerPosition = e.GetPosition(_window);
            open();
        };
        target.PointerMoved += (_, e) =>
        {
            Point position = e.GetPosition(_window);
            bool moved = _lastPointerPosition is { } previous && previous != position;
            _lastPointerPosition = position;
            if (moved && ReferenceEquals(_passiveReopenTarget, target) && target.IsPointerOver)
            {
                _passiveReopenTarget = null;
                open();
            }
        };
        target.PointerExited += (_, _) => StartCloseTimer();
        target.LostFocus += (_, _) => StartCloseTimer();
    }

    private bool CanExploreFromFocus(RoutedEventArgs e)
    {
        return !_restoringFocus && e is not FocusChangedEventArgs { NavigationMethod: NavigationMethod.Pointer };
    }

    private void StartCloseTimer()
    {
        if (_pendingClose is not null) { return; }
        long generation = _closeGeneration;
        _pendingClose = _scheduleClose(() =>
        {
            if (generation != _closeGeneration) { return; }
            StopCloseTimer();
            if (!IsInteracting(_groupTarget ?? _cardTarget) &&
                !IsInteracting(_localPopup.Child) && !IsInteracting(_cardPopup.Child)) { CloseAll(); }
        }, TimeSpan.FromMilliseconds(320));
    }

    private void StopCloseTimer()
    {
        IDisposable? pending = _pendingClose;
        _pendingClose = null;
        _closeGeneration++;
        pending?.Dispose();
    }

    private string GroupSummary(MemoryCoverageBarItem item)
    {
        return $"{item.AddressSpace} · {item.StartLabel}–{item.EndLabel} · {Text.FormatMemorySliceCount(item.Slices.Count)} · {item.SizeLabel}";
    }

    private void OpenLocal(MemoryCoverageBarItem item, Control target, MemoryFocusLaneViewModel? lane = null, bool collisionList = false)
    {
        if (_closingOverlays || !_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible) { return; }
        StopCloseTimer();
        if (_localPopup.IsOpen && ReferenceEquals(item, _activeGroup)) { return; }
        CloseAll();
        if (!_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible) { return; }
        _activeGroup = item;
        _groupTarget = target;
        target.Classes.Add("active");
        _local.Width = Bounds.Width;
        Panel strip = collisionList
            ? new StackPanel { Name = "MemoryCollisionList", Spacing = 4 }
            : new ProportionalStackPanel { Name = "MemoryLocalStrip", Height = 34, ClipToBounds = false };
        foreach (MemoryCoverageSegmentViewModel slice in item.Slices)
        {
            Control cell = collisionList ? CollisionEntry(slice) : SegmentContent(slice);
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
        var content = new StackPanel { Spacing = 6, Name = lane is null ? null : "CtrlRamFocusLane", DataContext = lane };
        _ = _local.SetValue(MemoryCoverageInteractionBehavior.RailContextProperty, lane is null ? [] : ContainingStates(lane));
        TopLevel top = TopLevel.GetTopLevel(this)!;
        Control anchor = collisionList ? _markers : lane is null ? _track : _positions;
        // The stem names the true range on the flash bar, so it starts at the track and crosses
        // any rail row between the track and the view; only the view itself keeps its place.
        // A CtrlRAM lane is the exception: its stem leaves from its own label row.
        Control origin = lane is null ? _track : _positions;
        double trackTop = _track.TranslatePoint(default, top)?.Y ?? 0;
        double originTop = origin.TranslatePoint(default, top)?.Y ?? 0;
        double originBottom = originTop + origin.Bounds.Height;
        double belowEdge = ShowLegend
            ? (_footer.TranslatePoint(default, top)?.Y ?? 0) + _footer.Bounds.Height
            : (anchor.TranslatePoint(default, top)?.Y ?? 0) + anchor.Bounds.Height;
        _localAbove = trackTop > top.Bounds.Height - belowEdge;
        var header = new StackPanel { Name = "MemoryLocalHeader", Spacing = 3 };
        header.Children.Add(new TextBlock { Text = collisionList ? Text.FormatMemorySliceCount(item.Slices.Count) : lane?.Title ?? Text.MemoryLocalViewLabel, Classes = { "bodyEmphasisText" }, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left });
        header.Children.Add(new TextBlock { Text = collisionList ? Text.MemoryLocalViewHint : $"{(lane is null ? Text.FormatMemorySliceCount(item.Slices.Count) : Text.MemoryCtrlRamDetailLabel)} · {item.SizeLabel} · {item.AddressSpace}", Classes = { "captionText" }, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(header);
        var rail = new Border { Name = "MemoryLocalRail", Child = strip };
        content.Children.Add(rail);
        if (!collisionList) { content.Children.Add(endpoints); }
        // Leave the other half for the terminal card; dense lists scroll instead
        // of forcing the card to slide back over selectable entries.
        _local.MaxHeight = collisionList ? Math.Max(64, ((_localAbove ? trackTop : top.Bounds.Height - belowEdge) / 2) - 18) : double.PositiveInfinity;
        if (collisionList)
        {
            var scroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, IsScrollChainingEnabled = false };
            scroll.ScrollChanged += (_, e) => { if (!_keyboardFocus && ReferenceEquals(e.Source, scroll) && e.OffsetDelta != default) { CloseCard(); } };
            _local.Child = scroll;
        }
        else { _local.Child = content; }
        double stemX = lane is null ? RangeCenterX(target, _track) : LabelCenterX(target);
        double connectorHeight = 10 + (_localAbove ? originTop - trackTop : Math.Max(0, belowEdge - originBottom));
        // Across the rail rows and the passive legend the stem is decorative: it stays out of
        // input and is interrupted behind text so every label remains readable.
        Control connector;
        if (connectorHeight > 10)
        {
            double stemTop = _localAbove ? originTop - connectorHeight : originBottom;
            Rect[] labels = ObstacleRects(OverviewObstacles(), _track, 0, trackTop - stemTop);
            Canvas stem = MemoryCoverageConnectorVisuals.CardConnector(stemX, connectorHeight, _localAbove, labels, "MemoryLocal");
            stem.Name = "MemoryLocalConnector";
            connector = stem;
        }
        else { connector = MemoryCoverageConnectorVisuals.LocalConnector(stemX, 10); }
        StackPanel frame = PopupFrame(_local, connector, _localAbove);
        _localPopup.Child = frame;
        _localPopup.Width = Bounds.Width;
        _localPopup.PlacementTarget = origin;
        _localPopup.Placement = _localAbove ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        _localPopup.IsOpen = true;
        Reveal(_local, _localPopup, _localAbove);
    }

    private void OpenCard(Control target, MemoryCoverageSegmentViewModel slice, bool? preferredAbove)
    {
        if (_closingOverlays || !_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible) { return; }
        StopCloseTimer();
        if (_cardPopup.IsOpen && ReferenceEquals(target, _cardTarget)) { return; }
        CloseCard();
        if (!_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible) { return; }
        _cardTarget = target;
        _card.DataContext = slice;
        var details = new ContentControl { Content = slice, HorizontalContentAlignment = HorizontalAlignment.Stretch, ContentTemplate = (IDataTemplate)this.FindResource("MemoryCoverageRegionCardTemplate")! };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(details);
        _card.Child = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, IsScrollChainingEnabled = false };
        TopLevel top = TopLevel.GetTopLevel(this)!;
        // A tiny marker is only a 24 px handle: its stem starts at the slice's true range on the flash bar.
        bool fromMarker = target.Classes.Contains("memoryTinyMarker");
        Control source = fromMarker ? _track : target;
        Point origin = source.TranslatePoint(default, top) ?? default;
        double below = top.Bounds.Height - origin.Y - source.Bounds.Height;
        // Prefer the clear upper side when it can hold an expanded summary card.
        // Otherwise place direct cards below the endpoint row, not over the rail.
        bool above = preferredAbove ?? ((ShowLegend && origin.Y >= 300) || origin.Y > below);
        bool viaOverview = preferredAbove is null && (ShowLegend || fromMarker);
        Control placementTarget = viaOverview ? above ? this : _footer : target;
        Point placementOrigin = placementTarget.TranslatePoint(default, top) ?? default;
        below = top.Bounds.Height - placementOrigin.Y - placementTarget.Bounds.Height;
        // Keep the approved header above the strip, with the card above that header.
        Point localOrigin = _local.TranslatePoint(default, top) ?? default;
        double connectorHeight = preferredAbove.HasValue
            ? Math.Max(20, (above ? origin.Y - localOrigin.Y : localOrigin.Y + _local.Bounds.Height - origin.Y - target.Bounds.Height) + 4)
            : ShowLabels ? 8 : 20;
        // A direct card opened from the main rail reaches across the overview text so the
        // stem still names the exact slice. The card itself keeps its place: the placement
        // target moves to the slice and the stem absorbs the distance.
        bool crossesOverview = viaOverview;
        if (crossesOverview)
        {
            connectorHeight += above ? origin.Y - placementOrigin.Y :
                placementOrigin.Y + placementTarget.Bounds.Height - origin.Y - source.Bounds.Height;
            placementTarget = source;
            placementOrigin = origin;
            below = top.Bounds.Height - origin.Y - source.Bounds.Height;
        }
        double available = (above ? placementOrigin.Y : below) - connectorHeight - 8;
        _card.MaxHeight = Math.Max(64, available);
        double trackLeft = _track.TranslatePoint(default, top)?.X ?? 0;
        double columnLeft = Math.Max(8, trackLeft);
        double columnRight = Math.Min(top.Bounds.Width - 8, trackLeft + Bounds.Width);
        double width = Math.Min(Math.Clamp(Bounds.Width * 0.9, 240, 380), Math.Max(1, columnRight - columnLeft));
        _card.Width = width;
        double center = fromMarker ? trackLeft + RangeCenterX(target, _track) : origin.X + (target.Bounds.Width / 2);
        double left = Math.Clamp(center - (width / 2), columnLeft, Math.Max(columnLeft, columnRight - width));
        double anchor = center - left;
        double connectorTop = above ? origin.Y - connectorHeight : origin.Y + source.Bounds.Height;
        Rect[] labels = preferredAbove.HasValue
            ? ObstacleRects(_local.GetVisualDescendants().OfType<TextBlock>().Where(static block => block.IsEffectivelyVisible), top, -left, -connectorTop)
            : crossesOverview ? ObstacleRects(OverviewObstacles(), top, -left, -connectorTop) : [];
        Control connector = target.Classes.Contains("memoryCollisionEntry")
            ? new Border { Height = connectorHeight, IsHitTestVisible = false }
            : MemoryCoverageConnectorVisuals.CardConnector(anchor, connectorHeight, above, labels);
        _cardPopup.Child = PopupFrame(_card, connector, above);
        _cardPopup.Width = width;
        _cardPopup.PlacementTarget = placementTarget;
        _cardPopup.HorizontalOffset = left - placementOrigin.X;
        _cardPopup.Placement = above ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        _cardPopup.IsOpen = true;
        Reveal(_card, _cardPopup, above);
    }


    // A CtrlRAM lane has no legend row; the rows of the sections it lies in stand for it.
    private MemoryCoverageInteractionState[] ContainingStates(MemoryFocusLaneViewModel lane)
    {
        string? space = lane.Ranges[0].AddressSpaceId;
        return [.. _displaySegments.Where(slice => slice.IsPrimaryContent && slice.RangeStart < lane.EndExclusive &&
                lane.Start < slice.RangeEndExclusive && (space is null || slice.AddressSpaceId is null || slice.AddressSpaceId == space))
            .Select(static slice => slice.Interaction).Distinct()];
    }

    private double LabelCenterX(Control target)
    {
        TextBlock label = target.GetVisualDescendants().OfType<TextBlock>().First();
        return label.TranslatePoint(new Point(label.Bounds.Width / 2, 0), _track)?.X ?? RangeCenterX(target, _track);
    }

    // A tiny marker carries the true range centre of its slice(s); any other target is proportional.
    private double RangeCenterX(Control target, Control reference)
    {
        double center = target.GetValue(RangeCenterProperty);
        return double.IsNaN(center)
            ? (target.TranslatePoint(default, reference)?.X ?? 0) + (target.Bounds.Width / 2)
            : (_markers.TranslatePoint(default, reference)?.X ?? 0) + center;
    }

    private StackPanel PopupFrame(Control body, Control connector, bool above)
    {
        connector.IsHitTestVisible = false;
        var frame = new StackPanel();
        TrackInputOrigin(frame);
        frame.Children.Add(above ? body : connector);
        frame.Children.Add(above ? connector : body);
        frame.PointerEntered += (_, _) => StopCloseTimer();
        frame.PointerExited += (_, _) => StartCloseTimer();
        frame.LostFocus += (_, _) => StartCloseTimer();
        return frame;
    }

    private void Reveal(Border surface, Popup popup, bool above)
    {
        FinishReveal(surface);
        if (ReducedMotion) { return; }
        surface.Opacity = 0;
        surface.RenderTransform = TransformOperations.Parse(above ? "translateY(8px)" : "translateY(-8px)");
        Transitions transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseOut() },
        ];
        surface.Transitions = transitions;
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(surface.Transitions, transitions) && popup.IsOpen)
            {
                surface.Opacity = 1;
                surface.RenderTransform = TransformOperations.Identity;
            }
        }, DispatcherPriority.Render);
    }

    private static void FinishReveal(Border surface)
    {
        surface.Transitions = null;
        surface.Opacity = 1;
        surface.RenderTransform = TransformOperations.Identity;
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
        if (_closingOverlays) { return; }
        _closingOverlays = true;
        try { CloseCardCore(); }
        finally
        {
            _closingOverlays = false;
            if (_closeAllPending) { CloseAll(); }
        }
    }

    private void CloseCardCore()
    {
        FinishReveal(_card);
        _cardPopup.IsOpen = false;
        if (_cardPopup.Child is Panel frame) { frame.Children.Clear(); }
        _cardPopup.Child = null;
        _card.Child = null;
        _card.DataContext = null;
        _cardTarget = null;
    }

    private void CloseAll()
    {
        _passiveReopenTarget = null;
        // Popup detachment synchronously invokes input/lifecycle callbacks. Do not
        // reopen or clear its children again while Avalonia is enumerating them.
        if (_closingOverlays) { _closeAllPending = true; return; }
        _closingOverlays = true;
        try
        {
            StopCloseTimer();
            _keyboardFocus = false;
            CloseCardCore();
            FinishReveal(_local);
            _localPopup.IsOpen = false;
            if (_localPopup.Child is Panel frame) { frame.Children.Clear(); }
            _localPopup.Child = null;
            _local.Child = null;
            _sliceTargets.Clear();
            _activeGroup = null;
            _ = _groupTarget?.Classes.Remove("active");
            _groupTarget = null;
        }
        finally
        {
            _closingOverlays = false;
            _closeAllPending = false;
        }
    }
}
