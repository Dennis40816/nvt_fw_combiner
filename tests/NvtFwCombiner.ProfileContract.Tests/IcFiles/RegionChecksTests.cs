// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

/// <summary>Verifies the loader projects canonical Domain graph violations with source marks.</summary>
public sealed class RegionChecksTests
{
    private static readonly SourceMark Mark = new(1, 1);
    private static readonly SourceMark ChildMark = new(7, 3);

    /// <summary>Complete nested partitions and touching roots satisfy the Domain graph.</summary>
    [Fact]
    public void CompleteNestedAndTouchingRegionsFitASyntheticLayout()
    {
        Assert.Empty(Check(
            [Region("parent", 0, 0x80), Region("left", 0, 0x40, "parent"),
             Region("right", 0x40, 0x40, "parent"), Region("tail", 0x80, 0x80)]));
    }

    /// <summary>Capacity and exact-coverage errors retain the offending source location.</summary>
    [Fact]
    public void RegionBeyondCapacityReportsEveryDomainViolation()
    {
        Assert.Equal<IcFileError>(
            [new(IcFileErrorCode.RegionOutsideLayout, ChildMark, "outside", "synthetic-layout"),
             new(IcFileErrorCode.RegionPartitionIncomplete, ChildMark, "outside", "synthetic-layout")],
            Check([Region("outside", 0, 0x110) with { Mark = ChildMark }]));
    }

    /// <summary>A missing parent is projected from Domain's typed reference violation.</summary>
    [Fact]
    public void MissingParentRetainsTheChildSourceMark()
    {
        Assert.Equal(new(IcFileErrorCode.DanglingReference, ChildMark, "child", "missing"),
            Assert.Single(Check([Region("root", 0, 0x100),
                Region("child", 0, 0x40, "missing") with { Mark = ChildMark }])));
    }

    /// <summary>The graph checks proper containment even when parent and child have equal ranges.</summary>
    [Fact]
    public void EqualRangeParentAndChildAreRejected()
    {
        Assert.Equal(new(IcFileErrorCode.RegionOutsideParent, ChildMark, "child", "parent"),
            Assert.Single(Check([Region("parent", 0, 0x100),
                Region("child", 0, 0x100, "parent") with { Mark = ChildMark }])));
    }

    /// <summary>Overlapping roots have no loader-specific allowance.</summary>
    [Fact]
    public void RootOverlapUsesTheCanonicalPartitionRule()
    {
        Assert.Equal(new(IcFileErrorCode.RegionPartitionDiscontinuity, ChildMark, "second", "synthetic-layout"),
            Assert.Single(Check([Region("first", 0, 0x80),
                Region("second", 0x40, 0xC0) with { Mark = ChildMark }])));
    }

    /// <summary>Internal child gaps identify the offending child and its parent.</summary>
    [Fact]
    public void ChildGapUsesTheCanonicalPartitionRule()
    {
        Assert.Equal(new(IcFileErrorCode.RegionPartitionDiscontinuity, ChildMark, "right", "parent"),
            Assert.Single(Check([Region("parent", 0, 0x100), Region("left", 0, 0x40, "parent"),
                Region("right", 0x80, 0x80, "parent") with { Mark = ChildMark }])));
    }

    /// <summary>The last child cannot leave an undeclared tail in its parent.</summary>
    [Fact]
    public void IncompleteChildCoverageUsesTheLastChildSourceMark()
    {
        Assert.Equal(new(IcFileErrorCode.RegionPartitionIncomplete, ChildMark, "child", "parent"),
            Assert.Single(Check([Region("parent", 0, 0x100),
                Region("child", 0, 0x80, "parent") with { Mark = ChildMark }])));
    }

    /// <summary>Self-parent input receives typed graph diagnostics before physical-region construction.</summary>
    [Fact]
    public void SelfParentReportsEveryDomainViolation()
    {
        Assert.Equal<IcFileError>(
            [new(IcFileErrorCode.RegionParentCycle, ChildMark, "self", "self"),
             new(IcFileErrorCode.RegionOutsideParent, ChildMark, "self", "self"),
             new(IcFileErrorCode.RegionPartitionIncomplete, Mark, "synthetic-layout", "synthetic-layout")],
            Check([Region("self", 0, 0x100, "self") with { Mark = ChildMark }]));
    }

    /// <summary>Each cycle edge and equal-range containment violation retains its source mark.</summary>
    [Fact]
    public void TwoRegionCycleReportsBothInvolvedRegions()
    {
        Assert.Equal<IcFileError>(
            [new(IcFileErrorCode.RegionParentCycle, Mark, "a", "b"),
             new(IcFileErrorCode.RegionParentCycle, ChildMark, "b", "a"),
             new(IcFileErrorCode.RegionOutsideParent, Mark, "a", "b"),
             new(IcFileErrorCode.RegionOutsideParent, ChildMark, "b", "a"),
             new(IcFileErrorCode.RegionPartitionIncomplete, Mark, "synthetic-layout", "synthetic-layout")],
            Check([Region("a", 0, 0x100, "b"), Region("b", 0, 0x100, "a") with { Mark = ChildMark }]));
    }

    /// <summary>No alignment restriction is added by graph validation.</summary>
    [Fact]
    public void OddLengthPartitionsDoNotAcquireLoaderAlignmentRules()
    {
        Assert.Empty(Check([Region("first", 0, 1), Region("second", 1, 0xFF)]));
    }

    private static ResolvedRegion Region(string id, long start, long length, string? parent = null)
    {
        return new ResolvedRegion(id, new ByteRange(start, length), parent, Mark);
    }

    private static ImmutableArray<IcFileError> Check(IReadOnlyList<ResolvedRegion> regions)
    {
        return RegionChecks.Check("synthetic-layout", 0x100, regions);
    }
}
