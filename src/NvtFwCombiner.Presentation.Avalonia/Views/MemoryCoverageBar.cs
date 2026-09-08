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
using Avalonia.Input;
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
    /// <summary>Suppresses the informational-card reveal animation.</summary>
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<MemoryCoverageBar, bool>(nameof(ReducedMotion));

    private readonly ItemsControl _main = new() { Height = 34, ClipToBounds = false };
    private readonly Border _track = new() { Height = 34, ClipToBounds = false };
    private readonly Popup _localPopup = new() { ShouldUseOverlayLayer = true, IsLightDismissEnabled = false };
    private readonly Popup _cardPopup = new() { ShouldUseOverlayLayer = true, IsLightDismissEnabled = false };
    private readonly Border _local = Surface("MemoryLocalView");
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
    private int _revealVersion;

    /// <summary>Constructs the shared rail using existing bar, color, card and interaction owners.</summary>
    public MemoryCoverageBar()
    {
        Height = 34;
        _track.Child = _main;
        Content = new Panel { Children = { _track, _localPopup, _cardPopup } };
        _ = _track.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NfcMemoryTrackBrush"));
        MemoryCoverageInteractionBehavior.SetIsEnabled(_card, true);
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            if (!IsPointerOver && !IsKeyboardFocusWithin &&
                !IsInteracting(_localPopup.Child) && !IsInteracting(_cardPopup.Child)) { CloseAll(); }
        };
        PointerExited += (_, _) => _closeTimer.Start();
        LostFocus += (_, _) => _closeTimer.Start();
        _card.KeyDown += OnCardKeyDown;
    }

    /// <inheritdoc cref="ItemsSourceProperty" />
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <inheritdoc cref="LabelsProperty" />
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <inheritdoc cref="IsPlainProperty" />
    public bool IsPlain { get => GetValue(IsPlainProperty); set => SetValue(IsPlainProperty, value); }
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
        if (change.Property == ItemsSourceProperty || change.Property == LabelsProperty || change.Property == IsPlainProperty)
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

    private static bool IsInteracting(Control? control)
    {
        return control is { IsPointerOver: true } or { IsKeyboardFocusWithin: true };
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
                Classes = { "memoryCoverageFill", "memoryCoverageLinkedRow" },
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
        var endpoints = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        endpoints.Children.Add(new TextBlock { Text = item.StartLabel, Classes = { "technicalValue" } });
        var end = new TextBlock { Text = item.EndLabel, Classes = { "technicalValue" } };
        Grid.SetColumn(end, 1);
        endpoints.Children.Add(end);
        var content = new StackPanel { Spacing = 6 };
        double above = _track.TranslatePoint(default, TopLevel.GetTopLevel(this)!)?.Y ?? 0;
        // Keep the terminal strip at the outward edge so its card cannot cover local metadata.
        _localAbove = above >= 380;
        if (_localAbove) { content.Children.Add(strip); content.Children.Add(endpoints); }
        content.Children.Add(new TextBlock { Text = Text.MemoryLocalViewLabel, Classes = { "bodyEmphasisText" }, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = $"{item.AddressSpace} · {Text.FormatMemorySliceCount(item.Slices.Count)} · {item.SizeLabel}", Classes = { "captionText" }, TextWrapping = TextWrapping.Wrap });
        if (!_localAbove) { content.Children.Add(endpoints); content.Children.Add(strip); }
        _local.Child = content;
        var connector = new Canvas { Height = 16 };
        double left = target.TranslatePoint(default, _track)?.X ?? 0;
        foreach ((double localX, double mainX) in new[] { (12d, left), (Bounds.Width - 12, left + target.Bounds.Width) })
        {
            connector.Children.Add(Connector(new Point(localX, _localAbove ? 0 : 16), new Point(mainX, _localAbove ? 16 : 0)));
        }
        StackPanel frame = PopupFrame(_local, connector, _localAbove);
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
        if (!string.IsNullOrWhiteSpace(slice.AddressSpaceId))
        {
            content.Children.Add(new TextBlock { Text = slice.AddressSpaceId, Classes = { "captionText" } });
        }
        content.Children.Add(details);
        _card.Child = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        TopLevel top = TopLevel.GetTopLevel(this)!;
        Point origin = target.TranslatePoint(default, top) ?? default;
        double below = top.Bounds.Height - origin.Y - target.Bounds.Height;
        bool above = preferredAbove ?? (origin.Y >= 250 || origin.Y > below);
        double available = (above ? origin.Y : below) - 20;
        _card.MaxHeight = Math.Max(64, available);
        double width = Math.Min(360, Math.Min(Bounds.Width, top.Bounds.Width - 16));
        _card.Width = width;
        double center = origin.X + (target.Bounds.Width / 2);
        double left = Math.Clamp(center - (width / 2), 8, Math.Max(8, top.Bounds.Width - width - 8));
        double anchor = center - left;
        var connector = new Canvas { Height = 8 };
        connector.Children.Add(Connector(new Point(anchor, 0), new Point(anchor, 8)));
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
        _ = line.Bind(Shape.StrokeProperty, new DynamicResourceExtension("NfcAccentStrongBrush"));
        return line;
    }

    private StackPanel PopupFrame(Control body, Control connector, bool above)
    {
        var frame = new StackPanel { Background = Brushes.Transparent };
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
        CloseCard();
        _localPopup.IsOpen = false;
        if (_localPopup.Child is Panel frame) { frame.Children.Clear(); }
        _localPopup.Child = null;
        _local.Child = null;
        _sliceTargets.Clear();
        _activeGroup = null;
        _groupTarget = null;
    }
}
