using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
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

    /// <summary>Shared selectors count distinct physical regions rather than compiled blocks.</summary>
    [Fact]
    public void CtrlRamDiscoveryPublishesDistinctTargetRegionCount()
    {
        TpFlashMapRegion master = Region("nf-master", "NF CtrlRAM (Master)", 0x100);
        TpFlashMapRegion slave = Region("nf-slave", "NF CtrlRAM (Slave)", 0x200);
        var source = new TpCtrlRamPostbuildSource(
            "nf",
            "NF_Ctrlram.bin",
            "nf",
            0x30,
            [
                Block("master-head", 0, new ByteRange(0x100, 0x10)),
                Block("master-tail", 0x10, new ByteRange(0x110, 0x10)),
                Block("slave", 0x20, new ByteRange(0x200, 0x10)),
            ],
            [master, slave],
            TpCtrlRamPostbuildArtifactRole.CtrlRam);

        ReplaceInputSlot slot = Assert.Single(MemoryLayoutProjector.ProjectCtrlRamDiscovery(
            "single", null, [master, slave], [source], hasReadableBase: false).InputSlots);
        CtrlRamInputDescriptionFacts facts = Assert.IsType<CtrlRamInputDescriptionFacts>(slot.CtrlRamDescription);

        Assert.Equal(2, facts.TargetRegionCount);
        Assert.True(facts.IsShared);
        Assert.Equal(3, facts.Sections.Count);
        Assert.Equal("NF CtrlRAM (Shared)", slot.Title);
        Assert.Equal(ReplaceRegionGroup.Common, slot.RegionGroup);
    }

    /// <summary>Multiple compiled blocks in one physical region remain one non-shared target.</summary>
    [Fact]
    public void CtrlRamDiscoveryDoesNotCountBlocksAsPhysicalTargets()
    {
        TpFlashMapRegion master = Region("nf-master", "NF CtrlRAM (Master)", 0x100);
        var source = new TpCtrlRamPostbuildSource(
            "nf",
            "NF_Ctrlram.bin",
            "nf",
            0x20,
            [
                Block("master-head", 0, new ByteRange(0x100, 0x10)),
                Block("master-tail", 0x10, new ByteRange(0x110, 0x10)),
            ],
            [master],
            TpCtrlRamPostbuildArtifactRole.CtrlRam);

        ReplaceInputSlot slot = Assert.Single(MemoryLayoutProjector.ProjectCtrlRamDiscovery(
            "2", null, [master], [source], hasReadableBase: false).InputSlots);
        CtrlRamInputDescriptionFacts facts = Assert.IsType<CtrlRamInputDescriptionFacts>(slot.CtrlRamDescription);

        Assert.Equal(1, facts.TargetRegionCount);
        Assert.False(facts.IsShared);
        Assert.Equal(2, facts.Sections.Count);
        Assert.Equal("NF CtrlRAM (Master)", slot.Title);
        Assert.Equal(ReplaceRegionGroup.Master, slot.RegionGroup);
    }

    /// <summary>DiffDLM keeps its typed artifact role even when it targets multiple physical regions.</summary>
    [Fact]
    public void CtrlRamDiscoverySupportsMultiRegionDiffDlmWithoutInvariantConflict()
    {
        TpFlashMapRegion first = Region("diff-master", "DIFF DLM (Master)", 0x300);
        TpFlashMapRegion second = Region("diff-slave", "DIFF DLM (Slave)", 0x400);
        var source = new TpCtrlRamPostbuildSource(
            "diff-dlm",
            "DiffDLM.bin",
            "diff-dlm",
            0x20,
            [
                new LegacyCombinerBlockArgument(
                    "diff-master", LegacyCombinerBlockSourceKind.StagedArtifact, "DiffDLM.bin", 0,
                    new ByteRange(0x300, 0x10), stagedArtifactId: "diff-dlm"),
                new LegacyCombinerBlockArgument(
                    "diff-slave", LegacyCombinerBlockSourceKind.StagedArtifact, "DiffDLM.bin", 0x10,
                    new ByteRange(0x400, 0x10), stagedArtifactId: "diff-dlm"),
            ],
            [first, second],
            TpCtrlRamPostbuildArtifactRole.DiffDlm);

        ReplaceInputSlot slot = Assert.Single(MemoryLayoutProjector.ProjectCtrlRamDiscovery(
            "cascade", null, [first, second], [source], hasReadableBase: false).InputSlots);
        CtrlRamInputDescriptionFacts facts = Assert.IsType<CtrlRamInputDescriptionFacts>(slot.CtrlRamDescription);

        Assert.Equal(2, facts.TargetRegionCount);
        Assert.True(facts.IsShared);
        Assert.Equal("DiffDLM", slot.Title);
        Assert.Equal(ReplaceRegionGroup.Cascade, slot.RegionGroup);
    }

    /// <summary>Empty and duplicate target-region authority fails closed before projection publication.</summary>
    [Fact]
    public void CtrlRamDiscoveryRejectsInvalidTargetRegionAuthority()
    {
        TpFlashMapRegion master = Region("nf-master", "NF CtrlRAM (Master)", 0x100);
        TpFlashMapRegion duplicate = Region("nf-master", "Conflicting duplicate", 0x300);
        var noTargets = new TpCtrlRamPostbuildSource(
            "empty", "NF_Ctrlram.bin", "nf", 4,
            [Block("orphan", 0, new ByteRange(0x100, 4))], [],
            TpCtrlRamPostbuildArtifactRole.CtrlRam);
        var duplicateTargets = new TpCtrlRamPostbuildSource(
            "duplicate", "NF_Ctrlram.bin", "nf", 4,
            [Block("master", 0, new ByteRange(0x100, 4))], [master, duplicate],
            TpCtrlRamPostbuildArtifactRole.CtrlRam);

        InvalidDataException empty = Assert.Throws<InvalidDataException>(() =>
            MemoryLayoutProjector.ProjectCtrlRamDiscovery(
                "single", null, [], [noTargets], hasReadableBase: false));
        InvalidDataException duplicates = Assert.Throws<InvalidDataException>(() =>
            MemoryLayoutProjector.ProjectCtrlRamDiscovery(
                "single", null, [master, duplicate], [duplicateTargets], hasReadableBase: false));

        Assert.Contains("no topology-resolved target regions", empty.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate target region ids", duplicates.Message, StringComparison.Ordinal);
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

    private static TpFlashMapRegion Region(string id, string displayName, long start)
    {
        return new TpFlashMapRegion(
            id,
            displayName,
            TpFlashMapRegionKind.CtrlRam,
            new ByteRange(start, 0x20));
    }

    private static LegacyCombinerBlockArgument Block(
        string blockId,
        long sourceOffset,
        ByteRange firmwareRange)
    {
        return new LegacyCombinerBlockArgument(
            blockId,
            LegacyCombinerBlockSourceKind.StagedArtifact,
            "NF_Ctrlram.bin",
            sourceOffset,
            firmwareRange,
            stagedArtifactId: "nf");
    }
}
