using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>A CtrlRAM focus lane exists only for a range the current topology can bind.</summary>
public sealed class MemoryFocusLaneTopologyTests
{
    /// <summary>The cascade-only DIFF range (Base group) is a lane on 2 IC cascade and context only on single IC.</summary>
    [Theory]
    [InlineData(true, new[] { "Master" })]
    [InlineData(false, new[] { "Common", "•" })]
    public void UnboundBaseCtrlRamRangeHasNoLaneOnSingleIc(bool isSingleIc, string[] positionLabels)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var normal = new MemoryCoverageSegmentViewModel("flash [0x25610,0x2B210)", "Normal CtrlRAM", "detail",
            MemoryCoverageFillRole.CtrlRamNormal, 0x5C00, regionGroup: ReplaceRegionGroup.Common,
            rangeStart: 0x25610, rangeEndExclusive: 0x2B210, contentRole: MemoryContentRole.CtrlRam, addressSpaceId: "flash");
        var diff = new MemoryCoverageSegmentViewModel("flash [0x33200,0x34600)", "DIFF CtrlRAM", "detail",
            MemoryCoverageFillRole.DiffDlm, 0x1400, regionGroup: ReplaceRegionGroup.Base,
            rangeStart: 0x33200, rangeEndExclusive: 0x34600, contentRole: MemoryContentRole.CtrlRam, addressSpaceId: "flash");
        IReadOnlyList<MemoryFocusLaneViewModel> lanes = MemoryFocusLaneViewModel.Create(
            [new MemoryCoverageLogicalItemViewModel("normal", [normal], text), new MemoryCoverageLogicalItemViewModel("diff", [diff], text)],
            text, isSingleIc);
        Assert.Equal(positionLabels, lanes.Select(static lane => lane.PositionLabel));
    }
}
