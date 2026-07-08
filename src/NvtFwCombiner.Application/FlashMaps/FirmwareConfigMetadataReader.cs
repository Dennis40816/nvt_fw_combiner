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
            image.Slice(start + FirmwareConfigLayout.ProjectIdOffset, FirmwareConfigLayout.ProjectIdLength));

        metadata = new FirmwareConfigMetadata(
            firmwareConfigStart,
            firmwareVersion,
            firmwareVersionBar,
            unchecked((byte)~firmwareVersion) == firmwareVersionBar,
            image[start + FirmwareConfigLayout.FirmwareSubVersionOffset],
            commonFwMajorVersion,
            commonFwMinorVersion,
            commonFwAdditionalVersion,
            projectId);
        return true;
    }
}

/// <summary>Display-oriented facts extracted from a flash image FWConfig structure.</summary>
public readonly record struct FirmwareConfigMetadata(
    long FirmwareConfigStart,
    byte FirmwareVersion,
    byte FirmwareVersionBar,
    bool IsFirmwareVersionBarValid,
    byte FirmwareSubVersion,
    byte CommonFwMajorVersion,
    byte CommonFwMinorVersion,
    byte CommonFwAdditionalVersion,
    ushort ProjectId)
{
    /// <summary>Common FW semantic version bytes.</summary>
    public string CommonFwVersion =>
        FormattableString.Invariant(
            $"{CommonFwMajorVersion}.{CommonFwMinorVersion}.{CommonFwAdditionalVersion}");
}
