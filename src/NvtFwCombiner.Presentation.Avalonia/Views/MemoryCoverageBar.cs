using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    /// <summary>Optional exact positions whose contiguous lanes open through the shared local-view overlay.</summary>
    public static readonly StyledProperty<IEnumerable?> FocusPositionsProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, IEnumerable?>(nameof(FocusPositions));
    /// <summary>Suppresses the informational-card reveal animation.</summary>
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(ReducedMotion));

    private readonly ItemsControl _main = new() { Height = 34, ClipToBounds = false };
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
        Content = new Panel { Children = { new StackPanel { Children = { _track, _positions } }, _localPopup, _cardPopup } };
        _ = _track.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcMemoryTrackBrush"));
        _track.PointerMoved += (_, e) =>
        {
            if (_main.GetVisualDescendants().OfType<ProportionalStackPanel>().FirstOrDefault() is { } panel &&
                panel.Children.Any(child => child.DataContext is MemoryCoverageBarItem { IsPrimaryContent: false } &&
                    child.Bounds.Contains(e.GetPosition(panel)))) { CloseAll(); }
        };
        MemoryCoverageInteractionBehavior.SetIsEnabled(_card, true);
        PointerExited += (_, _) => StartCloseTimer();
        LostFocus += (_, _) => StartCloseTimer();
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
        if (change.Property == ItemsSourceProperty || change.Property == LabelsProperty || change.Property == IsPlainProperty || change.Property == ShowLabelsProperty || change.Property == FocusPositionsProperty)
        {
            Subscribe();
            Rebuild();
        }
        else if (change.Property == BoundsProperty || change.Property == IsVisibleProperty || change.Property == IsEffectivelyEnabledProperty)
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
        _positionCollection?.CollectionChanged -= OnCollectionChanged;
        _positionCollection = FocusPositions as INotifyCollectionChanged;
        _positionCollection?.CollectionChanged += OnCollectionChanged;
    }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { Rebuild(); }

    private void Rebuild()
    {
        CloseAll();
        BuildPositions();
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
            WatchTargetExit(target);
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

    private void BuildPositions()
    {
        MemoryFocusPositionViewModel[] positions = FocusPositions?.Cast<MemoryFocusPositionViewModel>().ToArray() ?? [];
        _positions.IsVisible = positions.Length > 0;
        Height = positions.Length > 0 ? 62 : 34;
        _positions.ItemContainerTheme = (ControlTheme)this.FindResource("ProportionalContentPresenterTheme")!;
        _positions.ItemsPanel = new FuncTemplate<Panel?>(() => new ProportionalStackPanel());
        _positions.ItemTemplate = new FuncDataTemplate<MemoryFocusPositionViewModel>((position, scope) =>
        {
            if (position?.Lane is not { } lane) { return new Border { IsHitTestVisible = false }; }
            var marker = new Border { Height = 2, CornerRadius = new CornerRadius(1), Classes = { "memoryPositionUnderline" } };
            _ = marker.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcAccentBrush"));
            Grid.SetRow(marker, 1);
            var target = new Border
            {
                Name = "MemoryFocusPosition",
                DataContext = position,
                Focusable = true,
                FocusAdorner = null,
                RenderTransformOrigin = RelativePoint.Center,
                Classes = { "memoryExplorerGroup", "memoryFocusPosition" },
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,2"),
                    Children =
                {
                    marker,
                    new TextBlock { Text = position.Label, Classes = { "memoryPositionLabel" }, FontSize = 11, FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                }
                },
            };
            target.Classes.Set("reducedMotion", ReducedMotion);
            WatchTargetExit(target);
            AutomationProperties.SetName(target, $"{lane.Title} · {lane.RangeLabel}");
            AutomationProperties.SetHelpText(target, Text.MemoryLocalViewHint);
            var item = new MemoryCoverageBarItem(lane.Ranges);
            target.PointerEntered += (_, _) => OpenLocal(item, target, lane);
            target.GotFocus += (_, _) => { if (!_restoringFocus) { OpenLocal(item, target, lane); } };
            target.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up)
                {
                    OpenLocal(item, target, lane);
                    _ = _sliceTargets.FirstOrDefault()?.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape) { CloseAll(); e.Handled = true; }
            };
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
                KeepLabelUnscaled(control, label);
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
        WatchTargetExit(target);
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

    private void WatchTargetExit(Control target)
    {
        target.PointerExited += (_, _) => StartCloseTimer();
        target.LostFocus += (_, _) => StartCloseTimer();
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

    private void OpenLocal(MemoryCoverageBarItem item, Control target, MemoryFocusLaneViewModel? lane = null)
    {
        StopCloseTimer();
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
        var content = new StackPanel { Spacing = 6, Name = lane is null ? null : "CtrlRamFocusLane", DataContext = lane };
        TopLevel top = TopLevel.GetTopLevel(this)!;
        Control anchor = lane is null ? _track : _positions;
        double above = anchor.TranslatePoint(default, top)?.Y ?? 0;
        _localAbove = above > top.Bounds.Height - above - anchor.Bounds.Height;
        var header = new StackPanel { Name = "MemoryLocalHeader", Spacing = 3 };
        header.Children.Add(new TextBlock { Text = lane?.Title ?? Text.MemoryLocalViewLabel, Classes = { "bodyEmphasisText" }, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left });
        header.Children.Add(new TextBlock { Text = $"{(lane is null ? Text.FormatMemorySliceCount(item.Slices.Count) : Text.MemoryCtrlRamDetailLabel)} · {item.SizeLabel} · {item.AddressSpace}", Classes = { "captionText" }, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(header);
        var rail = new Border { Name = "MemoryLocalRail", Child = strip, BorderThickness = new Thickness(1), Padding = new Thickness(2), CornerRadius = new CornerRadius(5) };
        _ = rail.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("NfcBorderMutedBrush"));
        content.Children.Add(rail);
        content.Children.Add(endpoints);
        _local.Child = content;
        var connector = new Canvas { Height = 32 };
        double left = target.TranslatePoint(default, anchor)?.X ?? 0;
        foreach ((double localX, double mainX) in new[] { (3d, left), (Bounds.Width - 3, left + target.Bounds.Width) })
        {
            connector.Children.Add(MemoryCoverageConnectorVisuals.Connector(new Point(localX, _localAbove ? 0 : 32), new Point(mainX, _localAbove ? 32 : 0)));
        }
        StackPanel frame = PopupFrame(_local, connector, _localAbove);
        _localPopup.Child = frame;
        _localPopup.Width = Bounds.Width;
        _localPopup.PlacementTarget = anchor;
        _localPopup.Placement = _localAbove ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        _localPopup.IsOpen = true;
        Reveal(_local, _localPopup, _localAbove);
    }

    private void OpenCard(Control target, MemoryCoverageSegmentViewModel slice, bool? preferredAbove)
    {
        StopCloseTimer();
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
            ? Math.Max(20, (above ? origin.Y - localOrigin.Y : localOrigin.Y + _local.Bounds.Height - origin.Y - target.Bounds.Height) + 4)
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
        Canvas connector = MemoryCoverageConnectorVisuals.CardConnector(anchor, connectorHeight, above, labels);
        _cardPopup.Child = PopupFrame(_card, connector, above);
        _cardPopup.Width = width;
        _cardPopup.PlacementTarget = target;
        _cardPopup.HorizontalOffset = left - origin.X;
        _cardPopup.Placement = above ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        _cardPopup.IsOpen = true;
        Reveal(_card, _cardPopup, above);
    }


    private StackPanel PopupFrame(Control body, Control connector, bool above)
    {
        var frame = new StackPanel { Background = Brushes.Transparent };
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
        StopCloseTimer();
        _keyboardFocus = false;
        CloseCard();
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
}
