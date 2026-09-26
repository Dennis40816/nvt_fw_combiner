using System.Buffers.Binary;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Extracts stable display facts from the FWConfig structure embedded in a flash image.</summary>
public static class FirmwareConfigMetadataReader
{
    private const int NvtBackupMarkerLength = 4;
    private const int NvtBackupTerminalOffset = NvtBackupMarkerLength - 1;
    private const int NvtBackupStartDistanceBeforeTerminal = 0xFFF;

    /// <summary>
    /// Reads FWConfig facts from an absolute address for evidence and inspection only.
    /// Runtime consumers must use <see cref="TryReadBackup(ReadOnlySpan{byte}, out FirmwareConfigMetadata)"/>.
    /// </summary>
    public static bool TryReadAtAbsoluteAddress(
        ReadOnlySpan<byte> image,
        long structureStart,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (structureStart < 0 ||
            structureStart > int.MaxValue ||
            structureStart + FirmwareConfigLayout.RequiredLength > image.Length)
        {
            return false;
        }

        int start = (int)structureStart;
        byte firmwareVersion = image[start + FirmwareConfigLayout.FirmwareVersionOffset];
        byte firmwareVersionBar = image[start + FirmwareConfigLayout.FirmwareVersionBarOffset];
        byte commonFwMajorVersion = image[start + FirmwareConfigLayout.CommonFwMajorVersionOffset];
        byte commonFwMinorVersion = image[start + FirmwareConfigLayout.CommonFwMinorVersionOffset];
        byte commonFwAdditionalVersion = image[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset];
        ushort projectId = BinaryPrimitives.ReadUInt16LittleEndian(
            image[(start + FirmwareConfigLayout.ProjectIdOffset)..]);
        FirmwareConfigHardwareMetadata hardware = new(
            image[start + FirmwareConfigLayout.FreeRunModeOffset],
            image[start + FirmwareConfigLayout.SyncTypeOffset],
            image[start + FirmwareConfigLayout.SenseTerminalCountOffset],
            image[start + FirmwareConfigLayout.TouchPanelTerminalCountNormalOffset],
            image[start + FirmwareConfigLayout.TouchPanelTerminalCountSelfOffset],
            image[start + FirmwareConfigLayout.I2cDeviceAddressOffset],
            image[start + FirmwareConfigLayout.InterpolationStepXOffset],
            image[start + FirmwareConfigLayout.InterpolationStepYOffset],
            BinaryPrimitives.ReadUInt16LittleEndian(image[(start + FirmwareConfigLayout.S2dSensorDotsOffset)..]),
            image[start + FirmwareConfigLayout.MaxZoneCountOffset],
            unchecked((sbyte)image[start + FirmwareConfigLayout.InterpolationStartOffsetXOffset]),
            unchecked((sbyte)image[start + FirmwareConfigLayout.InterpolationStartOffsetYOffset]),
            image[start + FirmwareConfigLayout.MaxFingerCountOffset],
            ReadGipTable(image, start + FirmwareConfigLayout.GipBeforeLeftOffset),
            ReadGipTable(image, start + FirmwareConfigLayout.GipBeforeRightOffset),
            ReadGipTable(image, start + FirmwareConfigLayout.GipAfterLeftOffset),
            ReadGipTable(image, start + FirmwareConfigLayout.GipAfterRightOffset));

        metadata = new FirmwareConfigMetadata(
            structureStart,
            firmwareVersion,
            firmwareVersionBar,
            unchecked((byte)~firmwareVersion) == firmwareVersionBar,
            image[start + FirmwareConfigLayout.FirmwareSubVersionOffset],
            image[start + FirmwareConfigLayout.ChipNumberOffset],
            commonFwMajorVersion,
            commonFwMinorVersion,
            commonFwAdditionalVersion,
            projectId,
            hardware);
        return true;
    }

    /// <summary>
    /// Reads the canonical FWConfig Backup located at the unique NVT End Flag terminal byte minus
    /// <c>0xFFF</c>. Multiple complete exact NVT markers are rejected to avoid selecting an ambiguous source.
    /// </summary>
    public static bool TryReadBackup(ReadOnlySpan<byte> image, out FirmwareConfigMetadata metadata)
    {
        return TryReadBackup(image, out metadata, out _);
    }

    /// <summary>
    /// Reads the canonical FWConfig Backup and reports the complete exact NVT marker count for diagnostics.
    /// Existing whole-image compatibility read. Production code reads through a
    /// <see cref="FirmwareNvtEndFlagResolution"/>; this overload is deleted with the empty migration inventory.
    /// </summary>
    public static bool TryReadBackup(
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata,
        out int markerCount)
    {
        return TryReadBackupWithin(image, 0, image.Length, out metadata, out markerCount);
    }

    /// <summary>
    /// NVT-END-FLAG-1113-01 (ADR 0076): reads the canonical FWConfig Backup of a layout through its resolved end-flag
    /// declaration, in the image's own coordinates (a Base, a TP input or one bank). Declared: only the marker at the
    /// end flag counts, so <paramref name="markerCount"/> is 0 or 1 and markers elsewhere are neither counted nor
    /// rejected. Migration-inventory layout without a declaration: the existing compatibility read. Failed
    /// resolution: unreadable, never a whole-image search.
    /// </summary>
    public static bool TryReadBackup(
        ReadOnlySpan<byte> image,
        FirmwareNvtEndFlagResolution resolution,
        out FirmwareConfigMetadata metadata,
        out int markerCount)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.Succeeded)
        {
            metadata = default;
            markerCount = 0;
            return false;
        }

        if (resolution.EndFlag is not { } declaredEndFlag)
        {
            return TryReadBackupWithin(image, 0, image.Length, out metadata, out markerCount);
        }

        ByteRange position = declaredEndFlag.Position.Range;
        if (position.EndExclusive > image.Length)
        {
            metadata = default;
            markerCount = 0;
            return false;
        }

        return TryReadBackupWithin(image, checked((int)position.Start), checked((int)position.EndExclusive),
            out metadata, out markerCount);
    }

    private static bool TryReadBackupWithin(
        ReadOnlySpan<byte> image,
        int searchStart,
        int searchEndExclusive,
        out FirmwareConfigMetadata metadata,
        out int markerCount)
    {
        metadata = default;
        int? markerStart = null;
        markerCount = 0;
        for (int offset = searchStart; offset <= searchEndExclusive - NvtBackupMarkerLength; offset++)
        {
            if (image[offset] != 0x00 ||
                image[offset + 1] != (byte)'N' ||
                image[offset + 2] != (byte)'V' ||
                image[offset + 3] != (byte)'T')
            {
                continue;
            }

            markerCount++;
            markerStart ??= offset;
        }

        if (markerCount != 1 || markerStart is not { } start)
        {
            return false;
        }

        long backupStart = start + NvtBackupTerminalOffset - NvtBackupStartDistanceBeforeTerminal;
        return backupStart >= 0 &&
            backupStart + FirmwareConfigLayout.RequiredLength <= image.Length &&
            TryReadAtAbsoluteAddress(image, backupStart, out metadata);
    }

    private static FirmwareConfigGipTable ReadGipTable(ReadOnlySpan<byte> image, int start)
    {
        return new FirmwareConfigGipTable(
            BinaryPrimitives.ReadUInt32LittleEndian(image[start..]),
            BinaryPrimitives.ReadUInt32LittleEndian(image[(start + FirmwareConfigLayout.GipTableWordLength)..]),
            BinaryPrimitives.ReadUInt32LittleEndian(image[(start + (2 * FirmwareConfigLayout.GipTableWordLength))..]),
            BinaryPrimitives.ReadUInt32LittleEndian(image[(start + (3 * FirmwareConfigLayout.GipTableWordLength))..]));
    }
}

/// <summary>Display-oriented facts extracted from a flash image FWConfig structure.</summary>
public readonly record struct FirmwareConfigMetadata(
    long StructureStart,
    byte FirmwareVersion,
    byte FirmwareVersionBar,
    bool IsFirmwareVersionBarValid,
    byte FirmwareSubVersion,
    byte ChipNumber,
    byte CommonFwMajorVersion,
    byte CommonFwMinorVersion,
    byte CommonFwAdditionalVersion,
    ushort ProjectId,
    FirmwareConfigHardwareMetadata Hardware)
{
    /// <summary>Common FW semantic version bytes.</summary>
    public string CommonFwVersion =>
        FormattableString.Invariant(
            $"{CommonFwMajorVersion}.{CommonFwMinorVersion}.{CommonFwAdditionalVersion}");
}

/// <summary>Common-FW hardware facts defined by <c>ST_PUB_FW_CONFIG</c> offsets <c>0x029..0x07B</c>.</summary>
public readonly record struct FirmwareConfigHardwareMetadata(
    byte FreeRunMode,
    byte SyncType,
    byte SenseTerminalCount,
    byte TouchPanelTerminalCountNormal,
    byte TouchPanelTerminalCountSelf,
    byte I2cDeviceAddress,
    byte InterpolationStepX,
    byte InterpolationStepY,
    ushort S2dSensorDots,
    byte MaxZoneCount,
    sbyte InterpolationStartOffsetX,
    sbyte InterpolationStartOffsetY,
    byte MaxFingerCount,
    FirmwareConfigGipTable GipBeforeLeft,
    FirmwareConfigGipTable GipBeforeRight,
    FirmwareConfigGipTable GipAfterLeft,
    FirmwareConfigGipTable GipAfterRight);

/// <summary>Four little-endian GIP table words for one timing/direction group.</summary>
public readonly record struct FirmwareConfigGipTable(
    uint Table0,
    uint Table1,
    uint Table2,
    uint Table3);
