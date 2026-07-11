using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests physical firmware-region invariants.</summary>
public sealed class FirmwareRegionTests
{
    /// <summary>Verifies one valid TP CtrlRAM region preserves canonical physical facts.</summary>
    [Fact]
    public void ConstructorCreatesTpCtrlRamRegion()
    {
        var region = new FirmwareRegion(
            "tp-control-ram",
            "tp-image",
            FirmwareRegionOwner.Tp,
            FirmwareRegionKind.CtrlRam,
            new ByteRange(16, 8),
            FirmwareWriteConstraint.WholeRegion,
            alignment: 4);

        Assert.Equal("tp-control-ram", region.RegionId);
        Assert.Equal("tp-image", region.ParentRegionId);
        Assert.Equal(FirmwareRegionOwner.Tp, region.Owner);
        Assert.Equal(FirmwareRegionKind.CtrlRam, region.Kind);
        Assert.Equal(new ByteRange(16, 8), region.Range);
        Assert.Equal(4, region.Alignment);
    }

    /// <summary>Verifies CtrlRAM cannot be classified under a non-TP owner.</summary>
    [Fact]
    public void ConstructorRejectsNonTpCtrlRam()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "control-ram",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.CtrlRam,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.WholeRegion));
    }

    /// <summary>Verifies customer information uses its canonical physical owner.</summary>
    [Fact]
    public void ConstructorRejectsNonCustomerOwnerForCustomerInformation()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "customer-information",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.CustomerInformation,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
    }

    /// <summary>Verifies explicit reserved gaps cannot grant write authority.</summary>
    [Fact]
    public void ConstructorRejectsWritableReservedGap()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "reserved-gap",
            null,
            FirmwareRegionOwner.Reserved,
            FirmwareRegionKind.Reserved,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.ExplicitRange));
    }

    /// <summary>Verifies canonical reserved and unmapped gap classifications are accepted.</summary>
    [Fact]
    public void ConstructorAcceptsCanonicalGapClassifications()
    {
        var reserved = new FirmwareRegion(
            "reserved-gap",
            null,
            FirmwareRegionOwner.Reserved,
            FirmwareRegionKind.Reserved,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden);
        var unmapped = new FirmwareRegion(
            "unmapped-gap",
            null,
            FirmwareRegionOwner.Unknown,
            FirmwareRegionKind.Unmapped,
            new ByteRange(4, 4),
            FirmwareWriteConstraint.Forbidden);

        Assert.Equal(FirmwareRegionOwner.Reserved, reserved.Owner);
        Assert.Equal(FirmwareRegionOwner.Unknown, unmapped.Owner);
    }

    /// <summary>Verifies reserved and unmapped kinds reject mismatched owners.</summary>
    [Fact]
    public void ConstructorRejectsInvalidGapOwners()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "reserved-gap",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Reserved,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "unmapped-gap",
            null,
            FirmwareRegionOwner.Reserved,
            FirmwareRegionKind.Unmapped,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
    }

    /// <summary>Verifies unmapped gaps cannot grant write authority.</summary>
    [Fact]
    public void ConstructorRejectsWritableUnmappedGap()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "unmapped-gap",
            null,
            FirmwareRegionOwner.Unknown,
            FirmwareRegionKind.Unmapped,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.ExplicitRange));
    }

    /// <summary>Verifies unknown ownership remains fail-closed.</summary>
    [Fact]
    public void ConstructorRejectsWriteAuthorityForUnknownOwner()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "unknown-data",
            null,
            FirmwareRegionOwner.Unknown,
            FirmwareRegionKind.Data,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.DeclaredSubregions));
    }

    /// <summary>Verifies physical region start and length satisfy declared alignment.</summary>
    [Fact]
    public void ConstructorRejectsMisalignedRange()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "misaligned",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Data,
            new ByteRange(2, 4),
            FirmwareWriteConstraint.Forbidden,
            alignment: 4));
    }

    /// <summary>Verifies zero alignment and misaligned length are rejected.</summary>
    [Fact]
    public void ConstructorRejectsInvalidAlignmentBoundaries()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareRegion(
            "zero-alignment",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Data,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden,
            alignment: 0));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "misaligned-length",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Data,
            new ByteRange(0, 6),
            FirmwareWriteConstraint.Forbidden,
            alignment: 4));
    }

    /// <summary>Verifies undefined physical enum values fail closed.</summary>
    [Fact]
    public void ConstructorRejectsUndefinedEnumValues()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareRegion(
            "bad-owner",
            null,
            (FirmwareRegionOwner)int.MaxValue,
            FirmwareRegionKind.Data,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareRegion(
            "bad-kind",
            null,
            FirmwareRegionOwner.System,
            (FirmwareRegionKind)int.MaxValue,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareRegion(
            "bad-write-constraint",
            null,
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Data,
            new ByteRange(0, 4),
            (FirmwareWriteConstraint)int.MaxValue));
    }

    /// <summary>Verifies parent identifiers are nonblank and cannot reference the child itself.</summary>
    [Fact]
    public void ConstructorRejectsInvalidParentIdentifiers()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "child",
            " ",
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Data,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegion(
            "child",
            "child",
            FirmwareRegionOwner.System,
            FirmwareRegionKind.Data,
            new ByteRange(0, 4),
            FirmwareWriteConstraint.Forbidden));
    }
}
