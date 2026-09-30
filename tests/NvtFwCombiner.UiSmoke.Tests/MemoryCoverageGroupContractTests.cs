using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Typed display-group consumption and invariant diagnostics.</summary>
public sealed class MemoryCoverageGroupContractTests
{
    /// <summary>Pending/synthetic coverage with no published group has neutral Common presentation.</summary>
    [Fact]
    public void MissingTypedGroupRemainsCommon()
    {
        MemoryCoverageGroupViewModel group = Assert.Single(ReplaceRegionGroupBuilder.CreateCoverageGroups(
            [Segment(null, ReplaceRegionGroup.Master), Segment(null, ReplaceRegionGroup.SlaveRight)], ShellTextResources.For(ShellLanguage.English)));
        Assert.Equal(ReplaceRegionGroup.Common, group.RegionGroup);
    }

    /// <summary>Conflicting typed decisions fail with their logical identity, without reclassification.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(ReplaceRegionGroup.Master)]
    public void InconsistentTypedGroupsReportLogicalIdentity(ReplaceRegionGroup? other)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ReplaceRegionGroupBuilder.CreateCoverageGroups(
            [Segment(ReplaceRegionGroup.Common), Segment(other)], ShellTextResources.For(ShellLanguage.English)).ToArray());
        Assert.Contains("slot:source", error.Message, StringComparison.Ordinal);
        Assert.Contains("inconsistent Application display groups", error.Message, StringComparison.Ordinal);
    }

    private static MemoryCoverageSegmentViewModel Segment(ReplaceRegionGroup? displayGroup, ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common)
    {
        return new("range", "source", "detail", MemoryCoverageFillRole.Dp, 10,
            logicalCoverageGroupId: "slot:source", regionGroup: regionGroup, displayGroup: displayGroup);
    }
}
