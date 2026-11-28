// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Pin merging changes only viewport geometry and keeps every original row reachable.</summary>
public sealed class OutputLayoutPanelPinTests
{
    /// <summary>Exact touching edges stay separate while overlapping labels merge and retain member indices.</summary>
    [Fact]
    public void ExactPositionCollisionMergesOnlyOverlappingPins()
    {
        OutputLayoutPanelPinLayout.Candidate[] candidates =
        [
            new(0, 10, 18), new(1, 28, 18), new(2, 44, 18), new(3, 82, 18),
        ];
        IReadOnlyList<OutputLayoutPanelPinLayout.Cluster> result =
            OutputLayoutPanelPinLayout.Arrange(candidates, 100, indices => indices.Count == 1 ? 18 : 30);
        Assert.Equal(3, result.Count);
        Assert.Equal([0], result[0].Indices);
        Assert.Equal([1, 2], result[1].Indices);
        Assert.Equal([3], result[2].Indices);
        Assert.Equal((1d, 18d), (result[0].Left, result[0].Width));
        Assert.Equal((21d, 30d), (result[1].Left, result[1].Width));
        Assert.Equal((73d, 18d), (result[2].Left, result[2].Width));
        Assert.All(result, cluster => Assert.InRange(cluster.Left, 0, 100 - cluster.Width));
    }

    /// <summary>When the merged label itself collides, a second merge leaves one reachable target.</summary>
    [Fact]
    public void WiderMergedLabelAbsorbsTheNextCollision()
    {
        OutputLayoutPanelPinLayout.Candidate[] candidates =
        [
            new(0, 10, 18), new(1, 25, 18), new(2, 55, 18),
        ];
        IReadOnlyList<OutputLayoutPanelPinLayout.Cluster> result =
            OutputLayoutPanelPinLayout.Arrange(candidates, 80, indices => indices.Count == 1 ? 18 : 62);
        Assert.Equal([0, 1, 2], Assert.Single(result).Indices);
    }

    /// <summary>A narrow actual control presents one merged pin with an expandable list of original rows.</summary>
    [AvaloniaFact]
    public void DenseControlPinOpensOriginalMembers()
    {
        using var host = new OutputLayoutPanelTestHost(new(
            Enumerable.Range(0, 20).Select(index => OutputLayoutPanelTestHost.Slice(
                index * 50, (index + 1) * 50, $"Block {index}", slot: $"slot{index}"))), width: 420);
        Assert.True(host.Pins.Children.Count < host.Panel.Model!.Rows.Count);
        Border pin = Assert.IsType<Border>(host.Pins.Children.First(control =>
            ((OutputLayoutPanelRow[])control.DataContext!).Length > 1));
        OutputLayoutPanelRow[] originals = (OutputLayoutPanelRow[])pin.DataContext!;
        host.Click(pin);
        Button member = host.Find<Button>($"OutputLayout.PinMember.{originals[0].PinLabel}");
        Assert.True(member.IsEffectivelyVisible);
        host.Click(member);
        Assert.True(OutputLayoutPanelTestHost.Body(host.Row(originals[0].PinLabel)).IsVisible);
        Assert.Contains(originals[0].Members[0].DisplayTitle,
            AutomationProperties.GetName(host.Row(originals[0].PinLabel)), StringComparison.Ordinal);
        Assert.True(host.Find<Button>($"OutputLayout.PinCluster.{originals[0].FirstPin}–{originals[^1].LastPin}").Focus());
        host.Key(Key.Escape, PhysicalKey.Escape);
        Assert.True(pin.IsFocused);
        Assert.False(member.IsEffectivelyVisible);
    }
}
