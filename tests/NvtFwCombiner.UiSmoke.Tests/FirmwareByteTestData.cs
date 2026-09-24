using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class FirmwareByteTestData
{
    internal static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + (index % 251)));
        }

        return bytes;
    }

    internal static byte[] CreateHexPattern(int length)
    {
        return [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
    }

    internal static void WriteUiAbCmi(
        byte[] image,
        int bankStart,
        byte major,
        byte minor,
        ushort jira)
    {
        const int register16Offset = 0x401A;
        int start = checked(bankStart + register16Offset);
        image[start] = checked((byte)(jira & 0xFF));
        image[start + 1] = major;
        image[start + 2] = checked((byte)((minor << 4) | ((jira >> 8) & 0x0F)));
    }

    internal static byte[] CreateUiAbTpImage(
        byte version,
        byte subVersion,
        byte commonFwMajor,
        byte commonFwMinor,
        byte commonFwAdditional,
        ushort projectId)
    {
        const int tpLength = 0x40000;
        const int backupStart = 0x1000;
        const int markerStart = backupStart + 0xFFC;
        byte[] image = new byte[tpLength];
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = version;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~version);
        image[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = subVersion;
        image[backupStart + FirmwareConfigLayout.ChipNumberOffset] = 1;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = commonFwMajor;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = commonFwMinor;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = commonFwAdditional;
        image[backupStart + FirmwareConfigLayout.ProjectIdOffset] = (byte)(projectId & 0xFF);
        image[backupStart + FirmwareConfigLayout.ProjectIdOffset + 1] = checked((byte)(projectId >> 8));
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }

    // Valid primary/count metadata for format and layout tests, not a byte-output Golden.
    internal static byte[] CreateUiAbFormatTpImage(byte format, byte count)
    {
        byte[] tp = new byte[0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = format;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x36017] = count;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        return tp;
    }
}
