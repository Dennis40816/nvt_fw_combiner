using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>
/// Exact FWConfig source-version writes derived from a canonical NVT Backup metadata record.
/// This type describes bytes only; callers remain responsible for using an approved output target.
/// </summary>
public sealed class FirmwareConfigVersionWritePlan
{
    private FirmwareConfigVersionWritePlan(
        long sourceStructureStart,
        long canonicalBackupStructureStart,
        byte firmwareVersion,
        byte firmwareSubVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceStructureStart);
        ArgumentOutOfRangeException.ThrowIfNegative(canonicalBackupStructureStart);

        SourceStructureStart = sourceStructureStart;
        CanonicalBackupStructureStart = canonicalBackupStructureStart;
        FirmwareVersion = firmwareVersion;
        FirmwareSubVersion = firmwareSubVersion;
        SourceFirmwareVersionAndBarRange = new ByteRange(
            checked(sourceStructureStart + FirmwareConfigLayout.FirmwareVersionOffset),
            sizeof(ushort));
        SourceFirmwareSubVersionRange = new ByteRange(
            checked(sourceStructureStart + FirmwareConfigLayout.FirmwareSubVersionOffset),
            sizeof(byte));
        CanonicalBackupFirmwareVersionAndBarRange = new ByteRange(
            checked(canonicalBackupStructureStart + FirmwareConfigLayout.FirmwareVersionOffset),
            sizeof(ushort));
        CanonicalBackupFirmwareSubVersionRange = new ByteRange(
            checked(canonicalBackupStructureStart + FirmwareConfigLayout.FirmwareSubVersionOffset),
            sizeof(byte));
    }

    /// <summary>Original FWConfig structure whose reviewed fields are written before postbuild.</summary>
    public long SourceStructureStart { get; }

    /// <summary>Canonical NVT Backup FWConfig start whose final values must be observed in the output.</summary>
    public long CanonicalBackupStructureStart { get; }

    /// <summary>User-confirmed TP FW version byte.</summary>
    public byte FirmwareVersion { get; }

    /// <summary>Bitwise inverse required by <c>u8FWVersionBar</c>.</summary>
    public byte FirmwareVersionBar => unchecked((byte)~FirmwareVersion);

    /// <summary>User-confirmed TP FW sub-version byte.</summary>
    public byte FirmwareSubVersion { get; }

    /// <summary>Contiguous <c>u8FWVersion</c> and <c>u8FWVersionBar</c> write range.</summary>
    public ByteRange SourceFirmwareVersionAndBarRange { get; }

    /// <summary><c>u8FWSubVersion</c> write range.</summary>
    public ByteRange SourceFirmwareSubVersionRange { get; }

    /// <summary>Canonical Backup range that must contain the final version and complement bytes after postbuild.</summary>
    public ByteRange CanonicalBackupFirmwareVersionAndBarRange { get; }

    /// <summary>Canonical Backup range that must contain the final sub-version byte after postbuild.</summary>
    public ByteRange CanonicalBackupFirmwareSubVersionRange { get; }

    /// <summary>Exact bytes for <see cref="SourceFirmwareVersionAndBarRange"/>.</summary>
    public ReadOnlyMemory<byte> SourceFirmwareVersionAndBarBytes => new byte[] { FirmwareVersion, FirmwareVersionBar };

    /// <summary>Exact bytes for <see cref="SourceFirmwareSubVersionRange"/>.</summary>
    public ReadOnlyMemory<byte> SourceFirmwareSubVersionBytes => new byte[] { FirmwareSubVersion };

    /// <summary>
    /// Creates writes from metadata read through <see cref="FirmwareConfigMetadataReader.TryReadBackup"/>.
    /// A malformed source FW/bar pair is rejected before any output mutation can be planned.
    /// </summary>
    public static FirmwareConfigVersionWritePlan CreateFromCanonicalBackup(
        FirmwareConfigMetadata backupMetadata,
        byte firmwareVersion,
        byte firmwareSubVersion)
    {
        return !backupMetadata.IsFirmwareVersionBarValid
            ? throw new ArgumentException(
                "FWConfig Backup has an invalid FW version complement byte.",
                nameof(backupMetadata))
            : new FirmwareConfigVersionWritePlan(
            backupMetadata.StructureStart,
            backupMetadata.StructureStart,
            firmwareVersion,
            firmwareSubVersion);
    }

    /// <summary>
    /// Rebinds the same reviewed field writes to a legacy Combiner-declared FWConfig source block.
    /// The caller must first prove that the processor block propagates to the canonical Backup start.
    /// </summary>
    public FirmwareConfigVersionWritePlan RebaseToSourceStructure(long sourceStructureStart)
    {
        return new FirmwareConfigVersionWritePlan(
            sourceStructureStart,
            CanonicalBackupStructureStart,
            FirmwareVersion,
            FirmwareSubVersion);
    }
}
