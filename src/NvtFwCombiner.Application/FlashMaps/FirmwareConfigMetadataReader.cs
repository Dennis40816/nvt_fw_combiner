using System.Buffers.Binary;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Extracts stable display facts from the FWConfig structure embedded in a flash image.</summary>
public static class FirmwareConfigMetadataReader
{
    /// <summary>Attempts to read FWConfig facts from <paramref name="image"/> at <paramref name="firmwareConfigStart"/>.</summary>
    public static bool TryRead(
        ReadOnlySpan<byte> image,
        long firmwareConfigStart,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (firmwareConfigStart < 0 ||
            firmwareConfigStart > int.MaxValue ||
            firmwareConfigStart + FirmwareConfigLayout.RequiredLength > image.Length)
        {
            return false;
        }

        int start = (int)firmwareConfigStart;
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
            firmwareConfigStart,
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
    long FirmwareConfigStart,
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
