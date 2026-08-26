using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Application alone assigns retained ranges to one unambiguous replacement source.</summary>
    [Fact]
    public void ReplacePublishesFinalLogicalCoverageIdentityForRetainedCompanions()
    {
        ProjectionFixture singleSource = CreateFixture(
            CompositionKind.Replace,
            customRegions: FlatPhysicalRegions());
        ActiveSessionSnapshot singleSession = CreateSession(
            singleSource,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("dp-replacement", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot singleSnapshot = MemoryLayoutProjector.Project(
            singleSource.Capability,
            singleSession,
            singleSource.Composition);

        MemoryLayoutSegment[] dpSegments =
        [
            .. singleSnapshot.AfterSegments.Where(static segment => segment.RegionId == "dp-code"),
        ];
        Assert.Equal(
            ["slot:dp-replacement", "slot:dp-replacement"],
            dpSegments.Select(static segment => segment.LogicalCoverageGroupId));
        Assert.Equal(
            ["dp-replacement", "reference-base"],
            dpSegments.Select(static segment => segment.SourceSlotId));
        Assert.Equal([new ByteRange(0, 4), new ByteRange(4, 4)], dpSegments.Select(static segment => segment.Range));
        Assert.All(
            singleSnapshot.AfterSegments.Where(static segment =>
                segment.RegionId is "reserved-gap" or "tp-code"),
            static segment => Assert.Equal("slot:reference-base", segment.LogicalCoverageGroupId));
        Assert.All(singleSnapshot.AfterSegments, AssertNamespacedLogicalCoverageId);
    }

    /// <summary>Zero or multiple admitted replacement sources retain reference identity.</summary>
    [Fact]
    public void ReplaceRetainedRangesFailClosedUnlessExactlyOneSourceSlotQualifies()
    {
        ProjectionFixture multipleSources = CreateFixture(
            CompositionKind.Replace,
            MultiSourcePlan(),
            MultiSourceContract(),
            customRegions: FlatPhysicalRegions());
        ActiveSessionSnapshot multiSession = CreateSession(
            multipleSources,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("source-a", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("source-b", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot multiSnapshot = MemoryLayoutProjector.Project(
            multipleSources.Capability,
            multiSession,
            multipleSources.Composition);

        Assert.Equal(
            ["slot:source-a", "slot:source-b", "slot:reference-base"],
            multiSnapshot.AfterSegments
                .Where(static segment => segment.RegionId == "dp-code")
                .Select(static segment => segment.LogicalCoverageGroupId));
        Assert.All(
            multiSnapshot.AfterSegments.Where(static segment => segment.RegionId == "reserved-gap"),
            static segment => Assert.Equal("slot:reference-base", segment.LogicalCoverageGroupId));
    }

    /// <summary>Several writes from one slot remain one candidate, including a cross-region write.</summary>
    [Fact]
    public void ReplaceUsesDistinctSourceSlotsAndAnyPhysicalOverlap()
    {
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Replace,
            RepeatedSourceCrossBoundaryPlan(),
            SingleAuxiliarySourceContract(),
            customRegions: FlatPhysicalRegions());
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("source-a", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.All(
            snapshot.AfterSegments.Where(static segment =>
                segment.RegionId is "dp-code" or "reserved-gap"),
            static segment => Assert.Equal("slot:source-a", segment.LogicalCoverageGroupId));
        Assert.All(
            snapshot.AfterSegments.Where(static segment => segment.RegionId == "tp-code"),
            static segment => Assert.Equal("slot:reference-base", segment.LogicalCoverageGroupId));
        Assert.Equal(
            [
                new ByteRange(0, 2),
                new ByteRange(2, 2),
                new ByteRange(4, 2),
                new ByteRange(6, 2),
                new ByteRange(8, 2),
                new ByteRange(10, 2),
                new ByteRange(12, 4),
            ],
            snapshot.AfterSegments.Select(static segment => segment.Range));
    }

    /// <summary>A write binds only the exact projected slice it overlaps when one parent is split.</summary>
    [Fact]
    public void ReplaceRetainedCompanionDoesNotLeakAcrossSplitCanonicalRegion()
    {
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Replace,
            SingleSlicePlan(),
            customRegions: SplitPhysicalRegions());
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("dp-replacement", AuthoringSlotLifecycle.Verified, Capacity));
        var nestedDisplay = new CtrlRamRegion(
            "nested-display",
            "Nested CtrlRAM",
            2,
            2,
            IsMultiChipOnly: false,
            ReplaceRegionGroup.Master,
            CtrlRamRegionRole.Nf);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition,
            [nestedDisplay]);

        MemoryLayoutSegment[] parentSlices =
        [
            .. snapshot.AfterSegments.Where(static segment => segment.RegionId == "dp-code"),
        ];
        Assert.Equal([new ByteRange(0, 1), new ByteRange(1, 1), new ByteRange(4, 4)],
            parentSlices.Select(static segment => segment.Range));
        Assert.Equal(
            ["slot:dp-replacement", "slot:dp-replacement", "slot:reference-base"],
            parentSlices.Select(static segment => segment.LogicalCoverageGroupId));
        Assert.All(
            snapshot.AfterSegments.Where(static segment => segment.RegionId == "nested-ctrlram"),
            static segment => Assert.Equal("slot:reference-base", segment.LogicalCoverageGroupId));
    }

    /// <summary>Source-less physical fragments resolve the unique smallest containing canonical region.</summary>
    [Fact]
    public void PhysicalFallbackUsesSmallestContainingCanonicalRegion()
    {
        CompositionPlan plan = new(
            ImageInitialization.Blank("output-image", Capacity, 0),
            [
                new AddressSpace("unused-source", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.FillRange(
                    "split-dp",
                    10,
                    "output-image",
                    new ByteRange(4, 4),
                    0x5A,
                    OverlapPolicy.Reject,
                    "split the nested DP children"),
            ]);
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Merge,
            plan,
            new CompiledInputContract(
                [
                    SlotRequirement(
                        "unused-source",
                        "source",
                        CompiledInputArtifactClass.Auxiliary,
                        new CompiledExactBytesInputLengthRequirement(Capacity)),
                ],
                [
                    new CompiledInputSpaceBinding(
                        "unused-source",
                        "unused-source",
                        CompiledInputInstancePolicy.Singleton),
                ]));
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("unused-source", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.Equal(
            ["region:dp-code-before-anchor", "region:dp-code-anchor"],
            snapshot.AfterSegments
                .Where(static segment => segment.RegionId == "dp-code")
                .Select(static segment => segment.LogicalCoverageGroupId));
        Assert.Equal(
            [new ByteRange(0, 4), new ByteRange(4, 4)],
            snapshot.AfterSegments
                .Where(static segment => segment.RegionId == "dp-code")
                .Select(static segment => segment.Range));
    }

    private static void AssertNamespacedLogicalCoverageId(MemoryLayoutSegment segment)
    {
        Assert.Matches("^(slot|region|segment):.+", segment.LogicalCoverageGroupId);
    }

    private static CompositionPlan MultiSourcePlan()
    {
        return new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", Capacity),
            [
                new AddressSpace("reference-base", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("source-a", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("source-b", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-a",
                    100,
                    "source-a",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "replace first range"),
                CompositionOperation.ReplaceRange(
                    "replace-b",
                    200,
                    "source-b",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(2, 2),
                    OverlapPolicy.Reject,
                    "replace second range"),
            ]);
    }

    private static CompositionPlan SingleSlicePlan()
    {
        return new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", Capacity),
            [
                new AddressSpace("reference-base", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("dp-replacement", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-first-parent-slice",
                    100,
                    "dp-replacement",
                    new ByteRange(0, 1),
                    "output-image",
                    new ByteRange(0, 1),
                    OverlapPolicy.Reject,
                    "replace only the first projected parent slice"),
            ]);
    }

    private static CompositionPlan RepeatedSourceCrossBoundaryPlan()
    {
        return new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", Capacity),
            [
                new AddressSpace("reference-base", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("source-a", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-first",
                    100,
                    "source-a",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "first write from the source"),
                CompositionOperation.ReplaceRange(
                    "replace-second",
                    200,
                    "source-a",
                    new ByteRange(2, 2),
                    "output-image",
                    new ByteRange(4, 2),
                    OverlapPolicy.Reject,
                    "second write from the same source"),
                CompositionOperation.ReplaceRange(
                    "replace-cross-boundary",
                    300,
                    "source-a",
                    new ByteRange(4, 4),
                    "output-image",
                    new ByteRange(6, 4),
                    OverlapPolicy.Reject,
                    "write across the DP and reserved boundary"),
            ]);
    }

    private static CompiledInputContract MultiSourceContract()
    {
        return new CompiledInputContract(
            [
                SlotRequirement(
                    "reference-base",
                    "reference",
                    CompiledInputArtifactClass.ReferenceImage,
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity)),
                SlotRequirement(
                    "source-a",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    new CompiledExactBytesInputLengthRequirement(Capacity)),
                SlotRequirement(
                    "source-b",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    new CompiledExactBytesInputLengthRequirement(Capacity)),
            ],
            [
                new CompiledInputSpaceBinding(
                    "reference-base",
                    "reference-base",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "source-a",
                    "source-a",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "source-b",
                    "source-b",
                    CompiledInputInstancePolicy.Singleton),
            ]);
    }

    private static CompiledInputContract SingleAuxiliarySourceContract()
    {
        return new CompiledInputContract(
            [
                SlotRequirement(
                    "reference-base",
                    "reference",
                    CompiledInputArtifactClass.ReferenceImage,
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity)),
                SlotRequirement(
                    "source-a",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    new CompiledExactBytesInputLengthRequirement(Capacity)),
            ],
            [
                new CompiledInputSpaceBinding(
                    "reference-base",
                    "reference-base",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "source-a",
                    "source-a",
                    CompiledInputInstancePolicy.Singleton),
            ]);
    }

    private static IReadOnlyList<FirmwareRegion> FlatPhysicalRegions()
    {
        return
        [
            new(
                "flash-image",
                parentRegionId: null,
                FirmwareRegionOwner.System,
                FirmwareRegionKind.Image,
                new ByteRange(0, Capacity),
                FirmwareWriteConstraint.Forbidden),
            new(
                "dp-code",
                "flash-image",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Code,
                new ByteRange(0, 8),
                FirmwareWriteConstraint.ExplicitRange),
            new(
                "reserved-gap",
                "flash-image",
                FirmwareRegionOwner.Reserved,
                FirmwareRegionKind.Reserved,
                new ByteRange(8, 4),
                FirmwareWriteConstraint.Forbidden),
            new(
                "tp-code",
                "flash-image",
                FirmwareRegionOwner.Tp,
                FirmwareRegionKind.Code,
                new ByteRange(12, 4),
                FirmwareWriteConstraint.WholeRegion),
        ];
    }

    private static IReadOnlyList<FirmwareRegion> SplitPhysicalRegions()
    {
        return
        [
            new(
                "flash-image",
                parentRegionId: null,
                FirmwareRegionOwner.System,
                FirmwareRegionKind.Image,
                new ByteRange(0, Capacity),
                FirmwareWriteConstraint.Forbidden),
            new(
                "dp-code",
                "flash-image",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Code,
                new ByteRange(0, 8),
                FirmwareWriteConstraint.ExplicitRange),
            new(
                "nested-ctrlram",
                "dp-code",
                FirmwareRegionOwner.Tp,
                FirmwareRegionKind.CtrlRam,
                new ByteRange(2, 2),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "dp-before-ctrlram",
                "dp-code",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Code,
                new ByteRange(0, 2),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "dp-after-ctrlram",
                "dp-code",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Code,
                new ByteRange(4, 4),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "reserved-gap",
                "flash-image",
                FirmwareRegionOwner.Reserved,
                FirmwareRegionKind.Reserved,
                new ByteRange(8, 4),
                FirmwareWriteConstraint.Forbidden),
            new(
                "tp-code",
                "flash-image",
                FirmwareRegionOwner.Tp,
                FirmwareRegionKind.Code,
                new ByteRange(12, 4),
                FirmwareWriteConstraint.WholeRegion),
        ];
    }
}
