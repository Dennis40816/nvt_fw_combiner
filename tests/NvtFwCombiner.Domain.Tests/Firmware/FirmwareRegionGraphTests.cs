// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Characterizes the canonical graph rules and complete typed diagnostics.</summary>
public sealed class FirmwareRegionGraphTests
{
    /// <summary>Capacity failures precede partition failures without hiding either.</summary>
    [Fact]
    public void RegionBeyondCapacityReportsBothCapacityAndCoverageViolations()
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("root", 0, 17)], 16);

        Assert.Equal<FirmwareRegionGraphViolation>(
            [new(FirmwareRegionGraphViolationKind.ExceedsCapacity, "root", null),
             new(FirmwareRegionGraphViolationKind.PartitionIncomplete, "root", null)], violations);
    }

    /// <summary>Missing parents are reported for every child without indexing a missing range.</summary>
    [Fact]
    public void EveryUnknownParentIsReported()
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("root", 0, 16), Node("first", 0, 4, "missing-a"), Node("second", 4, 4, "missing-b")], 16);

        Assert.Equal<FirmwareRegionGraphViolation>(
            [new(FirmwareRegionGraphViolationKind.UnknownParent, "first", "missing-a"),
             new(FirmwareRegionGraphViolationKind.UnknownParent, "second", "missing-b")], violations);
    }

    /// <summary>All cycle edges and independently invalid containment and coverage are retained.</summary>
    [Fact]
    public void ParentCycleReportsEveryEdgeAndOtherViolations()
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("a", 0, 16, "b"), Node("b", 0, 16, "a")], 16);

        Assert.Equal<FirmwareRegionGraphViolation>(
            [new(FirmwareRegionGraphViolationKind.ParentCycle, "a", "b"),
             new(FirmwareRegionGraphViolationKind.ParentCycle, "b", "a"),
             new(FirmwareRegionGraphViolationKind.ParentDoesNotProperlyContainChild, "a", "b"),
             new(FirmwareRegionGraphViolationKind.ParentDoesNotProperlyContainChild, "b", "a"),
             new(FirmwareRegionGraphViolationKind.PartitionIncomplete, null, null)], violations);
    }

    /// <summary>A child occupying the parent's entire range violates proper containment.</summary>
    [Fact]
    public void EqualParentAndChildRangesAreRejected()
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("root", 0, 16), Node("child", 0, 16, "root")], 16);

        Assert.Equal(new(FirmwareRegionGraphViolationKind.ParentDoesNotProperlyContainChild, "child", "root"),
            Assert.Single(violations));
    }

    /// <summary>Both overlaps and internal gaps violate the same canonical partition rule.</summary>
    [Theory]
    [InlineData(4, 12)]
    [InlineData(12, 4)]
    public void NonAdjacentRootsAreRejected(long secondStart, long secondLength)
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("first", 0, 8), Node("second", secondStart, secondLength)], 16);

        Assert.Equal(new(FirmwareRegionGraphViolationKind.PartitionDiscontinuity, "second", null),
            Assert.Single(violations));
    }

    /// <summary>A root partition must reach the map's exact end.</summary>
    [Fact]
    public void IncompleteRootCoverageIsRejected()
    {
        Assert.Equal(new(FirmwareRegionGraphViolationKind.PartitionIncomplete, "root", null),
            Assert.Single(FirmwareRegionGraph.Validate([Node("root", 0, 8)], 16)));
    }

    /// <summary>An empty graph still reports its missing root partition.</summary>
    [Fact]
    public void EmptyRootPartitionIsRejected()
    {
        Assert.Equal(new(FirmwareRegionGraphViolationKind.PartitionIncomplete, null, null),
            Assert.Single(FirmwareRegionGraph.Validate([], 16)));
    }

    /// <summary>Nested partitions use the same adjacency rule and identify the parent.</summary>
    [Fact]
    public void NestedPartitionGapNamesTheChildAndParent()
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("root", 0, 16), Node("first", 0, 4, "root"), Node("second", 8, 8, "root")], 16);

        Assert.Equal(new(FirmwareRegionGraphViolationKind.PartitionDiscontinuity, "second", "root"),
            Assert.Single(violations));
    }

    /// <summary>Nested partitions must cover the parent's exact range.</summary>
    [Fact]
    public void IncompleteChildCoverageNamesTheChildAndParent()
    {
        Assert.Equal(new(FirmwareRegionGraphViolationKind.PartitionIncomplete, "child", "root"),
            Assert.Single(FirmwareRegionGraph.Validate(
                [Node("root", 0, 16), Node("child", 0, 8, "root")], 16)));
    }

    /// <summary>A tail leading into a cycle is not itself a cycle edge.</summary>
    [Fact]
    public void OnlyCycleMembersReceiveCycleViolations()
    {
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [Node("tail", 0, 4, "a"), Node("a", 0, 8, "b"), Node("b", 0, 16, "a")], 16);

        Assert.Equal<FirmwareRegionGraphViolation>(
            [new(FirmwareRegionGraphViolationKind.ParentCycle, "b", "a"),
             new(FirmwareRegionGraphViolationKind.ParentCycle, "a", "b")],
            violations.Where(static violation => violation.Kind == FirmwareRegionGraphViolationKind.ParentCycle));
    }

    /// <summary>Input order does not affect canonical partition validation.</summary>
    [Fact]
    public void CompleteNestedPartitionsAcceptUnsortedInput()
    {
        Assert.Empty(FirmwareRegionGraph.Validate(
            [Node("right", 8, 8, "root"), Node("root", 0, 16), Node("left", 0, 8, "root")], 16));
    }

    private static FirmwareRegionGraphNode Node(string id, long start, long length, string? parent = null)
    {
        return new FirmwareRegionGraphNode(id, parent, new ByteRange(start, length));
    }
}
