using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Renders the approved compact checking ring with a static reduced-motion state.</summary>
public sealed class VersionCheckingIndicator : Control
{
    private static readonly Geometry ArcGeometry = Geometry.Parse(
        "M 9,1.5 A 7.5,7.5 0 1 1 3.697,3.697");

    /// <summary>Defines the quiet circular track brush.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<VersionCheckingIndicator, IBrush?>(nameof(TrackBrush));

    /// <summary>Defines the active arc brush.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<VersionCheckingIndicator, IBrush?>(nameof(IndicatorBrush));

    /// <summary>Defines whether the arc remains static.</summary>
    public static readonly StyledProperty<bool> IsReducedMotionEnabledProperty =
        AvaloniaProperty.Register<VersionCheckingIndicator, bool>(nameof(IsReducedMotionEnabled));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private double _angle;
    private bool _isAttached;

    static VersionCheckingIndicator()
    {
        AffectsRender<VersionCheckingIndicator>(
            TrackBrushProperty,
            IndicatorBrushProperty,
            IsReducedMotionEnabledProperty);
    }

    /// <summary>Creates the bounded UI-thread animation timer.</summary>
    public VersionCheckingIndicator()
    {
        _timer.Tick += Timer_Tick;
    }

    /// <summary>Gets or sets the quiet circular track brush.</summary>
    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>Gets or sets the active arc brush.</summary>
    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    /// <summary>Gets or sets whether the ring uses its static treatment.</summary>
    public bool IsReducedMotionEnabled
    {
        get => GetValue(IsReducedMotionEnabledProperty);
        set => SetValue(IsReducedMotionEnabledProperty, value);
    }

    internal static bool ShouldAnimate(bool isAttached, bool isVisible, bool isReducedMotionEnabled)
    {
        return isAttached && isVisible && !isReducedMotionEnabled;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Render(context);
        if (TrackBrush is { } track)
        {
            context.DrawEllipse(
                brush: null,
                new Pen(track, 2),
                new Point(9, 9),
                radiusX: 7.5,
                radiusY: 7.5);
        }
        if (IndicatorBrush is not { } indicator)
        {
            return;
        }

        double radians = IsReducedMotionEnabled ? 0 : _angle * Math.PI / 180;
        Matrix transform = Matrix.CreateTranslation(-9, -9) *
                           Matrix.CreateRotation(radians) *
                           Matrix.CreateTranslation(9, 9);
        using (context.PushTransform(transform))
        {
            context.DrawGeometry(brush: null, new Pen(indicator, 2), ArcGeometry);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        UpdateAnimation();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property == IsReducedMotionEnabledProperty)
        {
            UpdateAnimation();
        }
    }

    private void UpdateAnimation()
    {
        if (ShouldAnimate(_isAttached, IsVisible, IsReducedMotionEnabled))
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _angle = 0;
            InvalidateVisual();
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _angle = (_angle + 18) % 360;
        InvalidateVisual();
    }
}
