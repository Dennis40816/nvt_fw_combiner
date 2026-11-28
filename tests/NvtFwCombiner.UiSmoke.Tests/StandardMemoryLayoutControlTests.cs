// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Loaded Standard inputs retain all facts in the production layered panel.</summary>
public sealed class StandardMemoryLayoutControlTests
{
    /// <summary>DP, TP and LDC details stay within the panel and leave authoring unchanged.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, false, false)]
    [InlineData(1440, 900, true, true)]
    [InlineData(980, 640, false, true)]
    [InlineData(980, 640, true, false)]
    public async Task LoadedStandardRowsRetainDetailsAndInputs(int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("standard-memory-controls");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace, width, height, dark, chinese);
        await host.LoadStandardAsync("NT51928");
        Assert.Equal(3, host.Shell.Merge.MergeSlots.Count(static slot => slot.HasFile));
        CtrlRamSelectorLayoutTests.AssertMergePanelAlignment(host.Window);
        string?[] paths = [.. host.Shell.Merge.MergeSlots.Select(static slot => slot.FilePath)];
        OutputLayoutPanelModel original = host.Model;
        host.AssertDetails(Assert.Single(original.Segments, static segment => segment.IsPrimaryContent && segment.ContentRole == MemoryContentRole.Dp));
        host.AssertDetails(Assert.Single(original.Segments, static segment => segment.IsPrimaryContent && segment.ContentRole == MemoryContentRole.Tp));
        host.AssertDetails(Assert.Single(original.Segments, static segment => segment.IsPrimaryContent && segment.ContentRole == MemoryContentRole.Ldc));
        Assert.Same(original, host.Model);
        Assert.Equal(paths, host.Shell.Merge.MergeSlots.Select(static slot => slot.FilePath));
        Assert.Equal("NT51928", host.Shell.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.StandardMerge, host.Shell.Merge.SelectedMergeMode);
        host.Capture($"standard-layout-{width}-{height}-{dark}-{chinese}");
    }
}
