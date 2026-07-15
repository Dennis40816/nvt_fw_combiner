using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>
/// Exact FWConfig version writes derived from a canonical NVT Backup metadata record.
/// This type describes bytes only; callers remain responsible for using an approved output target.
/// </summary>
public sealed class FirmwareConfigVersionWritePlan
{
    private FirmwareConfigVersionWritePlan(
        long firmwareConfigStart,
        long backupFirmwareConfigStart,
        byte firmwareVersion,
        byte firmwareSubVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firmwareConfigStart);
        ArgumentOutOfRangeException.ThrowIfNegative(backupFirmwareConfigStart);

        FirmwareConfigStart = firmwareConfigStart;
        BackupFirmwareConfigStart = backupFirmwareConfigStart;
        FirmwareVersion = firmwareVersion;
        FirmwareSubVersion = firmwareSubVersion;
        FirmwareVersionAndBarRange = new ByteRange(
            checked(firmwareConfigStart + FirmwareConfigLayout.FirmwareVersionOffset),
            sizeof(ushort));
        FirmwareSubVersionRange = new ByteRange(
            checked(firmwareConfigStart + FirmwareConfigLayout.FirmwareSubVersionOffset),
            sizeof(byte));
        BackupFirmwareVersionAndBarRange = new ByteRange(
            checked(backupFirmwareConfigStart + FirmwareConfigLayout.FirmwareVersionOffset),
            sizeof(ushort));
        BackupFirmwareSubVersionRange = new ByteRange(
            checked(backupFirmwareConfigStart + FirmwareConfigLayout.FirmwareSubVersionOffset),
            sizeof(byte));
    }

    /// <summary>FWConfig structure start for the described writes.</summary>
    public long FirmwareConfigStart { get; }

    /// <summary>Canonical NVT Backup FWConfig start whose final values must be observed in the output.</summary>
    public long BackupFirmwareConfigStart { get; }

    /// <summary>User-confirmed TP FW version byte.</summary>
    public byte FirmwareVersion { get; }

    /// <summary>Bitwise inverse required by <c>u8FWVersionBar</c>.</summary>
    public byte FirmwareVersionBar => unchecked((byte)~FirmwareVersion);

    /// <summary>User-confirmed TP FW sub-version byte.</summary>
    public byte FirmwareSubVersion { get; }

    /// <summary>Contiguous <c>u8FWVersion</c> and <c>u8FWVersionBar</c> write range.</summary>
    public ByteRange FirmwareVersionAndBarRange { get; }

    /// <summary><c>u8FWSubVersion</c> write range.</summary>
    public ByteRange FirmwareSubVersionRange { get; }

    /// <summary>Canonical Backup range that must contain the final version and complement bytes after postbuild.</summary>
    public ByteRange BackupFirmwareVersionAndBarRange { get; }

    /// <summary>Canonical Backup range that must contain the final sub-version byte after postbuild.</summary>
    public ByteRange BackupFirmwareSubVersionRange { get; }

    /// <summary>Exact bytes for <see cref="FirmwareVersionAndBarRange"/>.</summary>
    public ReadOnlyMemory<byte> FirmwareVersionAndBarBytes => new byte[] { FirmwareVersion, FirmwareVersionBar };

    /// <summary>Exact bytes for <see cref="FirmwareSubVersionRange"/>.</summary>
    public ReadOnlyMemory<byte> FirmwareSubVersionBytes => new byte[] { FirmwareSubVersion };

    /// <summary>
    /// Creates writes from metadata read through <see cref="FirmwareConfigMetadataReader.TryReadBackup"/>.
    /// A malformed source FW/bar pair is rejected before any output mutation can be planned.
    /// </summary>
    public static FirmwareConfigVersionWritePlan CreateForBackup(
        FirmwareConfigMetadata backupMetadata,
        byte firmwareVersion,
        byte firmwareSubVersion)
    {
        return !backupMetadata.IsFirmwareVersionBarValid
            ? throw new ArgumentException(
                "FWConfig Backup has an invalid FW version complement byte.",
                nameof(backupMetadata))
            : new FirmwareConfigVersionWritePlan(
            backupMetadata.FirmwareConfigStart,
            backupMetadata.FirmwareConfigStart,
            firmwareVersion,
            firmwareSubVersion);
    }

    /// <summary>
    /// Rebinds the same reviewed field writes to a legacy Combiner-declared FWConfig source block.
    /// The caller must first prove that the processor block propagates to the canonical Backup start.
    /// </summary>
    public FirmwareConfigVersionWritePlan RebaseToCombinerSource(long firmwareConfigSourceStart)
    {
        return new FirmwareConfigVersionWritePlan(
            firmwareConfigSourceStart,
            BackupFirmwareConfigStart,
            FirmwareVersion,
            FirmwareSubVersion);
    }
}
