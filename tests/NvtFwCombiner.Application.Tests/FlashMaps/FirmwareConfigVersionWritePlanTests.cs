using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Tests canonical FWConfig version field write planning.</summary>
public sealed class FirmwareConfigVersionWritePlanTests
{
    /// <summary>Creates the version, complement, and sub-version writes at their reviewed offsets.</summary>
    [Fact]
    public void CreateForBackupUsesReviewedFieldsAndComplementsVersion()
    {
        FirmwareConfigMetadata metadata = CreateMetadata(0x3B000, firmwareVersionBarValid: true);

        var plan = FirmwareConfigVersionWritePlan.CreateForBackup(metadata, 0x27, 0x04);

        Assert.Equal(0x3B000, plan.FirmwareConfigStart);
        Assert.Equal(0x3B000, plan.BackupFirmwareConfigStart);
        Assert.Equal(new ByteRange(0x3B000, 2), plan.FirmwareVersionAndBarRange);
        Assert.Equal([0x27, 0xD8], plan.FirmwareVersionAndBarBytes.ToArray());
        Assert.Equal(new ByteRange(0x3B011, 1), plan.FirmwareSubVersionRange);
        Assert.Equal(new ByteRange(0x3B000, 2), plan.BackupFirmwareVersionAndBarRange);
        Assert.Equal(new ByteRange(0x3B011, 1), plan.BackupFirmwareSubVersionRange);
        Assert.Equal([0x04], plan.FirmwareSubVersionBytes.ToArray());
    }

    /// <summary>Rejects a Backup whose existing version complement is malformed.</summary>
    [Fact]
    public void CreateForBackupRejectsInvalidFirmwareVersionComplement()
    {
        FirmwareConfigMetadata metadata = CreateMetadata(0x3B000, firmwareVersionBarValid: false);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            FirmwareConfigVersionWritePlan.CreateForBackup(metadata, 0x27, 0x04));

        Assert.Contains("invalid FW version complement", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Moves reviewed values to the declared Combiner source without changing their bytes.</summary>
    [Fact]
    public void RebaseToCombinerSourcePreservesReviewedValues()
    {
        var backupPlan = FirmwareConfigVersionWritePlan.CreateForBackup(
            CreateMetadata(0x3B000, firmwareVersionBarValid: true),
            0x18,
            0x03);

        FirmwareConfigVersionWritePlan sourcePlan = backupPlan.RebaseToCombinerSource(0x22000);

        Assert.Equal(0x22000, sourcePlan.FirmwareConfigStart);
        Assert.Equal(0x3B000, sourcePlan.BackupFirmwareConfigStart);
        Assert.Equal(new ByteRange(0x22000, 2), sourcePlan.FirmwareVersionAndBarRange);
        Assert.Equal([0x18, 0xE7], sourcePlan.FirmwareVersionAndBarBytes.ToArray());
        Assert.Equal(new ByteRange(0x22011, 1), sourcePlan.FirmwareSubVersionRange);
        Assert.Equal([0x03], sourcePlan.FirmwareSubVersionBytes.ToArray());
        Assert.Equal(new ByteRange(0x3B000, 2), sourcePlan.BackupFirmwareVersionAndBarRange);
        Assert.Equal(new ByteRange(0x3B011, 1), sourcePlan.BackupFirmwareSubVersionRange);
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
