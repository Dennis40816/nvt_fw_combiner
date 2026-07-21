using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Tests canonical FWConfig version field write planning.</summary>
public sealed class FirmwareConfigVersionWritePlanTests
{
    /// <summary>Creates the version, complement, and sub-version writes at their reviewed offsets.</summary>
    [Fact]
    public void CreateFromCanonicalBackupUsesReviewedFieldsAndComplementsVersion()
    {
        FirmwareConfigMetadata metadata = CreateMetadata(0x3B000, firmwareVersionBarValid: true);

        var plan = FirmwareConfigVersionWritePlan.CreateFromCanonicalBackup(metadata, 0x27, 0x04);

        Assert.Equal(0x3B000, plan.SourceStructureStart);
        Assert.Equal(0x3B000, plan.CanonicalBackupStructureStart);
        Assert.Equal(new ByteRange(0x3B000, 2), plan.SourceFirmwareVersionAndBarRange);
        Assert.Equal([0x27, 0xD8], plan.SourceFirmwareVersionAndBarBytes.ToArray());
        Assert.Equal(new ByteRange(0x3B011, 1), plan.SourceFirmwareSubVersionRange);
        Assert.Equal(new ByteRange(0x3B000, 2), plan.CanonicalBackupFirmwareVersionAndBarRange);
        Assert.Equal(new ByteRange(0x3B011, 1), plan.CanonicalBackupFirmwareSubVersionRange);
        Assert.Equal([0x04], plan.SourceFirmwareSubVersionBytes.ToArray());
    }

    /// <summary>Rejects a Backup whose existing version complement is malformed.</summary>
    [Fact]
    public void CreateFromCanonicalBackupRejectsInvalidFirmwareVersionComplement()
    {
        FirmwareConfigMetadata metadata = CreateMetadata(0x3B000, firmwareVersionBarValid: false);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            FirmwareConfigVersionWritePlan.CreateFromCanonicalBackup(metadata, 0x27, 0x04));

        Assert.Contains("invalid FW version complement", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Moves reviewed values to the declared Combiner source without changing their bytes.</summary>
    [Fact]
    public void RebaseToSourceStructurePreservesReviewedValues()
    {
        var backupPlan = FirmwareConfigVersionWritePlan.CreateFromCanonicalBackup(
            CreateMetadata(0x3B000, firmwareVersionBarValid: true),
            0x18,
            0x03);

        FirmwareConfigVersionWritePlan sourcePlan = backupPlan.RebaseToSourceStructure(0x22000);

        Assert.Equal(0x22000, sourcePlan.SourceStructureStart);
        Assert.Equal(0x3B000, sourcePlan.CanonicalBackupStructureStart);
        Assert.Equal(new ByteRange(0x22000, 2), sourcePlan.SourceFirmwareVersionAndBarRange);
        Assert.Equal([0x18, 0xE7], sourcePlan.SourceFirmwareVersionAndBarBytes.ToArray());
        Assert.Equal(new ByteRange(0x22011, 1), sourcePlan.SourceFirmwareSubVersionRange);
        Assert.Equal([0x03], sourcePlan.SourceFirmwareSubVersionBytes.ToArray());
        Assert.Equal(new ByteRange(0x3B000, 2), sourcePlan.CanonicalBackupFirmwareVersionAndBarRange);
        Assert.Equal(new ByteRange(0x3B011, 1), sourcePlan.CanonicalBackupFirmwareSubVersionRange);
    }

    private static FirmwareConfigMetadata CreateMetadata(long start, bool firmwareVersionBarValid)
    {
        return new FirmwareConfigMetadata(
            start,
            0x10,
            (byte)(firmwareVersionBarValid ? 0xEF : 0x00),
            firmwareVersionBarValid,
            0x02,
            1,
            1,
            4,
            1,
            0,
            new FirmwareConfigHardwareMetadata(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                default,
                default,
                default,
                default));
    }
}
