using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
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

    /// <summary>One source file can cover disconnected ranges without linking their hover or keyboard lift.</summary>
    [Fact]
    public void SameSourceDisconnectedRangesHaveIndependentInteraction()
    {
        MemoryCoverageSegmentViewModel first = Segment(0, 4);
        MemoryCoverageSegmentViewModel adjacent = Segment(4, 8);
        MemoryCoverageSegmentViewModel separated = Segment(12, 16);
        MemoryCoverageLogicalItemViewModel item = CreateItem(first, adjacent, separated);
        Assert.Equal(2, item.Ranges.Count);
        Assert.Same(first.Interaction, adjacent.Interaction);
        Assert.Same(first.Interaction, item.Ranges[0].Interaction);
        Assert.Same(separated.Interaction, item.Ranges[1].Interaction);
        Assert.NotSame(item.Interaction, first.Interaction);
        Assert.NotSame(item.Interaction, separated.Interaction);
        var owner = new object();
        first.Interaction.SetPointerActive(owner, true);
        Assert.True(adjacent.Interaction.IsActive);
        Assert.True(item.Ranges[0].Interaction.IsActive);
        Assert.False(separated.Interaction.IsActive);
        Assert.False(item.Ranges[1].Interaction.IsActive);
        first.Interaction.SetPointerActive(owner, false);
        item.Ranges[1].Interaction.SetFocusActive(owner, true);
        Assert.True(separated.Interaction.IsActive);
        Assert.False(first.Interaction.IsActive);
        item.Ranges[1].Interaction.SetFocusActive(owner, false);
        item.Interaction.SetPointerActive(owner, true);
        Assert.True(item.Interaction.IsActive);
        Assert.False(first.Interaction.IsActive);
        Assert.False(separated.Interaction.IsActive);
        item.Interaction.SetPointerActive(owner, false);
    }

    /// <summary>Only same-space, known, non-overlapping adjacency can form one physical interaction run.</summary>
    [Fact]
    public void DifferentOrUnknownAddressGeometryDoesNotLinkRowsOrInteraction()
    {
        MemoryCoverageSegmentViewModel baseline = Segment(0, 4);
        MemoryCoverageSegmentViewModel differentSpace = Segment(4, 8, addressSpaceId: "input");
        MemoryCoverageSegmentViewModel unknownSpace = Segment(8, 12, addressSpaceId: null);
        MemoryCoverageSegmentViewModel unknownRange = UnknownRange("output");
        MemoryCoverageSegmentViewModel overlapping = Segment(2, 6);

        AssertIndependent(baseline, differentSpace);
        AssertIndependent(baseline, unknownSpace);
        AssertIndependent(baseline, unknownRange);
        AssertIndependent(baseline, overlapping);
    }

    /// <summary>Preservation details retain separate supporting rows while only contiguous typed geometry shares hover state.</summary>
    [Fact]
    public void PreservationDetailsKeepSupportingRowsSeparateAcrossContiguousAndDisconnectedRuns()
    {
        MemoryCoverageSegmentViewModel detailed = Segment(
            0,
            4,
            preservationDetails: [PreservationDetail(0, 4)]);
        MemoryCoverageSegmentViewModel contiguous = Segment(4, 8);
        MemoryCoverageSegmentViewModel disconnected = Segment(12, 16);

        MemoryCoverageLogicalItemViewModel item = CreateItem(detailed, contiguous, disconnected);

        Assert.Equal(3, item.Ranges.Count);
        Assert.Same(detailed, item.Ranges[0]);
        Assert.Same(contiguous, item.Ranges[1]);
        Assert.Same(disconnected, item.Ranges[2]);
        Assert.Same(detailed.Interaction, contiguous.Interaction);
        Assert.NotSame(detailed.Interaction, disconnected.Interaction);
        Assert.Same(detailed.Interaction, item.Ranges[0].Interaction);
        Assert.Same(contiguous.Interaction, item.Ranges[1].Interaction);
        Assert.Same(disconnected.Interaction, item.Ranges[2].Interaction);
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

    private static void AssertIndependent(
        MemoryCoverageSegmentViewModel first,
        MemoryCoverageSegmentViewModel second)
    {
        MemoryCoverageLogicalItemViewModel item = CreateItem(first, second);

        Assert.Equal(2, item.Ranges.Count);
        Assert.Same(first, item.Ranges[0]);
        Assert.Same(second, item.Ranges[1]);
        Assert.NotSame(first.Interaction, second.Interaction);
        Assert.Same(first.Interaction, item.Ranges[0].Interaction);
        Assert.Same(second.Interaction, item.Ranges[1].Interaction);
    }

    private static MemoryCoverageSegmentViewModel UnknownRange(string? addressSpaceId)
    {
        return new MemoryCoverageSegmentViewModel(
            "unknown range",
            "NF CtrlRAM",
            "detail",
            MemoryCoverageFillRole.CtrlRamNf,
            4,
            regionId: "nf-ctrlram",
            sourceSlotId: "ctrlram",
            logicalCoverageGroupId: "slot:ctrlram",
            contentRole: MemoryContentRole.CtrlRam,
            ctrlRamRegionRole: CtrlRamRegionRole.Nf,
            addressSpaceId: addressSpaceId);
    }

    private static MemoryLayoutPreservationDetail PreservationDetail(long start, long length)
    {
        return new MemoryLayoutPreservationDetail(
            "preserved-detail",
            blockIndex: 0,
            MemoryEndpointIdentity.NotApplicable,
            "reference-base",
            new ByteRange(0, length),
            new ByteRange(start, length));
    }

    private static MemoryCoverageSegmentViewModel Segment(
        long start,
        long endExclusive,
        MemoryWorkflowDisposition disposition = MemoryWorkflowDisposition.WillReplace,
        string sourceSlotId = "ctrlram",
        bool usesBase = false,
        MemoryContentRole contentRole = MemoryContentRole.CtrlRam,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common,
        CtrlRamRegionRole ctrlRamRegionRole = CtrlRamRegionRole.Nf,
        string? addressSpaceId = "output",
        IReadOnlyList<MemoryLayoutPreservationDetail>? preservationDetails = null)
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
            preservationDetails: preservationDetails,
            regionGroup: regionGroup,
            rangeStart: start,
            rangeEndExclusive: endExclusive,
            addressSpaceId: addressSpaceId,
            logicalCoverageGroupId: "slot:ctrlram",
            contentRole: contentRole,
            ctrlRamRegionRole: ctrlRamRegionRole);
    }
}
