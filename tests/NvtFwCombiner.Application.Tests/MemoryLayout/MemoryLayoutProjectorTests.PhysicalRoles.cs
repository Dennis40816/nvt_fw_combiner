using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Canonical Vector CtrlRAM remains a distinct detailed display role.</summary>
    [Fact]
    public void CtrlRamDiscoveryPreservesVectorFamilyRole()
    {
        var vector = new TpFlashMapRegion(
            "vector",
            "Vector CtrlRAM",
            TpFlashMapRegionKind.CtrlRam,
            new ByteRange(0, 4));

        CtrlRamInspectionDisplay display = MemoryLayoutProjector.ProjectCtrlRamDiscovery(
            "single",
            commandPlan: null,
            [vector],
            sources: [],
            hasReadableBase: true);

        Assert.Equal(CtrlRamRegionRole.Vector, Assert.Single(display.Regions).Role);
    }

    /// <summary>Keeps protected customer information and unknown map gaps as distinct typed facts.</summary>
    [Fact]
    public void ProjectionDistinguishesCustomerInformationFromUnmappedStructure()
    {
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Merge,
            customRegions: DistinctPhysicalRoleRegions());
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.Equal(
            [
                MemoryContentRole.Dp,
                MemoryContentRole.Unmapped,
                MemoryContentRole.CustomerInformation,
                MemoryContentRole.Tp,
            ],
            snapshot.AfterSegments.Select(static segment => segment.ContentRole));
        MemoryLayoutSegment customerInformation = Assert.Single(
            snapshot.AfterSegments,
            static segment => segment.CanonicalRegion?.Owner == FirmwareRegionOwner.Customer);
        Assert.Equal(FirmwareRegionKind.Data, customerInformation.CanonicalRegion!.Kind);
        Assert.Equal(MemoryWorkflowDisposition.Resolved, customerInformation.Disposition);
        Assert.Empty(customerInformation.ContributingOperations);
    }

    private static IReadOnlyList<FirmwareRegion> DistinctPhysicalRoleRegions()
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
                new ByteRange(0, 4),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "unknown-gap",
                "flash-image",
                FirmwareRegionOwner.Unknown,
                FirmwareRegionKind.Unmapped,
                new ByteRange(4, 4),
                FirmwareWriteConstraint.Forbidden),
            new(
                "customer-data",
                "flash-image",
                FirmwareRegionOwner.Customer,
                FirmwareRegionKind.Data,
                new ByteRange(8, 4),
                FirmwareWriteConstraint.ExplicitRange),
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
