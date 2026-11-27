// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Shared, display-only output map. All instance state is UI thread only.</summary>
public sealed partial class OutputMap : UserControl
{
    /// <summary>Defines the immutable display snapshot.</summary>
    public static readonly StyledProperty<OutputMapModel?> ModelProperty =
        AvaloniaProperty.Register<OutputMap, OutputMapModel?>(nameof(Model));
    /// <summary>Defines the externally bindable pinned block or region key.</summary>
    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<OutputMap, string?>(nameof(SelectedKey), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Defines the externally bindable preview key.</summary>
    public static readonly StyledProperty<string?> HoveredKeyProperty =
        AvaloniaProperty.Register<OutputMap, string?>(nameof(HoveredKey), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Defines the existing shell resource bundle.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<OutputMap, object?>(nameof(Labels));
    /// <summary>Defines the existing reduced-motion preference.</summary>
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<OutputMap, bool>(nameof(ReducedMotion));

    private readonly RelayCommand<string?> _pin;
    private readonly RelayCommand<string?> _preview;
    private readonly RelayCommand _clear;
    private readonly MapParts? _parts;
    private int _zoomRebuildDepth;
    private ZoomIdentity? _zoomIdentity;

    /// <summary>Creates an independent map; it does not subscribe to any workflow or service.</summary>
    public OutputMap()
    {
        _pin = new RelayCommand<string?>(key => SetCurrentValue(SelectedKeyProperty, key));
        _preview = new RelayCommand<string?>(key => SetCurrentValue(HoveredKeyProperty, key));
        _clear = new RelayCommand(() => { _pin.Execute(null); _preview.Execute(null); });
        InitializeComponent();
        _parts = new MapParts(this.FindControl<Canvas>("MapRegions")!, this.FindControl<Canvas>("MapBar")!,
            this.FindControl<Canvas>("MapCrc")!, this.FindControl<WrapPanel>("MapJump")!,
            this.FindControl<Border>("MapZoom")!, this.FindControl<StackPanel>("ZoomContent")!);
        _parts.Bar.SizeChanged += (_, _) => LayoutOverview();
        _parts.Regions.SizeChanged += (_, _) => LayoutOverview();
        _parts.Crc.SizeChanged += (_, _) => LayoutOverview();
        _parts.Zoom.SizeChanged += (_, e) =>
        {
            if (e.PreviousSize.Width != e.NewSize.Width) { RenderZoom(); }
        };
        Rebuild();
    }

    /// <summary>Gets or sets supplied facts; changing them replaces the visual snapshot.</summary>
    public OutputMapModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    /// <summary>Gets or sets the pinned block or region key. Null clears the pin.</summary>
    public string? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }
    /// <summary>Gets or sets the preview key, including hover from an external step table.</summary>
    public string? HoveredKey { get => GetValue(HoveredKeyProperty); set => SetValue(HoveredKeyProperty, value); }
    /// <summary>Gets or sets the shell localization resource bundle.</summary>
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <summary>Gets or sets whether interaction transitions are disabled.</summary>
    public bool ReducedMotion { get => GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }

    private OutputMapTextResources Text => new((Labels as ShellTextResources)?.Language ?? ShellLanguage.English);
    private Viewport Overview => new(0, Model?.OutputLength ?? 0);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_parts is null) { return; }
        if (change.Property == ModelProperty || change.Property == LabelsProperty) { Rebuild(); }
        else if (change.Property == SelectedKeyProperty || change.Property == HoveredKeyProperty || change.Property == ReducedMotionProperty)
        {
            RefreshInteraction();
        }
    }

    private FocusTarget? Resolve(string? key)
    {
        if (key is null || Model is null) { return null; }
        OutputMapBlock? block = Model.Blocks.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.Ordinal));
        if (block is not null) { return new(key, [block.TargetRange], block.Label, block, null); }
        OutputMapRegion? region = Model.Regions.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));
        return region is null ? null : new(key, [region.Range], region.Name, null, region);
    }

    private FocusTarget? HighlightTarget => Model?.Highlight is { Ranges.Count: > 0 } highlight
        ? new(null, highlight.Ranges, highlight.Severity == OutputMapHighlightSeverity.Error ? Text.Error : Text.Warning, null, null) : null;
    private FocusTarget? FocusedTarget => Resolve(HoveredKey) ?? Resolve(SelectedKey) ?? HighlightTarget;
    private static string Hex(long address)
    {
        return FormattableString.Invariant($"0x{address:X5}");
    }
    private string CoordinateLabel(double address)
    {
        return Hex(address >= (Model?.OutputLength ?? 0)
        ? Model?.OutputLength ?? 0 : (long)Math.Max(0, address));
    }
    private static string RangeLabel(ByteRange range)
    {
        return $"[{Hex(range.Start)}, {Hex(range.EndExclusive)})";
    }
    private string OutputRange(ByteRange range)
    {
        return $"{Model?.AddressSpaceId} {RangeLabel(range)}";
    }

    private sealed record MapParts(Canvas Regions, Canvas Bar, Canvas Crc, WrapPanel Jump, Border Zoom, StackPanel ZoomContent);
    private sealed record FocusTarget(string? Key, IReadOnlyList<ByteRange> Ranges, string Name, OutputMapBlock? Block, OutputMapRegion? Region);
    private sealed record Placement(ByteRange Range, double MinimumWidth, bool Marker);
    private sealed record ZoomIdentity(OutputMapModel? Model, object? Labels, string? FocusKey);
    private readonly record struct Viewport(double Start, double End)
    {
        internal double Length => End - Start;
    }
}
