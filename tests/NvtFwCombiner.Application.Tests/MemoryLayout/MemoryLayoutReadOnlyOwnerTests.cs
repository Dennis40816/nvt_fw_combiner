// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Checks effective read-only facts at the same canonical access owner used by the compiler.</summary>
public sealed class MemoryLayoutReadOnlyOwnerTests
{
    /// <summary>Missing profile permission prohibits every canonical byte rather than granting implicit writes.</summary>
    [Fact]
    public void MissingAccessRulesProduceDefaultDenialForEveryRegion()
    {
        FirmwareImageMap map = Map();
        var contract = new CompiledRegionAccessContract([], []);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        Assert.Equal([new ByteRange(0, 8), new ByteRange(8, 8)], ranges.Select(static range => range.Range.Range));
        Assert.All(ranges, static range => Assert.Equal(CompiledReadOnlyReasonKind.NoAccessRule, range.ReasonKind));
        Assert.All(ranges, static range => Assert.Equal("flash", range.Range.AddressSpaceId));
    }

    /// <summary>A whole-region grant covers its children atomically without treating those children as read-only.</summary>
    [Fact]
    public void WholeRegionGrantDoesNotMisclassifyItsChildrenAsReadOnly()
    {
        FirmwareImageMap map = Map();
        CompiledRegionAccessContract contract = RootContract(map, RegionAccessKind.Whole);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        Assert.Empty(ranges);
    }

    /// <summary>Parts grants only their declared child while retaining the exact denying parent rule.</summary>
    [Fact]
    public void PartsGrantLeavesOnlyUnlistedChildReadOnly()
    {
        FirmwareImageMap map = Map();
        CompiledRegionAccessContract contract = RootContract(map, RegionAccessKind.Parts, ["left"]);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        CompiledReadOnlyRange blocked = Assert.Single(ranges);
        Assert.Equal("right", blocked.Region.RegionId);
        Assert.Equal(new ByteRange(8, 8), blocked.Range.Range);
        Assert.Equal(CompiledReadOnlyReasonKind.ProfileAccess, blocked.ReasonKind);
        Assert.Same(contract.Requirements[0], blocked.AccessRule);
        Assert.Equal(RegionAccessKind.Parts, blocked.AccessRule!.Access);
    }

    /// <summary>A profile cannot relax the physical prohibition of a canonical ancestor.</summary>
    [Fact]
    public void ForbiddenPhysicalAncestorRemainsTheReadOnlyReason()
    {
        FirmwareImageMap map = Map(FirmwareWriteConstraint.Forbidden);
        CompiledRegionAccessContract contract = RootContract(map, RegionAccessKind.ExplicitRange);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        Assert.Equal([new ByteRange(0, 8), new ByteRange(8, 8)], ranges.Select(static range => range.Range.Range));
        Assert.All(ranges, static range =>
        {
            Assert.Equal(CompiledReadOnlyReasonKind.PhysicalWriteConstraint, range.ReasonKind);
            Assert.Equal("root", range.GoverningRegion.RegionId);
            Assert.Equal(FirmwareWriteConstraint.Forbidden, range.GoverningRegion.WriteConstraint);
        });
    }

    /// <summary>DeclaredSubregions authorizes exact children rather than making an entire parent read-only.</summary>
    [Fact]
    public void DeclaredPhysicalSubregionsRemainWritableThroughTheirExactRanges()
    {
        FirmwareImageMap map = Map(FirmwareWriteConstraint.DeclaredSubregions);
        CompiledRegionAccessContract contract = RootContract(map, RegionAccessKind.ExplicitRange);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        Assert.Empty(ranges);
    }

    /// <summary>Explicit read-only rules retain the profile explanation and canonical governing region.</summary>
    [Fact]
    public void ReadOnlyRuleRetainsItsReasonAndGoverningRegion()
    {
        FirmwareImageMap map = Map();
        CompiledRegionAccessContract contract = RootContract(map, RegionAccessKind.ReadOnly);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        Assert.Equal([new ByteRange(0, 8), new ByteRange(8, 8)], ranges.Select(static range => range.Range.Range));
        Assert.All(ranges, range =>
        {
            Assert.Equal(CompiledReadOnlyReasonKind.ProfileAccess, range.ReasonKind);
            Assert.Same(contract.Requirements[0], range.AccessRule);
            Assert.Equal("Synthetic active access rule", range.AccessRule!.Reason);
            Assert.Equal("root", range.GoverningRegion.RegionId);
        });
    }

    /// <summary>A partially aligned writable interior does not grant its unaligned edge bytes.</summary>
    [Fact]
    public void AlignmentRestrictionsKeepOnlyUnwritableFringesReadOnly()
    {
        FirmwareRegion[] regions =
        [
            new("root", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image,
                new ByteRange(0, 16), FirmwareWriteConstraint.ExplicitRange, 4),
            new("left", "root", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code,
                new ByteRange(0, 1), FirmwareWriteConstraint.ExplicitRange),
            new("middle", "root", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code,
                new ByteRange(1, 14), FirmwareWriteConstraint.ExplicitRange),
            new("right", "root", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code,
                new ByteRange(15, 1), FirmwareWriteConstraint.ExplicitRange),
        ];
        FirmwareImageMap map = Map(customRegions: regions);
        var contract = new CompiledRegionAccessContract(
            [new CompiledRegionAccessRequirement("middle", RegionAccessKind.ExplicitRange,
                "Synthetic aligned interior", [], [regions[0], regions[2]])], []);

        IReadOnlyList<CompiledReadOnlyRange> ranges = contract.GetReadOnlyRanges(map);

        Assert.Equal([new ByteRange(0, 1), new ByteRange(1, 3), new ByteRange(12, 3), new ByteRange(15, 1)],
            ranges.Select(static range => range.Range.Range));
        Assert.Equal(
            [CompiledReadOnlyReasonKind.NoAccessRule, CompiledReadOnlyReasonKind.PhysicalWriteConstraint,
             CompiledReadOnlyReasonKind.PhysicalWriteConstraint, CompiledReadOnlyReasonKind.NoAccessRule],
            ranges.Select(static range => range.ReasonKind));
    }

    /// <summary>The query cannot combine a compiled access rule with look-alike foreign canonical regions.</summary>
    [Fact]
    public void ReadOnlyProjectionRejectsASeparatelyConstructedMap()
    {
        FirmwareImageMap original = Map();
        CompiledRegionAccessContract contract = RootContract(original, RegionAccessKind.Whole);
        FirmwareImageMap other = Map();

        _ = Assert.Throws<ArgumentException>(() => contract.GetReadOnlyRanges(other));
    }

    /// <summary>The finite candidate projection agrees with every legal target on small aligned maps.</summary>
    [Theory]
    [InlineData(2, 3)]
    [InlineData(4, 3)]
    [InlineData(8, 3)]
    [InlineData(1, 1)]
    public void EffectiveRangesAgreeWithExhaustiveTargetAuthorization(int rootAlignment, int middleAlignment)
    {
        FirmwareRegion[] regions =
        [
            new("root", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image,
                new ByteRange(0, 16), FirmwareWriteConstraint.ExplicitRange, rootAlignment),
            new("left", "root", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code,
                new ByteRange(0, 3), FirmwareWriteConstraint.Forbidden),
            new("middle", "root", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code,
                new ByteRange(3, 12), FirmwareWriteConstraint.ExplicitRange, middleAlignment),
            new("right", "root", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code,
                new ByteRange(15, 1), FirmwareWriteConstraint.Forbidden),
        ];
        FirmwareImageMap map = Map(customRegions: regions);
        var contract = new CompiledRegionAccessContract(
            [new CompiledRegionAccessRequirement("middle", RegionAccessKind.ExplicitRange,
                "Synthetic independently aligned ancestors", [], [regions[0], regions[2]])], []);
        Dictionary<string, FirmwareRegion> byId = regions.ToDictionary(static region => region.RegionId, StringComparer.Ordinal);
        HashSet<long> writable = [];
        for (long start = 0; start < 16; start++)
        {
            for (long end = start + 1; end <= 16; end++)
            {
                if (contract.GetTargetWriteRestriction(ByteRange.FromStartEndExclusive(start, end), "flash", byId) is null)
                {
                    for (long address = start; address < end; address++)
                    {
                        _ = writable.Add(address);
                    }
                }
            }
        }

        IReadOnlyList<CompiledReadOnlyRange> projected = contract.GetReadOnlyRanges(map);

        for (long address = 0; address < 16; address++)
        {
            Assert.Equal(!writable.Contains(address), projected.Any(range =>
                range.Range.Range.Contains(new ByteRange(address, 1))));
        }
    }

    private static CompiledRegionAccessContract RootContract(FirmwareImageMap map, RegionAccessKind access,
        IReadOnlyList<string>? allowedChildren = null)
    {
        FirmwareRegion root = map.Regions.First(static region => region.RegionId == "root");
        return new CompiledRegionAccessContract(
            [new CompiledRegionAccessRequirement("root", access, "Synthetic active access rule", allowedChildren ?? [], [root])], []);
    }

    private static FirmwareImageMap Map(FirmwareWriteConstraint rootConstraint = FirmwareWriteConstraint.ExplicitRange,
        IReadOnlyList<FirmwareRegion>? customRegions = null)
    {
        FirmwareRegion[] regions = customRegions is null
            ?
            [
                new("root", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image, new ByteRange(0, 16), rootConstraint),
                new("left", "root", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, new ByteRange(0, 8), FirmwareWriteConstraint.WholeRegion),
                new("right", "root", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(8, 8), FirmwareWriteConstraint.WholeRegion),
            ]
            : [.. customRegions];
        return FirmwareImageMapTestFactory.CreateDirect("access-map", "flash",
            new FirmwareMapApplicability(["NT-SYNTHETIC"], [ExperienceIds.StandardMerge],
                TopologyRequirement.NoTopologyConstraint(), 16),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet("physical", "flash", regions, ["synthetic-access-evidence"])], [], ["synthetic-access-evidence"]);
    }
}
