using System.Buffers.Binary;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Extracts stable display facts from the FWConfig structure embedded in a flash image.</summary>
public static class FirmwareConfigMetadataReader
{
    private const int FirmwareVersionOffset = 0x000;
    private const int FirmwareVersionBarOffset = 0x001;
    private const int FirmwareSubVersionOffset = 0x011;
    private const int CommonFwMajorVersionOffset = 0x01A;
    private const int CommonFwMinorVersionOffset = 0x01B;
    private const int CommonFwAdditionalVersionOffset = 0x01C;
    private const int ProjectIdOffset = 0x022;
    private const int RequiredLength = ProjectIdOffset + sizeof(ushort);

    /// <summary>Attempts to read FWConfig facts from <paramref name="image"/> at <paramref name="firmwareConfigStart"/>.</summary>
    public static bool TryRead(
        ReadOnlySpan<byte> image,
        long firmwareConfigStart,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (firmwareConfigStart < 0 ||
            firmwareConfigStart > int.MaxValue ||
            firmwareConfigStart + RequiredLength > image.Length)
        {
            return false;
        }

        int start = (int)firmwareConfigStart;
        byte firmwareVersion = image[start + FirmwareVersionOffset];
        byte firmwareVersionBar = image[start + FirmwareVersionBarOffset];
        byte commonFwMajorVersion = image[start + CommonFwMajorVersionOffset];
        byte commonFwMinorVersion = image[start + CommonFwMinorVersionOffset];
        byte commonFwAdditionalVersion = image[start + CommonFwAdditionalVersionOffset];
        ushort projectId = BinaryPrimitives.ReadUInt16LittleEndian(
            image.Slice(start + ProjectIdOffset, sizeof(ushort)));

        metadata = new FirmwareConfigMetadata(
            firmwareConfigStart,
            firmwareVersion,
            firmwareVersionBar,
            unchecked((byte)~firmwareVersion) == firmwareVersionBar,
            image[start + FirmwareSubVersionOffset],
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
