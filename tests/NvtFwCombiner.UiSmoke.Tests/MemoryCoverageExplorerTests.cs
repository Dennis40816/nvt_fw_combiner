using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Production workflows expose complete source facts through inline panel disclosure.</summary>
public sealed class MemoryCoverageExplorerTests
{
    /// <summary>Merge and Replace reveal exact ranges and sources through the same accessible row path.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProductionMapsExposeSharedSliceExplorer(bool replace, bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("memory-explorer");
        if (replace)
        {
            await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
                dark: darkChinese, chinese: darkChinese);
            await host.LoadGeneralAsync(workspace);
            OutputLayoutPanelModel model = host.Model;
            Assert.NotEmpty(model.Rows);
            Assert.Same(host.Shell.Replace.OutputLayout.Current.Panel, host.Panel.Model);
            MemoryCoverageSegmentViewModel selected = model.Rows.SelectMany(static row => row.Members)
                .First(static segment => segment.IsSelectedForWrite);
            host.AssertDetails(selected);
            host.Capture($"memory-replace-inline-{darkChinese}");
        }
        else
        {
            await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace,
                dark: darkChinese, chinese: darkChinese);
            await host.LoadStandardAsync();
            Assert.NotEmpty(host.Model.Rows);
            MemoryCoverageSegmentViewModel selected = host.Model.Rows.SelectMany(static row => row.Members)
                .First(static segment => segment.IsSelectedForWrite);
            host.AssertDetails(selected);
            host.Capture($"memory-merge-inline-{darkChinese}");
        }
    }

    /// <summary>Canonical CtrlRAM keeps physical role, partial preservation and complete inline details.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionCtrlRamWindowShowsReferenceAlignedLocalCard(bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("memory-ctrlram-reference");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1440, height: 900, dark: darkChinese, chinese: darkChinese);
        await XamlControlStyleContractTests.LoadNt51950GoldenCtrlRamInputsAsync(
            host.Shell, TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.DoesNotContain(host.Shell.Replace.ReplaceMemoryRows,
            static row => row.RangeLabel.StartsWith("0x0A11C", StringComparison.Ordinal));
        Assert.Contains(host.Model.Segments, static segment => !segment.IsPrimaryContent && segment.HasProcessingFacts);
        Assert.DoesNotContain(host.Shell.Replace.ReplaceCoverageGroups.SelectMany(static group => group.Items)
            .SelectMany(static item => item.Segments), static segment => !segment.IsPrimaryContent);
        foreach (CtrlRamRegionRole role in new[] { CtrlRamRegionRole.Nf, CtrlRamRegionRole.Normal, CtrlRamRegionRole.Vn })
        {
            Assert.Contains(host.Model.RowSegments,
                segment => segment.CtrlRamRegionRole == role && segment.IsSelectedForWrite);
            MemoryCoverageSegmentViewModel selected = host.Model.RowSegments.First(
                segment => segment.CtrlRamRegionRole == role && segment.IsSelectedForWrite);
            OutputLayoutPreparedLaneLink lane = Assert.Single(host.Model.LaneLinks,
                link => link.Row == OutputLayoutRowIdentity.For(selected));
            Assert.Equal(role, selected.CtrlRamRegionRole);
            Assert.NotNull(lane.Zoom);
            if (role == CtrlRamRegionRole.Nf)
            {
                Assert.Equal(2816, selected.DisplayParts.Where(static part => part.IsSelectedForWrite)
                    .Sum(static part => part.RangeEndExclusive!.Value - part.RangeStart!.Value));
                Assert.Equal(7952, selected.DisplayParts.Where(static part => part.UsesKeptPattern)
                    .Sum(static part => part.RangeEndExclusive!.Value - part.RangeStart!.Value));
            }
            host.AssertDetails(selected);
            host.Capture($"memory-ctrlram-inline-{darkChinese}-{role}");
        }
        Assert.DoesNotContain(host.Window.GetVisualDescendants(),
            static control => control.Name is "MemorySliceCard" or "MemoryLocalView");
    }
}
