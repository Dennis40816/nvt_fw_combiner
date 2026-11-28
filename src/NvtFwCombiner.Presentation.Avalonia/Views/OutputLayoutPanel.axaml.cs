// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Standalone layered output layout. All interaction state is UI thread only.</summary>
public sealed partial class OutputLayoutPanel : UserControl
{
    internal static readonly StyledProperty<OutputLayoutPanelModel?> ModelProperty =
        AvaloniaProperty.Register<OutputLayoutPanel, OutputLayoutPanelModel?>(nameof(Model));
    /// <summary>Defines the existing shell text resource bundle.</summary>
    public static readonly StyledProperty<object?> LabelsProperty =
        AvaloniaProperty.Register<OutputLayoutPanel, object?>(nameof(Labels));
    /// <summary>Defines the shell reduced-motion preference.</summary>
    public static readonly StyledProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.Register<OutputLayoutPanel, bool>(nameof(ReducedMotion));

    private readonly OutputLayoutPanelPresenter _presenter;

    /// <summary>Creates a panel without subscribing to a page, session or service.</summary>
    public OutputLayoutPanel()
    {
        InitializeComponent();
        _presenter = new(this);
    }

    internal OutputLayoutPanelModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    /// <summary>Gets or sets the shell localization bundle; English is the default.</summary>
    public object? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <summary>Gets or sets reduced motion. Panel disclosures and highlights are immediate in both modes.</summary>
    public bool ReducedMotion { get => GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }
    /// <summary>Raised by the explicit Issues action, with the existing coverage segment as context.</summary>
    public event EventHandler<OutputLayoutIssueRequestedEventArgs>? IssueRequested;

    internal void RequestIssue(MemoryCoverageSegmentViewModel segment)
    {
        IssueRequested?.Invoke(this, new(segment));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_presenter is null) { return; }
        if (change.Property == ModelProperty || change.Property == LabelsProperty) { _presenter.Rebuild(); }
        if (change.Property == ReducedMotionProperty) { _presenter.RefreshMotion(); }
    }
}

/// <summary>Host routing context for an explicit Issues navigation request.</summary>
public sealed class OutputLayoutIssueRequestedEventArgs : EventArgs
{
    internal OutputLayoutIssueRequestedEventArgs(MemoryCoverageSegmentViewModel segment)
    {
        Segment = segment;
    }
    internal MemoryCoverageSegmentViewModel Segment { get; }
    /// <summary>Gets the existing profile-owned region identifier, when present.</summary>
    public string? RegionId => Segment.RegionId;
    /// <summary>Gets the existing input slot identifier, when present.</summary>
    public string? SourceSlotId => Segment.SourceSlotId;
}
