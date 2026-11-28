// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regressions for the independently reviewed standalone zoom interactions and host contract.</summary>
public sealed partial class CtrlRamZoomBarTests
{
    /// <summary>Neutral anchors never replace the published lane title in visible or accessible headings.</summary>
    [AvaloniaTheory]
    [InlineData(ReplaceRegionGroup.Base, false)]
    [InlineData(ReplaceRegionGroup.Other, false)]
    [InlineData(ReplaceRegionGroup.Base, true)]
    [InlineData(ReplaceRegionGroup.Other, true)]
    public void NeutralLaneUsesItsTitleInsteadOfTheBarAnchor(ReplaceRegionGroup group, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        MemoryFocusLaneViewModel lane = DetailedLane(group, text);
        Assert.Equal("•", lane.PositionLabel);
        using OutputLayoutPartsTestHost host = new(CtrlRamZoomDisplay.FromLane(lane));
        host.Zoom.Labels = text;
        host.Layout();
        Button position = host.Find<Button>("CtrlRamZoom.Position");
        Assert.Equal($"{lane.Title} · {text.MemoryCtrlRamDetailLabel} · {host.Zoom.Data!.SizeLabel} · flash",
            Assert.IsType<TextBlock>(position.Content).Text);
        Assert.Equal($"{lane.Title} · {text.MemoryCtrlRamDetailLabel} · flash · {lane.RangeLabel}",
            ControlAutomationPeer.CreatePeerForElement(position).GetName());
    }

    /// <summary>Publishing while disabled must not strand the position command after its ancestor is enabled.</summary>
    [AvaloniaFact]
    public void PositionReenablesAfterDataWasPublishedWhileTheHostWasDisabled()
    {
        using OutputLayoutPartsTestHost host = new(null);
        host.Window.IsEnabled = false;
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane());
        host.Layout();
        Button position = host.Find<Button>("CtrlRamZoom.Position");
        Assert.False(position.IsEffectivelyEnabled);
        host.Window.IsEnabled = true;
        host.Layout();
        Assert.True(position.IsEffectivelyEnabled);
        Assert.True(position.Focus());
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Assert.Equal(-1, host.Zoom.SelectedIndex);
        host.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        host.Layout();
        Assert.Equal(0, host.Zoom.SelectedIndex);
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.0").IsFocused);
    }

    /// <summary>Selecting a block leaves details with the host and preserves every original display fact.</summary>
    [AvaloniaFact]
    public void SelectionDoesNotRenderADetailSectionAndRetainsCompleteHostFacts()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryFocusLaneViewModel lane = DetailedLane(ReplaceRegionGroup.Master, text);
        using OutputLayoutPartsTestHost host = new(CtrlRamZoomDisplay.FromLane(lane));
        double height = host.Zoom.Bounds.Height;
        host.Zoom.SelectedIndex = 0;
        host.Layout();
        Assert.Equal(height, host.Zoom.Bounds.Height);
        Assert.DoesNotContain(host.Zoom.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsEffectivelyVisible && block.Text == lane.Ranges[0].Detail);
        CtrlRamZoomSlice item = Assert.Single(host.Zoom.Data!.Slices);
        MemoryCoverageSegmentViewModel original = Assert.Single(lane.Ranges);
        Assert.Equal(original.SourceLabel, item.SourceLabel);
        Assert.Equal(original.SourceFieldLabel, item.SourceFieldLabel);
        Assert.Equal(original.Detail, item.Detail);
        Assert.Equal(original.PreservationSummary, item.PreservationSummary);
        Assert.Equal(original.DetailsLabel, item.DetailsLabel);
        Assert.Equal(original.PreservationDetails, item.PreservationDetails);
        Assert.Equal(original.ProcessingFacts, item.ProcessingFacts);
        Assert.NotEmpty(item.PreservationDetails);
        Assert.NotEmpty(item.ProcessingFacts);
    }

    /// <summary>Snapshot publication must not write a transient cleared value through a host binding or lose focus.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepublishingDataPreservesBoundSelectionAndFocusOnTheEquivalentTarget(bool tiny)
    {
        using OutputLayoutPartsTestHost host = new(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(tiny: !tiny)));
        OutputLayoutPartsSelection selection = new() { Index = 1 };
        using IDisposable binding = host.Zoom.Bind(CtrlRamZoomBar.SelectedIndexProperty,
            new Binding(nameof(selection.Index)) { Source = selection });
        List<int> writes = [];
        selection.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(selection.Index)) { writes.Add(selection.Index); }
        };
        Button old = host.Find<Button>($"CtrlRamZoom.{(!tiny ? "Tiny" : "Slice")}.1");
        Assert.True(old.Focus());
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane(tiny: tiny, chinese: true));
        host.Layout();
        Assert.Equal(1, selection.Index);
        Assert.Equal(1, host.Zoom.SelectedIndex);
        Assert.Empty(writes);
        Assert.True(host.Find<Button>($"CtrlRamZoom.{(tiny ? "Tiny" : "Slice")}.1").IsFocused);
        Assert.Null(old.GetVisualParent());
        selection.Index = 2;
        host.Layout();
        Assert.Equal(2, host.Zoom.SelectedIndex);
    }

    /// <summary>Escape clears a selected block once, then bubbles to the host when no block remains selected.</summary>
    [AvaloniaFact]
    public void EscapeBubblesOnlyWhenThereIsNoSelectedBlock()
    {
        using OutputLayoutPartsTestHost host = new(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        int escapes = 0;
        host.Window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { escapes++; }
        });
        host.Zoom.SelectedIndex = 1;
        Assert.True(host.Find<Button>("CtrlRamZoom.Slice.1").Focus());
        host.Press(Key.Escape, PhysicalKey.Escape);
        Assert.Equal(-1, host.Zoom.SelectedIndex);
        Assert.Equal(0, escapes);
        Assert.True(host.Find<Button>("CtrlRamZoom.Position").IsFocused);
        host.Press(Key.Escape, PhysicalKey.Escape);
        Assert.Equal(1, escapes);
    }

    /// <summary>Changing label fit transfers focus between equivalent targets and exposes only one automation stop.</summary>
    [AvaloniaFact]
    public void ResizeTransfersFocusAndExcludesUnlabelledRailButtonsFromAutomation()
    {
        using OutputLayoutPartsTestHost host = new(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        Button rail = host.Find<Button>("CtrlRamZoom.Slice.0");
        Button tiny = host.Find<Button>("CtrlRamZoom.Tiny.0");
        host.Zoom.SelectedIndex = 0;
        Assert.True(rail.Focus());
        host.Window.Width = 120;
        host.Layout();
        Assert.True(tiny.IsEffectivelyVisible);
        Assert.True(tiny.IsFocused);
        Assert.False(rail.Focusable);
        Assert.False(ControlAutomationPeer.CreatePeerForElement(rail).IsControlElement());
        Assert.False(ControlAutomationPeer.CreatePeerForElement(rail).IsContentElement());
        Assert.True(ControlAutomationPeer.CreatePeerForElement(tiny).IsControlElement());
        host.Window.Width = 960;
        host.Layout();
        Assert.False(tiny.IsEffectivelyVisible);
        Assert.True(rail.IsFocused);
        Assert.True(rail.Focusable);
        Assert.True(ControlAutomationPeer.CreatePeerForElement(rail).IsControlElement());
        Assert.Equal(0, host.Zoom.SelectedIndex);
    }

    /// <summary>The standalone heading reuses the shell wording in both supported languages.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZoomHeadingReusesTheExistingDetailLabel(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        using OutputLayoutPartsTestHost host = new(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        host.Zoom.Labels = text;
        host.Layout();
        Button position = host.Find<Button>("CtrlRamZoom.Position");
        Assert.Contains(text.MemoryCtrlRamDetailLabel, Assert.IsType<TextBlock>(position.Content).Text, StringComparison.Ordinal);
        Assert.Contains(text.MemoryCtrlRamDetailLabel, AutomationProperties.GetName(position), StringComparison.Ordinal);
    }

    private static MemoryFocusLaneViewModel DetailedLane(ReplaceRegionGroup group, ShellTextResources text)
    {
        MemoryCoverageSegmentViewModel item = new("flash [0x1000,0x1400)", "Reference BIN", "Complete detail for host rows.",
            MemoryCoverageFillRole.CtrlRamNf, 1, regionId: "nf", regionGroup: group, text: text,
            rangeStart: 0x1000, rangeEndExclusive: 0x1400, addressSpaceId: "flash",
            contentRole: MemoryContentRole.CtrlRam, ctrlRamRegionRole: CtrlRamRegionRole.Nf,
            sourceFieldLabel: "Reference source", processingFacts: [new("Operation", "preserve-nf")],
            preservationDetails: [new("kept", 0, MemoryEndpointIdentity.NotApplicable, "reference-base",
                new ByteRange(0, 0x10), new ByteRange(0x1000, 0x10))]);
        return Assert.Single(MemoryFocusLaneViewModel.Create([new MemoryCoverageLogicalItemViewModel("nf", [item], text)], text));
    }
}
