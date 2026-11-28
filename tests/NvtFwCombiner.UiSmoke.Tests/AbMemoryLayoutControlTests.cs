// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Loaded AB inputs retain physical-bank facts in the production layered panel.</summary>
public sealed class AbMemoryLayoutControlTests
{
    /// <summary>DP and both TP banks expose exact supplied details without changing input selection.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, false, false)]
    [InlineData(1440, 900, true, true)]
    [InlineData(980, 640, false, true)]
    [InlineData(980, 640, true, false)]
    public async Task LoadedAbRowsRetainBothBanksAndDetails(int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("ab-memory-controls");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace, width, height, dark, chinese);
        await host.LoadAbAsync();
        Assert.All(host.Shell.Merge.AbMergeSlots, static slot => Assert.True(slot.HasFile));
        CtrlRamSelectorLayoutTests.AssertMergePanelAlignment(host.Window);
        OutputLayoutPanelModel original = host.Model;
        MemoryCoverageSegmentViewModel dp = Assert.Single(original.Segments, static segment => segment.RangeStart == 0);
        MemoryCoverageSegmentViewModel a = Assert.Single(original.Segments, static segment => segment.RangeStart == 0x7000);
        MemoryCoverageSegmentViewModel b = Assert.Single(original.Segments, static segment => segment.RangeStart == 0x47000);
        Assert.Equal(0x7000, dp.RangeEndExclusive);
        Assert.Equal(0x40000, a.RangeEndExclusive);
        Assert.Equal(0x80000, b.RangeEndExclusive);
        host.AssertDetails(dp);
        host.AssertDetails(a);
        host.AssertDetails(b);
        Assert.Same(original, host.Model);
        Assert.Equal("NT51929", host.Shell.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.AbMerge, host.Shell.Merge.SelectedMergeMode);
        host.Capture($"ab-layout-{width}-{height}-{dark}-{chinese}");
    }
}
