using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Locks typed logical grouping independently from exact bar geometry.</summary>
public sealed class MemoryCoverageLogicalGroupingTests
{
    /// <summary>Compatible adjacent fragments render as one partial row without changing exact bar geometry.</summary>
    [Fact]
    public void CompatibleAdjacentFragmentsCoalesceOnlyInSupportingRows()
    {
        MemoryCoverageSegmentViewModel written = Segment(
            start: 0,
            endExclusive: 4,
            disposition: MemoryWorkflowDisposition.WillReplace,
            sourceSlotId: "ctrlram",
            usesBase: false);
        MemoryCoverageSegmentViewModel kept = Segment(
            start: 4,
            endExclusive: 8,
            disposition: MemoryWorkflowDisposition.Kept,
            sourceSlotId: "reference-base",
            usesBase: true);
        MemoryCoverageSegmentViewModel[] exactBarSegments = [written, kept];

        MemoryCoverageLogicalItemViewModel item = Assert.Single(
            ReplaceRegionGroupBuilder.CreateLogicalItems(
                exactBarSegments,
                ShellTextResources.For(ShellLanguage.English)));

        MemoryCoverageSegmentViewModel row = Assert.Single(item.Ranges);
        Assert.Equal("0x00000-0x00007", row.AddressRangeLabel);
        Assert.Equal("Partially replaced", row.ChangeLabel);
        Assert.Equal(2, item.Segments.Count);
        Assert.Same(written, item.Segments[0]);
        Assert.Same(kept, item.Segments[1]);
        Assert.Equal([4d, 4d], item.Segments.Select(static segment => segment.BarWidth));
        Assert.Same(item.Interaction, written.Interaction);
        Assert.Same(item.Interaction, kept.Interaction);
        Assert.Same(item.Interaction, row.Interaction);
    }

    /// <summary>Nonadjacent or semantically different fragments remain separate visible ranges.</summary>
    [Fact]
    public void IncompatibleFragmentsDoNotCoalesce()
    {
        MemoryCoverageSegmentViewModel baseline = Segment(0, 2);
        MemoryCoverageSegmentViewModel nonAdjacent = Segment(4, 6);
        MemoryCoverageSegmentViewModel differentContent = Segment(
            2,
            4,
            contentRole: MemoryContentRole.Dp);
        MemoryCoverageSegmentViewModel differentGroup = Segment(
            2,
            4,
            regionGroup: ReplaceRegionGroup.Master);
        MemoryCoverageSegmentViewModel differentCtrlRamRole = Segment(
            2,
            4,
            ctrlRamRegionRole: CtrlRamRegionRole.Vn);

        Assert.Equal(2, CreateItem(baseline, nonAdjacent).Ranges.Count);
        Assert.Equal(2, CreateItem(baseline, differentContent).Ranges.Count);
        Assert.Equal(2, CreateItem(baseline, differentGroup).Ranges.Count);
        Assert.Equal(2, CreateItem(baseline, differentCtrlRamRole).Ranges.Count);
    }

    /// <summary>Partial-row state is localized from the shared resource contract.</summary>
    [Theory]
    [InlineData(false, "Partially replaced")]
    [InlineData(true, "部分替換")]
    public void PartialRowsUseLocalizedState(bool useTraditionalChinese, string expected)
    {
        MemoryCoverageSegmentViewModel written = Segment(
            0,
            2,
            disposition: MemoryWorkflowDisposition.WillReplace,
            sourceSlotId: "ctrlram");
        MemoryCoverageSegmentViewModel kept = Segment(
            2,
            4,
            disposition: MemoryWorkflowDisposition.Kept,
            sourceSlotId: "reference-base",
            usesBase: true);

        MemoryCoverageLogicalItemViewModel item = Assert.Single(
            ReplaceRegionGroupBuilder.CreateLogicalItems(
                [written, kept],
                ShellTextResources.For(useTraditionalChinese
                    ? ShellLanguage.ChineseTraditional
                    : ShellLanguage.English)));

        Assert.Equal(expected, Assert.Single(item.Ranges).ChangeLabel);
    }

    /// <summary>Presentation fails closed when Application omits the typed grouping identity.</summary>
    [Fact]
    public void MissingLogicalCoverageIdentityFailsClosed()
    {
        MemoryCoverageSegmentViewModel missing = new(
            "0x00000-0x00001",
            "Source",
            "detail",
            MemoryCoverageFillRole.CtrlRamNf,
            2,
            rangeStart: 0,
            rangeEndExclusive: 2);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ReplaceRegionGroupBuilder.CreateLogicalItems(
                [missing],
                ShellTextResources.For(ShellLanguage.English)));
        Assert.Contains("Application memory projection", exception.Message, StringComparison.Ordinal);
    }

    private static MemoryCoverageLogicalItemViewModel CreateItem(
        params MemoryCoverageSegmentViewModel[] segments)
    {
        return Assert.Single(
            ReplaceRegionGroupBuilder.CreateLogicalItems(
                segments,
                ShellTextResources.For(ShellLanguage.English)));
    }

    private static MemoryCoverageSegmentViewModel Segment(
        long start,
        long endExclusive,
        MemoryWorkflowDisposition disposition = MemoryWorkflowDisposition.WillReplace,
        string sourceSlotId = "ctrlram",
        bool usesBase = false,
        MemoryContentRole contentRole = MemoryContentRole.CtrlRam,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common,
        CtrlRamRegionRole ctrlRamRegionRole = CtrlRamRegionRole.Nf)
    {
        return new MemoryCoverageSegmentViewModel(
            FormattableString.Invariant($"0x{start:X5}-0x{endExclusive - 1:X5}"),
            "NF CtrlRAM",
            "detail",
            MemoryCoverageFillRole.CtrlRamNf,
            endExclusive - start,
            disposition: disposition,
            usesBaseFirmwarePattern: usesBase,
            regionId: "nf-ctrlram",
            sourceSlotId: sourceSlotId,
            regionGroup: regionGroup,
            rangeStart: start,
            rangeEndExclusive: endExclusive,
            logicalCoverageGroupId: "slot:ctrlram",
            contentRole: contentRole,
            ctrlRamRegionRole: ctrlRamRegionRole);
    }
}
